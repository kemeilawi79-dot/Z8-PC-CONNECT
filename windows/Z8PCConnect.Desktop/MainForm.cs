using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Drawing;

namespace Z8PCConnect.Desktop;

public sealed class MainForm : Form
{
    private readonly Label state = new() { AutoSize = true, ForeColor = Color.FromArgb(0, 110, 100), Font = new Font("Segoe UI", 11, FontStyle.Bold) };
    private readonly Label code = new() { AutoSize = true, Font = new Font("Segoe UI", 28, FontStyle.Bold) };
    private readonly Label ips = new() { AutoSize = true, Font = new Font("Segoe UI", 12) };
    private readonly TextBox log = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, BackColor = Color.FromArgb(12, 20, 32), ForeColor = Color.Gainsboro };
    private readonly Button start = new() { Text = "Start PC connection", AutoSize = true };
    private readonly Button stop = new() { Text = "Stop", AutoSize = true, Enabled = false };
    private Process? server;

    public MainForm()
    {
        Text = "Z8 PC CONNECT"; Width = 760; Height = 560; MinimumSize = new Size(640, 460);
        BackColor = Color.FromArgb(240, 237, 228); ForeColor = Color.FromArgb(0, 71, 65);
        var header = new Label { Text = "Z8 PC CONNECT", Font = new Font("Segoe UI", 24, FontStyle.Bold), AutoSize = true, Location = new Point(24, 18) };
        var subtitle = new Label { Text = "Connect your phone to this PC", AutoSize = true, Location = new Point(28, 66) };
        state.Location = new Point(28, 106); state.Text = "Starting…";
        var ipTitle = new Label { Text = "PC ADDRESS · PORT 8765", AutoSize = true, Location = new Point(28, 150), Font = new Font("Segoe UI", 9, FontStyle.Bold) };
        ips.Location = new Point(28, 174); ips.Text = string.Join("     ", GetLocalIps());
        var codeTitle = new Label { Text = "ONE-TIME PAIRING CODE", AutoSize = true, Location = new Point(28, 222), Font = new Font("Segoe UI", 9, FontStyle.Bold) };
        code.Location = new Point(28, 245); code.Text = "------";
        start.Location = new Point(28, 310); stop.Location = new Point(220, 310);
        start.Click += (_,_) => StartServer(); stop.Click += (_,_) => StopServer();
        var help = new Label { Text = "On your phone, enter a PC address and the current code, then tap Pair and connect.\nUse the same trusted Wi-Fi, or USB tethering for a cable-based network connection.", AutoSize = true, Location = new Point(28, 360) };
        var panel = new Panel { Location = new Point(24, 420), Size = new Size(690, 90), Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom };
        panel.Controls.Add(log);
        Controls.AddRange([header, subtitle, state, ipTitle, ips, codeTitle, code, start, stop, help, panel]);
        FormClosing += (_,_) => StopServer(); Shown += (_,_) => StartServer();
    }
    private static string[] GetLocalIps() => Dns.GetHostEntry(Dns.GetHostName()).AddressList.Where(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a)).Select(a => a.ToString()).ToArray();
    private void StartServer()
    {
        if (server is { HasExited: false }) return;
        var dll = Path.Combine(AppContext.BaseDirectory, "server", "Z8PCConnect.Server.dll");
        if (!File.Exists(dll)) { Append("Server files missing: " + dll); state.Text = "Server not installed"; return; }
        var psi = new ProcessStartInfo("dotnet", "\"" + dll + "\"") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(dll)! };
        server = new Process { StartInfo = psi, EnableRaisingEvents = true };
        server.OutputDataReceived += (_,e) => { if (e.Data != null && !IsDisposed) BeginInvoke(new Action(() => HandleServerLine(e.Data))); };
        server.ErrorDataReceived += (_,e) => { if (e.Data != null && !IsDisposed) BeginInvoke(new Action(() => Append(e.Data))); };
        try { server.Start(); server.BeginOutputReadLine(); server.BeginErrorReadLine(); state.Text = "Starting PC connection…"; start.Enabled = false; stop.Enabled = true; }
        catch (Exception ex) { Append(ex.Message); state.Text = "Could not start"; }
    }
    private void HandleServerLine(string line)
    {
        if (line.StartsWith("PAIRING_CODE=")) { code.Text = line.Substring(13); state.Text = "Ready for phone connection"; }
        else if (line.StartsWith("PC_IP=")) { var ip = line.Substring(6); if (!ips.Text.Contains(ip)) ips.Text += "     " + ip; }
        else if (line == "Z8_READY") state.Text = "Server online";
        Append(line);
    }
    private void Append(string line) { if (!log.IsDisposed) log.AppendText(line + Environment.NewLine); }
    private void StopServer()
    {
        try { if (server is { HasExited: false }) server.Kill(true); } catch { }
        server?.Dispose(); server = null; start.Enabled = true; stop.Enabled = false; state.Text = "Stopped"; code.Text = "------";
    }
}
