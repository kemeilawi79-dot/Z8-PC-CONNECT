using System.Drawing;
using System.Drawing.Imaging;
using System.Net;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://0.0.0.0:8765");
var app = builder.Build();
app.UseWebSockets();
var pairingCode = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
var tokens = new HashSet<string>(StringComparer.Ordinal);
var gate = new object();
var receivedFiles = new System.Collections.Concurrent.ConcurrentQueue<object>();
Console.WriteLine("Z8_READY");
Console.WriteLine($"PAIRING_CODE={pairingCode}");
Console.WriteLine($"PC_NAME={Environment.MachineName}");
foreach (var ip in GetIPv4Addresses()) Console.WriteLine($"PC_IP={ip}");
Console.WriteLine("Z8 PC CONNECT server ready on TCP 8765.");

app.MapGet("/health", () => Results.Json(new { app = "Z8 PC CONNECT", status = "ready", version = "0.4.0", transport = "LAN WebSocket" }));
app.MapGet("/status", () => Results.Json(new {
    app = "Z8 PC CONNECT", status = "ready", pcName = Environment.MachineName,
    pairingCode, port = 8765, addresses = GetIPv4Addresses()
}));
app.MapPost("/pair", (PairRequest req) => {
    lock (gate) {
        if (string.IsNullOrWhiteSpace(req.Code) || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(req.Code.Trim()), Encoding.UTF8.GetBytes(pairingCode)))
            return Results.Unauthorized();
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+','-').Replace('/','_');
        tokens.Add(token);
        Console.WriteLine($"PHONE_PAIRED at {DateTime.Now:T}");
        return Results.Json(new { token, pcName = Environment.MachineName, user = Environment.UserName });
    }
});
app.MapPost("/revoke", (HttpRequest req) => {
    var token = req.Headers.Authorization.ToString().Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase);
    lock (gate) tokens.Remove(token);
    return Results.Ok(new { revoked = true });
});
app.MapGet("/clipboard", (HttpRequest req) => {
    if (!Authorized(req, tokens, gate)) return Results.Unauthorized();
    try { return Results.Json(new { text = ReadClipboard() }); } catch { return Results.Json(new { text = "" }); }
});
app.MapPost("/clipboard", (ClipboardRequest body, HttpRequest req) => {
    if (!Authorized(req, tokens, gate)) return Results.Unauthorized();
    try { SetClipboard(body.Text ?? ""); return Results.Ok(new { saved = true }); } catch { return Results.Problem("Clipboard unavailable"); }
});
app.MapGet("/clipboard/image", (HttpRequest req) => {
    if (!Authorized(req, tokens, gate)) return Results.Unauthorized();
    try {
        var image = ReadClipboardImage();
        return image == null ? Results.NotFound(new { error = "No image is on the PC clipboard." }) : Results.File(image, "image/png", "z8-pc-clipboard.png");
    } catch { return Results.Problem("Could not read the PC image clipboard."); }
});
app.MapPost("/clipboard/image", async (HttpRequest req) => {
    if (!Authorized(req, tokens, gate)) return Results.Unauthorized();
    if (req.ContentLength is > 25000000) return Results.BadRequest(new { error = "Clipboard image exceeds 25 MB." });
    using var buffer = new MemoryStream();
    await req.Body.CopyToAsync(buffer);
    if (buffer.Length < 8 || buffer.Length > 25000000) return Results.BadRequest(new { error = "Invalid or oversized image." });
    try { SetClipboardImage(buffer.ToArray()); return Results.Ok(new { saved = true, bytes = buffer.Length }); }
    catch { return Results.Problem("Could not write the PC image clipboard."); }
});
app.MapPost("/files/upload", async (HttpRequest req) => {
    if (!Authorized(req, tokens, gate)) return Results.Unauthorized();
    var name = Path.GetFileName(req.Query["name"].ToString());
    if (string.IsNullOrWhiteSpace(name)) name = "received-file";
    var destination = req.Query["destination"].ToString();
    var folder = destination.ToLowerInvariant() switch {
        "desktop" => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
        "documents" => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        _ => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")
    };
    Directory.CreateDirectory(folder);
    var path = Path.Combine(folder, name);
    var stem = Path.GetFileNameWithoutExtension(name);
    var ext = Path.GetExtension(name);
    for (var n = 1; File.Exists(path); n++) path = Path.Combine(folder, $"{stem} ({n}){ext}");
    await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
        await req.Body.CopyToAsync(output);
    }
    receivedFiles.Enqueue(new { name = Path.GetFileName(path), path, destination = Path.GetFileName(folder), receivedAt = DateTimeOffset.Now });
    while (receivedFiles.Count > 30) receivedFiles.TryDequeue(out _);
    Console.WriteLine($"FILE_RECEIVED={path}");
    return Results.Json(new { saved = true, name = Path.GetFileName(path), path, destination = Path.GetFileName(folder) });
});
app.MapGet("/files/recent", (HttpContext ctx) => {
    if (ctx.Connection.RemoteIpAddress is { } remote && !IPAddress.IsLoopback(remote.MapToIPv4())) return Results.Unauthorized();
    return Results.Json(receivedFiles.ToArray());
});
app.Map("/remote", async (HttpContext ctx) => {
    var token = ctx.Request.Query["token"].ToString();
    lock (gate) { if (!tokens.Contains(token)) { ctx.Response.StatusCode = 401; return; } }
    if (!ctx.WebSockets.IsWebSocketRequest) { ctx.Response.StatusCode = 400; return; }
    using var socket = await ctx.WebSockets.AcceptWebSocketAsync();
    Console.WriteLine("REMOTE_SCREEN_CONNECTED");
    using var stop = CancellationTokenSource.CreateLinkedTokenSource(ctx.RequestAborted);
    var receiveTask = Task.Run(async () => {
        var buffer = new byte[16384];
        while (socket.State == WebSocketState.Open && !stop.IsCancellationRequested) {
            using var message = new MemoryStream();
            WebSocketReceiveResult result;
            do {
                result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), stop.Token);
                if (result.MessageType == WebSocketMessageType.Close) return;
                if (result.MessageType == WebSocketMessageType.Text) message.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);
            if (result.MessageType == WebSocketMessageType.Text) {
                try { HandleInput(JsonDocument.Parse(message.ToArray()).RootElement); }
                catch (Exception ex) { Console.WriteLine($"Input rejected: {ex.Message}"); }
            }
        }
    }, stop.Token);
    try {
        while (socket.State == WebSocketState.Open && !stop.IsCancellationRequested) {
            using var bmp = CaptureScreen();
            using var ms = new MemoryStream();
            var encoder = ImageCodecInfo.GetImageEncoders().First(x => x.MimeType == "image/jpeg");
            using var parameters = new EncoderParameters(1);
            parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 72L);
            bmp.Save(ms, encoder, parameters);
            await socket.SendAsync(ms.ToArray(), WebSocketMessageType.Binary, true, stop.Token);
            await Task.Delay(120, stop.Token);
        }
    } catch (OperationCanceledException) { }
      catch (WebSocketException ex) { Console.WriteLine($"Phone disconnected: {ex.Message}"); }
      finally {
        stop.Cancel();
        try { await receiveTask; } catch { }
        if (socket.State == WebSocketState.Open) {
            try { await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", CancellationToken.None); } catch { }
        }
        Console.WriteLine("REMOTE_SCREEN_DISCONNECTED");
      }
});
app.Run();

