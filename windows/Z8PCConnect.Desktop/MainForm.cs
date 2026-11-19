using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text.Json;

namespace Z8PCConnect.Desktop;

public sealed class MainForm : Form
{
    private static readonly Color Forest = Color.FromArgb(23, 76, 60);
    private static readonly Color Deep = Color.FromArgb(10, 42, 36);
    private static readonly Color Sand = Color.FromArgb(240, 237, 228);
    private static readonly Color Ice = Color.FromArgb(185, 231, 242);
    private static readonly Color Ink = Color.FromArgb(24, 53, 46);
    private readonly Label state = new() { AutoSize = true, Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = Forest };
    private readonly Label code = new() { AutoSize = true, Font = new Font("Segoe UI", 32, FontStyle.Bold), ForeColor = Forest };
    private readonly Label ips = new() { AutoSize = true, Font = new Font("Segoe UI", 13, FontStyle.Bold), ForeColor = Forest };
    private readonly Label pcName = new() { AutoSize = true, ForeColor = Ink, Font = new Font("Segoe UI", 10) };
    private readonly TextBox log = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = Deep, ForeColor = Sand, BorderStyle = BorderStyle.None, Dock = DockStyle.Fill, Font = new Font("Consolas", 9) };
    private readonly Button start = new() { Text = "START CONNECTION", AutoSize = true };
    private readonly Button stop = new() { Text = "STOP", AutoSize = true, Enabled = false };
    private readonly Button copyIp = new() { Text = "COPY PC ADDRESS", AutoSize = true };
    private readonly Button copyCode = new() { Text = "COPY PAIRING CODE", AutoSize = true };
    private readonly Button cast = new() { Text = "OPEN SEPARATE PHONE CAST", AutoSize = true };
    private readonly Button embedPhone = new() { Text = "OPEN IN-APP PHONE CONTROL", AutoSize = true };
    private readonly PictureBox phoneView = new() { Size = new Size(260, 470), BackColor = Color.Black, BorderStyle = BorderStyle.FixedSingle, SizeMode = PictureBoxSizeMode.Zoom, TabStop = true };
    private readonly System.Windows.Forms.Timer phoneRefresh = new() { Interval = 650 };
    private readonly TextBox phoneText = new() { Size = new Size(600, 34), PlaceholderText = "Type text to send to the phone…" };
    private readonly Button sendPhoneText = new() { Text = "SEND TO PHONE", AutoSize = true };
    private readonly Label transferSummary = new() { Text = "Files received from your phone will appear here.", Location = new Point(28, 58), Size = new Size(900, 42), ForeColor = Ink, Font = new Font("Segoe UI", 10) };
    private readonly Button openDownloads = new() { Text = "OPEN LAPTOP DOWNLOADS", AutoSize = true };
    private string phoneSerial = "";
    private Point phoneDown;
    private bool phoneCaptureBusy;
    private Process? phoneMirror;
    private readonly System.Windows.Forms.Timer refresh = new() { Interval = 2500 };
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(2) };
    private Process? server;
    private readonly FlowLayoutPanel page = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(28), BackColor = Sand };

    public MainForm()
    {
        Text = "Z8 PC CONNECT";
        Width = 1180; Height = 650; MinimumSize = new Size(1000, 560);
        BackColor = Deep; ForeColor = Ink;
        StartPosition = FormStartPosition.CenterScreen;
        Controls.Add(page);
        page.SizeChanged += (_, _) => { var side = Math.Max(28, (page.ClientSize.Width - 980) / 2); page.Padding = new Padding(side, 24, side, 24); };
        page.Padding = new Padding(28);
        BuildPage();
        refresh.Tick += async (_, _) => await RefreshStatus();
        phoneRefresh.Tick += async (_, _) => await RefreshPhoneFrame();
        Shown += async (_, _) => { await StartServer(); refresh.Start(); };
        FormClosing += (_, _) => { refresh.Stop(); phoneRefresh.Stop(); StopServer(); phoneView.Image?.Dispose(); http.Dispose(); };
    }

    private void BuildPage()
    {
        var hero = new Panel { Width = 980, Height = 120, BackColor = Deep, Margin = new Padding(0, 0, 0, 18) };
        hero.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var b = new SolidBrush(Deep); e.Graphics.FillRectangle(b, hero.ClientRectangle);
            using var family = new FontFamily("Segoe UI");
            using var logoPath = new GraphicsPath();
            logoPath.AddString("Z8", family, (int)(FontStyle.Bold | FontStyle.Italic), 66, new PointF(28, 12), StringFormat.GenericDefault);
            using var shadow = new SolidBrush(Color.FromArgb(85, 0, 0, 0));
            using var outline = new Pen(Forest, 2.4f);
            using var brush = new LinearGradientBrush(new Rectangle(24, 10, 170, 86), Sand, Ice, 0f);
            e.Graphics.TranslateTransform(2, 3);
            e.Graphics.FillPath(shadow, logoPath);
            e.Graphics.ResetTransform();
            e.Graphics.DrawPath(outline, logoPath);
            e.Graphics.FillPath(brush, logoPath);
            using var title = new Font("Segoe UI", 23, FontStyle.Bold);
            using var sub = new Font("Segoe UI", 10, FontStyle.Regular);
            using var white = new SolidBrush(Sand); using var cyan = new SolidBrush(Ice);
            e.Graphics.DrawString("Z8 PC CONNECT", title, white, 210, 25);
            e.Graphics.DrawString("YOUR PC. IN YOUR HANDS.   •   CONNECT  /  CONTROL  /  ACCESS", sub, cyan, 214, 67);
        };
        page.Controls.Add(hero);

        var connection = MakeCard(980, 300);
        connection.Controls.Add(MakeHeading("PC CONNECTION"));
        state.Text = "Starting connection service…";
        state.Location = new Point(28, 62);
        pcName.Text = "This laptop"; pcName.Location = new Point(28, 92);
        var ipLabel = MakeSmall("PC ADDRESS  ·  PORT 8765", 28, 130);
        ips.Location = new Point(28, 154); ips.Text = "Finding active network…";
        code.Location = new Point(550, 145); code.Text = "------";
        var codeLabel = MakeSmall("PAIRING CODE  ·  ENTER THIS ON YOUR PHONE", 550, 116);
        Style(start); Style(stop); Style(copyIp); Style(copyCode);
        start.Location = new Point(28, 232); stop.Location = new Point(245, 232);
        copyIp.Location = new Point(330, 232); copyCode.Location = new Point(520, 232);
        start.Click += async (_, _) => await StartServer();
        stop.Click += (_, _) => StopServer();
        copyIp.Click += (_, _) => { if (!string.IsNullOrWhiteSpace(ips.Text)) Clipboard.SetText(ips.Text.Split(' ')[0]); };
        copyCode.Click += (_, _) => { if (code.Text.Length == 6) Clipboard.SetText(code.Text); };
        connection.Controls.AddRange([state, pcName, ipLabel, ips, codeLabel, code, start, stop, copyIp, copyCode]);
        page.Controls.Add(connection);

        var guide = MakeCard(980, 225);
        guide.Controls.Add(MakeHeading("CONNECT YOUR ANDROID PHONE"));
        AddGuide(guide, "1", "On your phone, open Google Play, search for Z8 PC CONNECT, and install the app when it is published.");
        AddGuide(guide, "2", "Open this Windows app. Keep it running and note the PC address and six-digit pairing code above.");
        AddGuide(guide, "3", "Connect both devices to the same Wi-Fi (or use USB tethering), then enter the PC address and code in the phone app.");
        AddGuide(guide, "4", "Tap Pair and Connect. The live PC desktop will appear on your phone; rotate to landscape for the full-screen view.");
        page.Controls.Add(guide);

        var phone = MakeCard(980, 540);
        phone.Controls.Add(MakeHeading("PHONE SCREEN · IN-APP CONTROL"));
        phoneView.Location = new Point(28, 58);
        phoneView.MouseDown += (_, e) => { phoneDown = e.Location; phoneView.Focus(); };
        phoneView.MouseUp += (_, e) => { var a = MapPhonePoint(phoneDown); var b = MapPhonePoint(e.Location); if (Math.Abs(e.X-phoneDown.X)+Math.Abs(e.Y-phoneDown.Y) < 12) SendPhoneShell("shell","input","tap",a.X.ToString(),a.Y.ToString()); else SendPhoneShell("shell","input","swipe",a.X.ToString(),a.Y.ToString(),b.X.ToString(),b.Y.ToString(),"350"); };
        phoneView.MouseWheel += (_, e) => { var p = MapPhonePoint(new Point(phoneView.Width/2, phoneView.Height/2)); var endY = Math.Clamp(p.Y + (e.Delta > 0 ? 260 : -260), 0, phoneView.Image?.Height ?? 1000); SendPhoneShell("shell","input","swipe",p.X.ToString(),p.Y.ToString(),p.X.ToString(),endY.ToString(),"250"); };
        phoneView.KeyPress += (_, e) => { if (!char.IsControl(e.KeyChar)) SendPhoneTextToDevice(e.KeyChar.ToString()); };
        Style(sendPhoneText); sendPhoneText.Location = new Point(330, 390); sendPhoneText.Click += (_, _) => SendPhoneTextToDevice(phoneText.Text);
        phoneText.Location = new Point(330, 340); phoneText.ForeColor = Forest;
        var phoneInfo = new Label { Text = "Connect your Android phone by USB or Wi-Fi ADB. Click the live preview to tap, drag to swipe, use the mouse wheel to scroll, or type after focusing the preview. The separate cast button opens a full independent phone window.", Location = new Point(330, 70), Size = new Size(600, 110), ForeColor = Ink, Font = new Font("Segoe UI", 11) };
        Style(embedPhone); embedPhone.Location = new Point(330, 205);
        Style(cast); cast.Location = new Point(330, 265);
        embedPhone.Click += async (_, _) => await StartPhonePreview();
        cast.Click += (_, _) => LaunchPhoneCast();
        phone.Controls.AddRange([phoneView, phoneInfo, embedPhone, cast, phoneText, sendPhoneText]);
        page.Controls.Add(phone);

        var transfers = MakeCard(980, 155);
        transfers.Controls.Add(MakeHeading("PHONE → PC FILE TRANSFERS"));
        Style(openDownloads); openDownloads.Location = new Point(28, 105);
        openDownloads.Click += (_, _) => {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo("explorer.exe", folder) { UseShellExecute = true });
        };
        transfers.Controls.Add(transferSummary);
        transfers.Controls.Add(openDownloads);
        page.Controls.Add(transfers);

        var logCard = MakeCard(980, 170);
        logCard.Controls.Add(MakeHeading("LIVE CONNECTION ACTIVITY"));
        var logPanel = new Panel { Location = new Point(22, 54), Size = new Size(930, 94), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, BackColor = Deep, Padding = new Padding(10) };
        logPanel.Controls.Add(log); logCard.Controls.Add(logPanel);
        page.Controls.Add(logCard);
    }

    private CardPanel MakeCard(int width, int height) => new() { Width = width, Height = height, BackColor = Color.White, Padding = new Padding(24), Margin = new Padding(0, 0, 0, 18) };
    private Label MakeHeading(string text) => new() { Text = text, AutoSize = true, Location = new Point(26, 18), Font = new Font("Segoe UI", 13, FontStyle.Bold), ForeColor = Forest };
    private Label MakeSmall(string text, int x, int y) => new() { Text = text, AutoSize = true, Location = new Point(x, y), Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = Forest };
    private void AddGuide(Panel panel, string n, string text)
    {
        var y = 58 + (int.Parse(n) - 1) * 38;
        var badge = new Label { Text = n, Location = new Point(28, y), Size = new Size(24, 24), TextAlign = ContentAlignment.MiddleCenter, BackColor = Forest, ForeColor = Sand, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
        var label = new Label { Text = text, Location = new Point(66, y + 2), Size = new Size(880, 34), AutoSize = false, AutoEllipsis = false, ForeColor = Ink, Font = new Font("Segoe UI", 9.5f) };
        panel.Controls.AddRange([badge, label]);
    }
    private static void Style(Button b)
    {
        b.BackColor = Forest; b.ForeColor = Sand; b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderSize = 0;
        b.Font = new Font("Segoe UI", 9, FontStyle.Bold); b.Padding = new Padding(14, 8, 14, 8); b.Cursor = Cursors.Hand;
    }
    private void Append(string line)
    {
        if (log.IsDisposed) return;
        if (log.InvokeRequired) { log.BeginInvoke(new Action(() => Append(line))); return; }
        log.AppendText($"[{DateTime.Now:HH:mm:ss}] {line}{Environment.NewLine}");
    }
    private async Task StartServer()
    {
        if (server is { HasExited: false }) { await RefreshStatus(); return; }
        var dll = Path.Combine(AppContext.BaseDirectory, "server", "Z8PCConnect.Server.dll");
        if (!File.Exists(dll)) { Append("Server files missing: " + dll); state.Text = "SERVER NOT INSTALLED"; return; }
        try
        {
            var psi = new ProcessStartInfo("dotnet", "\"" + dll + "\"") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(dll)! };
            server = new Process { StartInfo = psi, EnableRaisingEvents = true };
            server.OutputDataReceived += (_, e) => { if (e.Data != null) { Append(e.Data); if (e.Data.StartsWith("PAIRING_CODE=")) BeginInvoke(new Action(() => code.Text = e.Data.Substring(13))); } };
            server.ErrorDataReceived += (_, e) => { if (e.Data != null) Append(e.Data); };
            server.Start(); server.BeginOutputReadLine(); server.BeginErrorReadLine();
            start.Enabled = false; stop.Enabled = true; state.Text = "STARTING CONNECTION…";
            await Task.Delay(700); await RefreshStatus();
        }
        catch (Exception ex) { Append(ex.Message); state.Text = "SERVER START FAILED"; }
    }
    private async Task RefreshStatus()
    {
        try
        {
            var status = await http.GetFromJsonAsync<ServerStatus>("http://127.0.0.1:8765/status");
            if (status == null) return;
            state.Text = "●  SERVER ONLINE  ·  READY FOR PHONE";
            pcName.Text = "PC NAME: " + status.PcName;
            ips.Text = string.Join("     ", status.Addresses);
            code.Text = status.PairingCode;
            await RefreshTransfers();
        }
        catch
        {
            if (server is { HasExited: true }) { state.Text = "SERVER STOPPED"; start.Enabled = true; stop.Enabled = false; }
        }
    }
    private async Task RefreshTransfers()
    {
        try
        {
            var files = await http.GetFromJsonAsync<RecentFile[]>("http://127.0.0.1:8765/files/recent");
            if (files == null || files.Length == 0)
                transferSummary.Text = "No files received yet. Phone uploads will be saved directly to the selected laptop folder.";
            else
                transferSummary.Text = string.Join("   •   ", files.TakeLast(3).Select(f => f.Name + "  →  " + f.Destination));
        }
        catch { }
    }

    private void StopServer()
    {
        try { if (server is { HasExited: false }) server.Kill(true); } catch { }
        server?.Dispose(); server = null; start.Enabled = true; stop.Enabled = false;
        state.Text = "●  CONNECTION STOPPED"; code.Text = "------"; Append("Server stopped.");
    }
    private string? FindScrcpy() =>
        new[] { Path.Combine(AppContext.BaseDirectory, "scrcpy", "scrcpy-win64-v5.0.1", "scrcpy.exe"), Path.Combine(AppContext.BaseDirectory, "scrcpy.exe"), "scrcpy.exe" }.FirstOrDefault(File.Exists);

    private string? FindAdb() =>
        new[] {
            Path.Combine(AppContext.BaseDirectory, "scrcpy", "scrcpy-win64-v5.0.1", "adb.exe"),
            Path.Combine(AppContext.BaseDirectory, "platform-tools", "adb.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Android", "Sdk", "platform-tools", "adb.exe")
        }.FirstOrDefault(File.Exists);

    private string FindPhoneSerial(string? ignoredFolder = null)
    {
        var adb = FindAdb();
        if (adb == null) return "";
        var folder = Path.GetDirectoryName(adb)!;
        using var probe = Process.Start(new ProcessStartInfo(adb, "devices") { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true, WorkingDirectory = folder });
        var output = probe?.StandardOutput.ReadToEnd() ?? "";
        probe?.WaitForExit(2500);
        var devices = output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .Where(parts => parts.Length >= 2 && parts[1] == "device")
            .Select(parts => parts[0]).ToArray();
        return devices.FirstOrDefault(value => !value!.Contains(":")) ?? devices.FirstOrDefault() ?? "";
    }

    private async Task LaunchEmbeddedPhone()
    {
        try
        {
            if (phoneMirror is { HasExited: false }) { ResizeEmbeddedPhone(); return; }
            var scrcpy = FindScrcpy();
            if (scrcpy == null) throw new FileNotFoundException("The bundled scrcpy engine is missing.");
            var folder = Path.GetDirectoryName(scrcpy)!;
            var serial = FindPhoneSerial(folder);
            if (string.IsNullOrWhiteSpace(serial)) throw new InvalidOperationException("No authorized Android phone was found. Connect it by USB or enable Wireless debugging first.");
            phoneMirror = Process.Start(new ProcessStartInfo(scrcpy, "-s \"" + serial + "\" --window-borderless --window-title \"Z8 Embedded Phone\"") {
                UseShellExecute = false, WorkingDirectory = folder, CreateNoWindow = true
            });
            if (phoneMirror == null) throw new InvalidOperationException("Could not start the phone screen engine.");
            for (var i = 0; i < 24 && phoneMirror.MainWindowHandle == IntPtr.Zero && !phoneMirror.HasExited; i++)
            {
                await Task.Delay(250);
                phoneMirror.Refresh();
            }
            var handle = phoneMirror.MainWindowHandle;
            if (handle == IntPtr.Zero) throw new InvalidOperationException("The phone screen window did not initialize. Use the separate cast button instead.");
            SetParent(handle, phoneView.Handle);
            var style = GetWindowLongPtr(handle, -16).ToInt64();
            style = (style & ~0x80000000L) | 0x40000000L | 0x10000000L;
            SetWindowLongPtr(handle, -16, new IntPtr(style));
            ResizeEmbeddedPhone();
            Append("Android phone screen embedded in the PC app.");
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "In-app Phone Control", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    private void ResizeEmbeddedPhone()
    {
        if (phoneMirror is not { HasExited: false }) return;
        phoneMirror.Refresh();
        var handle = phoneMirror.MainWindowHandle;
        if (handle != IntPtr.Zero) MoveWindow(handle, 0, 0, phoneView.ClientSize.Width, phoneView.ClientSize.Height, true);
    }

    private void StopEmbeddedPhone()
    {
        try { if (phoneMirror is { HasExited: false }) phoneMirror.Kill(true); } catch { }
        phoneMirror?.Dispose(); phoneMirror = null;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(IntPtr child, IntPtr newParent);
    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);
    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool MoveWindow(IntPtr hWnd, int x, int y, int width, int height, bool repaint);

    private void DebugPhone(string message) { try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "z8-phone-preview.log"), DateTime.Now.ToString("HH:mm:ss.fff") + " " + message + Environment.NewLine); } catch { } }

    private async Task StartPhonePreview()
    {
        DebugPhone("StartPhonePreview begin");
        var adb = FindAdb();
        if (adb == null) { MessageBox.Show("Android Debug Bridge (ADB) is missing. The Windows package should include it.", "Phone Control"); return; }
        phoneSerial = FindPhoneSerial();
        DebugPhone("serial=" + phoneSerial);
        if (string.IsNullOrWhiteSpace(phoneSerial))
        {
            MessageBox.Show("No authorized Android phone found. Connect by USB with USB debugging enabled, or connect ADB over Wi-Fi.", "Phone Control");
            return;
        }
        phoneRefresh.Start();
        DebugPhone("timer started");
        await RefreshPhoneFrame();
        DebugPhone("first frame complete");
        Append("In-app Android preview started for " + phoneSerial);
    }

    private async Task RefreshPhoneFrame()
    {
        if (phoneCaptureBusy || string.IsNullOrWhiteSpace(phoneSerial)) return;
        phoneCaptureBusy = true;
        DebugPhone("capture tick");
        try
        {
            var frame = await CapturePhoneFrame();
            DebugPhone("frame=" + (frame == null ? "null" : frame.Width + "x" + frame.Height));
            if (frame != null)
            {
                var old = phoneView.Image;
                phoneView.Image = frame;
                old?.Dispose();
            }
        }
        catch (Exception ex)
        {
            phoneRefresh.Stop();
            DebugPhone("capture error=" + ex);
            Append("Phone preview stopped: " + ex.Message);
        }
        finally { phoneCaptureBusy = false; }
    }

    private async Task<Bitmap?> CapturePhoneFrame()
    {
        var adb = FindAdb();
        if (adb == null) return null;
        var psi = new ProcessStartInfo(adb) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(adb)! };
        foreach (var arg in new[] { "-s", phoneSerial, "exec-out", "screencap", "-p" }) psi.ArgumentList.Add(arg);
        DebugPhone("starting adb screencap");
        using var proc = Process.Start(psi);
        if (proc == null) return null;
        using var ms = new MemoryStream();
        await proc.StandardOutput.BaseStream.CopyToAsync(ms);
        await proc.WaitForExitAsync();
        DebugPhone("adb exit=" + proc.ExitCode + " bytes=" + ms.Length);
        if (proc.ExitCode != 0 || ms.Length < 8) return null;
        ms.Position = 0;
        using var decoded = new Bitmap(ms);
        return new Bitmap(decoded);
    }

    private Point MapPhonePoint(Point point)
    {
        if (phoneView.Image == null) return Point.Empty;
        var image = phoneView.Image;
        var scale = Math.Min(phoneView.ClientSize.Width / (float)image.Width, phoneView.ClientSize.Height / (float)image.Height);
        var drawnWidth = image.Width * scale;
        var drawnHeight = image.Height * scale;
        var left = (phoneView.ClientSize.Width - drawnWidth) / 2f;
        var top = (phoneView.ClientSize.Height - drawnHeight) / 2f;
        var x = (int)Math.Clamp((point.X - left) / scale, 0, image.Width - 1);
        var y = (int)Math.Clamp((point.Y - top) / scale, 0, image.Height - 1);
        return new Point(x, y);
    }

    private void SendPhoneShell(params string[] args)
    {
        if (string.IsNullOrWhiteSpace(phoneSerial)) return;
        var adb = FindAdb();
        if (adb == null) return;
        var psi = new ProcessStartInfo(adb) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(adb)! };
        psi.ArgumentList.Add("-s"); psi.ArgumentList.Add(phoneSerial);
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        try { using var process = Process.Start(psi); process?.WaitForExit(1200); } catch { }
    }

    private void SendPhoneTextToDevice(string value)
    {
        if (string.IsNullOrEmpty(value)) return;
        var safe = value.Replace("%", "%25").Replace(" ", "%s").Replace("&", "\\&").Replace(";", "\\;");
        SendPhoneShell("shell", "input", "text", safe);
    }

    private void LaunchPhoneCast()
    {
        var scrcpy = new[] { Path.Combine(AppContext.BaseDirectory, "scrcpy", "scrcpy-win64-v5.0.1", "scrcpy.exe"), Path.Combine(AppContext.BaseDirectory, "scrcpy.exe"), "scrcpy.exe" }.FirstOrDefault(File.Exists);
        if (scrcpy == null) { MessageBox.Show("scrcpy is not installed. Install scrcpy and connect your Android phone by USB with USB debugging enabled, then try again.", "Phone Cast", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        try
        {
            var folder = Path.GetDirectoryName(scrcpy)!;
            var adb = Path.Combine(folder, "adb.exe");
            var serial = "";
            if (File.Exists(adb))
            {
                using var probe = Process.Start(new ProcessStartInfo(adb, "devices") { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true, WorkingDirectory = folder });
                var output = probe?.StandardOutput.ReadToEnd() ?? "";
                probe?.WaitForExit(2500);
                var devices = output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                    .Where(parts => parts.Length >= 2 && parts[1] == "device")
                    .Select(parts => parts[0]).ToArray();
                serial = devices.FirstOrDefault(value => !value!.Contains(":")) ?? devices.FirstOrDefault() ?? "";
            }
            var args = (string.IsNullOrWhiteSpace(serial) ? "" : "-s \"" + serial + "\" ") + "--window-title \"Z8 Phone Cast\" --always-on-top";
            Process.Start(new ProcessStartInfo(scrcpy, args) { UseShellExecute = true, WorkingDirectory = folder });
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Phone Cast", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
    private sealed class ServerStatus { public string PcName { get; set; } = ""; public string PairingCode { get; set; } = ""; public string[] Addresses { get; set; } = []; }
    private sealed class RecentFile { public string Name { get; set; } = ""; public string Path { get; set; } = ""; public string Destination { get; set; } = ""; public DateTimeOffset ReceivedAt { get; set; } }
}

internal sealed class CenterFlowPanel : FlowLayoutPanel
{
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        foreach (Control c in Controls)
            if (c.Visible && c.Width < ClientSize.Width - Padding.Horizontal)
                c.Left = Padding.Left + (ClientSize.Width - Padding.Horizontal - c.Width) / 2;
    }
}

internal sealed class CardPanel : Panel
{
    public CardPanel() { DoubleBuffered = true; SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new Rectangle(4, 4, Width - 10, Height - 10);
        using var shadow = new SolidBrush(Color.FromArgb(70, 0, 0, 0));
        using var pathShadow = Rounded(rect with { X = rect.X + 2, Y = rect.Y + 5 }, 18);
        e.Graphics.FillPath(shadow, pathShadow);
        using var white = new SolidBrush(BackColor);
        using var path = Rounded(rect, 18);
        e.Graphics.FillPath(white, path);
        using var border = new Pen(Color.FromArgb(220, 185, 231, 242), 2);
        e.Graphics.DrawPath(border, path);
        base.OnPaint(e);
    }
    private static GraphicsPath Rounded(Rectangle r, int radius)
    {
        var p = new GraphicsPath(); int d = radius * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p;
    }
}
