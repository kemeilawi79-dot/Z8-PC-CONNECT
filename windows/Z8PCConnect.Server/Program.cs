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
Console.WriteLine("Z8_READY");
Console.WriteLine($"PAIRING_CODE={pairingCode}");
Console.WriteLine($"PC_NAME={Environment.MachineName}");
foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList.Where(x => x.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !IPAddress.IsLoopback(x)))
    Console.WriteLine($"PC_IP={ip}");
Console.WriteLine("Z8 PC CONNECT server ready on TCP 8765.");

app.MapGet("/health", () => Results.Json(new { app = "Z8 PC CONNECT", status = "ready", version = "0.3.0", transport = "LAN WebSocket" }));
app.MapPost("/pair", (PairRequest req) => {
    lock (gate) {
        if (req.Code != pairingCode) return Results.Unauthorized();
        pairingCode = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
        Console.WriteLine($"PAIRING_CODE={pairingCode}");
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+','-').Replace('/','_');
        tokens.Add(token);
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
app.MapPost("/files/upload", async (HttpRequest req) => {
    if (!Authorized(req, tokens, gate)) return Results.Unauthorized();
    var name = Path.GetFileName(req.Query["name"].ToString());
    if (string.IsNullOrWhiteSpace(name)) return Results.BadRequest("Missing filename");
    var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Z8 PC CONNECT Received");
    Directory.CreateDirectory(folder);
    var path = Path.Combine(folder, name);
    await using var output = File.Create(path);
    await req.Body.CopyToAsync(output);
    return Results.Json(new { saved = true, path });
});
app.Map("/remote", async (HttpContext ctx) => {
    var token = ctx.Request.Query["token"].ToString();
    lock (gate) { if (!tokens.Contains(token)) { ctx.Response.StatusCode = 401; return; } }
    if (!ctx.WebSockets.IsWebSocketRequest) { ctx.Response.StatusCode = 400; return; }
    using var socket = await ctx.WebSockets.AcceptWebSocketAsync();
    var receiveTask = Task.Run(async () => {
        var buffer = new byte[16384];
        while (socket.State == WebSocketState.Open && !ctx.RequestAborted.IsCancellationRequested) {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ctx.RequestAborted);
            if (result.MessageType == WebSocketMessageType.Close) break;
            if (result.MessageType == WebSocketMessageType.Text) {
                try { HandleInput(JsonDocument.Parse(Encoding.UTF8.GetString(buffer, 0, result.Count)).RootElement); }
                catch (Exception ex) { Console.WriteLine($"Input rejected: {ex.Message}"); }
            }
        }
    }, ctx.RequestAborted);
    while (socket.State == WebSocketState.Open && !ctx.RequestAborted.IsCancellationRequested) {
        try {
            using var bmp = CaptureScreen();
            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Jpeg);
            await socket.SendAsync(new ArraySegment<byte>(ms.ToArray()), WebSocketMessageType.Binary, true, ctx.RequestAborted);
        } catch (OperationCanceledException) { break; }
          catch (Exception ex) { Console.WriteLine($"Capture: {ex.Message}"); }
        await Task.Delay(100, ctx.RequestAborted);
    }
    try { await receiveTask; } catch { }
    if (socket.State == WebSocketState.Open) await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", CancellationToken.None);
});
app.Run();

static bool Authorized(HttpRequest req, HashSet<string> tokens, object gate) {
    var value = req.Headers.Authorization.ToString();
    if (!value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return false;
    lock (gate) return tokens.Contains(value[7..]);
}
static Bitmap CaptureScreen() {
    var bounds = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0,0,1280,720);
    var bmp = new Bitmap(Math.Max(1,bounds.Width), Math.Max(1,bounds.Height));
    using var g = Graphics.FromImage(bmp); g.CopyFromScreen(bounds.Left,bounds.Top,0,0,bounds.Size);
    return bmp;
}
static void HandleInput(JsonElement e) {
    var type = e.TryGetProperty("type", out var t) ? t.GetString() : "";
    if (type == "move") Native.SetCursorPos(e.GetProperty("x").GetInt32(), e.GetProperty("y").GetInt32());
    else if (type == "click") { Native.mouse_event(2,0,0,0,UIntPtr.Zero); Native.mouse_event(4,0,0,0,UIntPtr.Zero); }
    else if (type == "rightClick") { Native.mouse_event(8,0,0,0,UIntPtr.Zero); Native.mouse_event(16,0,0,0,UIntPtr.Zero); }
    else if (type == "scroll") Native.mouse_event(0x0800,0,0,e.GetProperty("delta").GetInt32(),UIntPtr.Zero);
    else if (type == "key") {
        var vk=(ushort)e.GetProperty("keyCode").GetInt32();
        Native.keybd_event((byte)vk,0,0,UIntPtr.Zero); Native.keybd_event((byte)vk,0,2,UIntPtr.Zero);
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