static string[] GetIPv4Addresses() =>
    System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
        .Where(n => n.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up)
        .SelectMany(n => n.GetIPProperties().UnicastAddresses)
        .Where(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(a.Address) && !a.Address.ToString().StartsWith("169.254."))
        .Select(a => a.Address.ToString()).Distinct().ToArray();

static bool Authorized(HttpRequest req, HashSet<string> tokens, object gate) {
    var value = req.Headers.Authorization.ToString();
    if (!value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return false;
    lock (gate) return tokens.Contains(value[7..]);
}
static Bitmap CaptureScreen() {
    var bounds = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0,0,1280,720);
    var bmp = new Bitmap(Math.Max(1,bounds.Width), Math.Max(1,bounds.Height));
    using var g = Graphics.FromImage(bmp);
    g.CopyFromScreen(bounds.Left,bounds.Top,0,0,bounds.Size);
    return bmp;
}
static void HandleInput(JsonElement e) {
    var type = e.TryGetProperty("type", out var t) ? t.GetString() : "";
    if (type == "move") Native.SetCursorPos(e.GetProperty("x").GetInt32(), e.GetProperty("y").GetInt32());
    else if (type == "mouseDown") Native.mouse_event(2,0,0,0,UIntPtr.Zero);
    else if (type == "mouseUp") Native.mouse_event(4,0,0,0,UIntPtr.Zero);
    else if (type == "click") { Native.mouse_event(2,0,0,0,UIntPtr.Zero); Native.mouse_event(4,0,0,0,UIntPtr.Zero); }
    else if (type == "rightClick") { Native.mouse_event(8,0,0,0,UIntPtr.Zero); Native.mouse_event(16,0,0,0,UIntPtr.Zero); }
    else if (type == "scroll") Native.mouse_event(0x0800,0,0,e.GetProperty("delta").GetInt32(),UIntPtr.Zero);
    else if (type == "key") {
        var vk=(ushort)e.GetProperty("keyCode").GetInt32();
        Native.keybd_event((byte)vk,0,0,UIntPtr.Zero); Native.keybd_event((byte)vk,0,2,UIntPtr.Zero);
    }
    else if (type == "keyCombo") {
        var keys = e.GetProperty("keys").EnumerateArray().Select(x => (byte)x.GetInt32()).ToArray();
        if (keys.Length > 0) {
            foreach (var modifier in keys.Take(keys.Length - 1)) Native.keybd_event(modifier,0,0,UIntPtr.Zero);
            var last = keys[^1];
            Native.keybd_event(last,0,0,UIntPtr.Zero);
            Native.keybd_event(last,0,2,UIntPtr.Zero);
            foreach (var modifier in keys.Take(keys.Length - 1).Reverse()) Native.keybd_event(modifier,0,2,UIntPtr.Zero);
        }
    }
    else if (type == "text") {
        foreach (char c in e.GetProperty("text").GetString() ?? "") {
            var mapped=Native.VkKeyScan(c);
            if(mapped == -1) { Native.SendUnicode(c); continue; }
            var vk=(byte)(mapped & 0xff); var shift=(mapped & 0x100)!=0; var ctrl=(mapped & 0x200)!=0; var alt=(mapped & 0x400)!=0;
            if(shift) Native.keybd_event(0x10,0,0,UIntPtr.Zero);
            if(ctrl) Native.keybd_event(0x11,0,0,UIntPtr.Zero);
            if(alt) Native.keybd_event(0x12,0,0,UIntPtr.Zero);
            Native.keybd_event(vk,0,0,UIntPtr.Zero); Native.keybd_event(vk,0,2,UIntPtr.Zero);
            if(alt) Native.keybd_event(0x12,0,2,UIntPtr.Zero);
            if(ctrl) Native.keybd_event(0x11,0,2,UIntPtr.Zero);
            if(shift) Native.keybd_event(0x10,0,2,UIntPtr.Zero);
        }
    }
}
static string ReadClipboard() { string value=""; var t=new Thread(()=> { if(Clipboard.ContainsText()) value=Clipboard.GetText(); }); t.SetApartmentState(ApartmentState.STA); t.Start(); t.Join(); return value; }
static void SetClipboard(string value) { var t=new Thread(()=>Clipboard.SetText(value)); t.SetApartmentState(ApartmentState.STA); t.Start(); t.Join(); }
static byte[]? ReadClipboardImage() {
    byte[]? value = null;
    var t = new Thread(() => {
        if (Clipboard.ContainsImage()) {
            using var image = Clipboard.GetImage();
            if (image != null) {
                using var ms = new MemoryStream();
                image.Save(ms, ImageFormat.Png);
                value = ms.ToArray();
            }
        }
    });
    t.SetApartmentState(ApartmentState.STA); t.Start(); t.Join();
    return value;
}
static void SetClipboardImage(byte[] bytes) {
    using var ms = new MemoryStream(bytes);
    using var source = new Bitmap(ms);
    using var copy = new Bitmap(source);
    var t = new Thread(() => Clipboard.SetImage(copy));
    t.SetApartmentState(ApartmentState.STA); t.Start(); t.Join();
}
static class Native {
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int X,int Y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags,uint dx,uint dy,int data,UIntPtr extra);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk,byte scan,uint flags,UIntPtr extra);
    [DllImport("user32.dll")] public static extern short VkKeyScan(char ch);
    public static void SendUnicode(char ch) {
        var input = new INPUT { type = 1, U = new InputUnion { ki = new KEYBDINPUT { wVk = 0, wScan = ch, dwFlags = 4 } } };
        SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
        input.U.ki.dwFlags = 6;
        SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }
    [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint type; public InputUnion U; }
    [StructLayout(LayoutKind.Explicit)] struct InputUnion { [FieldOffset(0)] public KEYBDINPUT ki; }
    [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort wVk,wScan; public uint dwFlags,time; public IntPtr dwExtraInfo; }
    [DllImport("user32.dll", SetLastError=true)] static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
}
record PairRequest(string Code);
record ClipboardRequest(string? Text);
