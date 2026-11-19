using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Drawing;

namespace Z8PCConnect.Desktop;

public sealed class MainForm : Form
{
    private readonly Color forest = Color.FromArgb(23, 76, 60);
    private readonly Color forestDeep = Color.FromArgb(16, 56, 46);
    private readonly Color sand = Color.FromArgb(240, 237, 228);
    private readonly Color ice = Color.FromArgb(185, 231, 242);
    private readonly Label state = new() { AutoSize = true, Font = new Font("Segoe UI", 11, FontStyle.Bold) };
    private readonly Label code = new() { AutoSize = true, Font = new Font("Segoe UI", 30, FontStyle.Bold) };
    private readonly Label ips = new() { AutoSize = true, Font = new Font("Segoe UI", 12, FontStyle.Bold) };
    private readonly TextBox log = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = Color.FromArgb(16, 56, 46), ForeColor = Color.FromArgb(240, 237, 228), BorderStyle = BorderStyle.None, Dock = DockStyle.Fill, Font = new Font("Consolas", 9) };
    private readonly Button start = new() { Text = "START PC CONNECTION", AutoSize = true };
    private readonly Button stop = new() { Text = "STOP", AutoSize = true, Enabled = false };
    private readonly Panel card = new() { Size = new Size(760, 650), BackColor = Color.FromArgb(240, 237, 228) };
    private Process? server;

    public MainForm()
    {
        Text = "Z8 PC CONNECT";
        Width = 1040; Height = 820; MinimumSize = new Size(820, 700);
        BackColor = ice; ForeColor = forest;
        StartPosition = FormStartPosition.CenterScreen;
        BuildCard();
        Controls.Add(card);
        Resize += (_, _) => CenterCard();
        Shown += (_, _) => { CenterCard(); StartServer(); };
        FormClosing += (_, _) => StopServer();
    }

    private void BuildCard()
    {
        card.Padding = new Padding(34);
        card.Anchor = AnchorStyles.None;
        var header = new Label { Text = "Z8 PC CONNECT", Font = new Font("Segoe UI", 27, FontStyle.Bold), ForeColor = forest, AutoSize = true, Location = new Point(34, 24) };
        var subtitle = new Label { Text = "Your laptop, connected securely to your phone", Font = new Font("Segoe UI", 11), ForeColor = forest, AutoSize = true, Location = new Point(38, 70) };
        state.Location = new Point(38, 112); state.ForeColor = forest; state.Text = "Starting connection service…";
        var ipTitle = MakeLabel("PC ADDRESS  ·  PORT 8765", 38, 158, 9);
        ips.Location = new Point(38, 184); ips.ForeColor = forest; ips.Text = string.Join("     ", GetLocalIps());
        var codeTitle = MakeLabel("ONE-TIME PAIRING CODE", 38, 235, 9);
        code.Location = new Point(38, 256); code.ForeColor = forest; code.Text = "------";
        StyleButton(start); StyleButton(stop);
        start.Location = new Point(38, 326); stop.Location = new Point(270, 326);
        start.Click += (_, _) => StartServer(); stop.Click += (_, _) => StopServer();
        var help = new Label {
            Text = "On your phone, enter the PC address and the six-digit code above.\nKeep this app open and connect both devices to the same trusted Wi-Fi.",
            AutoSize = true, Font = new Font("Segoe UI", 10), ForeColor = forest, Location = new Point(38, 382)
        };
        var logPanel = new Panel { Location = new Point(38, 448), Size = new Size(680, 165), Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom, BackColor = forestDeep, Padding = new Padding(12) };
        logPanel.Controls.Add(log);
        card.Controls.AddRange([header, subtitle, state, ipTitle, ips, codeTitle, code, start, stop, help, logPanel]);
    }

    private Label MakeLabel(string text, int x, int y, float size) =>
        new() { Text = text, AutoSize = true, Location = new Point(x, y), Font = new Font("Segoe UI", size, FontStyle.Bold), ForeColor = forest };

    private void StyleButton(Button button)
    {
        button.BackColor = forest; button.ForeColor = sand; button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0; button.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        button.Padding = new Padding(16, 9, 16, 9); button.Cursor = Cursors.Hand;
    }

    private void CenterCard()
    {
        card.Left = Math.Max(12, (ClientSize.Width - card.Width) / 2);
        card.Top = Math.Max(12, (ClientSize.Height - card.Height) / 2);
    }

    private static string[] GetLocalIps() =>
        Dns.GetHostEntry(Dns.GetHostName()).AddressList
            .Where(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a))
            .Select(a => a.ToString()).ToArray();

    private void StartServer()
    {
        if (server is { HasExited: false }) return;
        var dll = Path.Combine(AppContext.BaseDirectory, "server", "Z8PCConnect.Server.dll");
        if (!File.Exists(dll)) { Append("Server files missing: " + dll); state.Text = "Server not installed"; return; }
        var psi = new ProcessStartInfo("dotnet", "\"" + dll + "\"") {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
            CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(dll)!
        };
        server = new Process { StartInfo = psi, EnableRaisingEvents = true };
        server.OutputDataReceived += (_, e) => { if (e.Data != null && !IsDisposed) BeginInvoke(new Action(() => HandleServerLine(e.Data))); };
        server.ErrorDataReceived += (_, e) => { if (e.Data != null && !IsDisposed) BeginInvoke(new Action(() => Append(e.Data))); };
        try {
            server.Start(); server.BeginOutputReadLine(); server.BeginErrorReadLine();
            state.Text = "Starting PC connection…"; start.Enabled = false; stop.Enabled = true;
        } catch (Exception ex) { Append(ex.Message); state.Text = "Could not start"; }
    }

    private void HandleServerLine(string line)
    {
        if (line.StartsWith("PAIRING_CODE=")) { code.Text = line.Substring(13); state.Text = "READY · waiting for your phone"; }
        else if (line.StartsWith("PC_IP=")) { var ip = line.Substring(6); if (!ips.Text.Contains(ip)) ips.Text += "     " + ip; }
        else if (line == "Z8_READY") state.Text = "SERVER ONLINE";
        Append(line);
    }

    private void Append(string line) { if (!log.IsDisposed) log.AppendText(line + Environment.NewLine); }

    private void StopServer()
    {
        try { if (server is { HasExited: false }) server.Kill(true); } catch { }
        server?.Dispose(); server = null; start.Enabled = true; stop.Enabled = false;
        state.Text = "STOPPED"; code.Text = "------";
    }
}
