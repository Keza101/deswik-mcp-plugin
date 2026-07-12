using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace Deswik.Addin;

/// <summary>
/// Dock-panel UI for the MCP bridge plugin. Deswik's plugin host calls
/// DeswikMcpAddin.RegisterMainControl() and docks whatever control it
/// returns — this one shows live bridge status, traffic and a log tail.
/// </summary>
internal class McpStatusControl : UserControl
{
    private readonly DeswikMcpAddin _addin;
    private readonly Label _lblStatus;
    private readonly Label _lblStats;
    private readonly ListBox _lstLog;
    private readonly System.Windows.Forms.Timer _timer;

    private const int MaxLogLines = 300;

    public McpStatusControl(DeswikMcpAddin addin)
    {
        _addin = addin;
        Dock = DockStyle.Fill;
        MinimumSize = new Size(280, 200);
        Padding = new Padding(6);

        var title = new Label
        {
            Text = "Deswik MCP Bridge",
            Font = new Font(Font.FontFamily, 10.5f, FontStyle.Bold),
            Dock = DockStyle.Top,
            Height = 26
        };

        _lblStatus = new Label
        {
            Text = "● waiting...",
            ForeColor = Color.Gray,
            Font = new Font(Font.FontFamily, 9.5f, FontStyle.Bold),
            Dock = DockStyle.Top,
            Height = 22
        };

        _lblStats = new Label
        {
            Text = "bridge 127.0.0.1:9595",
            Dock = DockStyle.Top,
            Height = 34,
            AutoEllipsis = true
        };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 34,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 4, 0, 4)
        };

        var btnReconnect = new Button { Text = "Reconnect", AutoSize = true };
        btnReconnect.Click += (_, _) => _addin.RequestReconnect();

        var btnLog = new Button { Text = "Open log file", AutoSize = true };
        btnLog.Click += (_, _) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo(_addin.LogFilePath)
                { UseShellExecute = true });
            }
            catch { }
        };

        var btnClear = new Button { Text = "Clear", AutoSize = true };
        btnClear.Click += (_, _) => _lstLog.Items.Clear();

        buttons.Controls.Add(btnReconnect);
        buttons.Controls.Add(btnLog);
        buttons.Controls.Add(btnClear);

        _lstLog = new ListBox
        {
            Dock = DockStyle.Fill,
            IntegralHeight = false,
            HorizontalScrollbar = true,
            Font = new Font("Consolas", 8.25f)
        };

        Controls.Add(_lstLog);
        Controls.Add(buttons);
        Controls.Add(_lblStats);
        Controls.Add(_lblStatus);
        Controls.Add(title);

        // poll connection/traffic state once a second
        _timer = new System.Windows.Forms.Timer { Interval = 1000 };
        _timer.Tick += (_, _) => RefreshStatus();
        _timer.Start();
        RefreshStatus();
    }

    private void RefreshStatus()
    {
        var connected = _addin.IsBridgeConnected;
        _lblStatus.Text = connected ? "● connected" : "● disconnected";
        _lblStatus.ForeColor = connected ? Color.ForestGreen : Color.Firebrick;
        _lblStats.Text =
            $"bridge 127.0.0.1:9595   commands: {_addin.CommandCount}\n" +
            $"last: {_addin.LastCommand}";
    }

    /// <summary>Thread-safe log append (bridge events fire off the UI thread).</summary>
    public void AppendLog(string line)
    {
        if (IsDisposed || !IsHandleCreated) return;
        try
        {
            BeginInvoke(() =>
            {
                _lstLog.Items.Add($"{DateTime.Now:HH:mm:ss} {line}");
                while (_lstLog.Items.Count > MaxLogLines)
                    _lstLog.Items.RemoveAt(0);
                _lstLog.TopIndex = _lstLog.Items.Count - 1;
            });
        }
        catch
        {
            // never let UI logging take the host down
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _timer.Dispose();
        base.Dispose(disposing);
    }
}
