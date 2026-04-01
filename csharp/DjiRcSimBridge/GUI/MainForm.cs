using System.Reflection;
using DjiRcSimBridge.Bridge;
using DjiRcSimBridge.Config;
using DjiRcSimBridge.Gamepad;
using DjiRcSimBridge.Protocol;
using DjiRcSimBridge.Serial;

namespace DjiRcSimBridge.GUI;

public sealed class MainForm : Form
{
    private readonly string? _portArg;
    private readonly NotifyIcon _trayIcon;
    private readonly System.Windows.Forms.Timer _uiTimer;

    private BridgeRunner? _bridge;

    // Status
    private Label _lblSerialStatus = null!;
    private Label _lblGamepadStatus = null!;
    private Label _lblModeStyle = null!;

    // Sticks
    private ProgressBar _barLH = null!, _barLV = null!, _barRH = null!, _barRV = null!;
    private Label _lblLH = null!, _lblLV = null!, _lblRH = null!, _lblRV = null!;
    private ProgressBar _barLT = null!, _barRT = null!;
    private Label _lblLT = null!, _lblRT = null!;

    // Buttons
    private Label _lblShoot = null!, _lblFn = null!, _lblSwap = null!, _lblRth = null!, _lblMode = null!;

    // Mode selector
    private ComboBox _cmbModeStyle = null!;
    private Button _btnReconnect = null!;

    public MainForm(string? portArg)
    {
        _portArg = portArg;

        // Load icon from embedded resource
        var icon = LoadEmbeddedIcon();
        Icon = icon;

        // Tray icon
        _trayIcon = new NotifyIcon
        {
            Icon = icon,
            Text = "DJI RC-N3 Simulator Bridge",
            Visible = true,
        };
        _trayIcon.DoubleClick += (_, _) => RestoreFromTray();
        _trayIcon.ContextMenuStrip = BuildTrayMenu();

        // UI refresh timer (30 Hz)
        _uiTimer = new System.Windows.Forms.Timer { Interval = 33 };
        _uiTimer.Tick += OnUiTimerTick;

        BuildLayout();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        ConnectAndStart();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            _trayIcon.ShowBalloonTip(2000, "DJI RC-N3 Bridge",
                "Running in background. Double-click to restore.", ToolTipIcon.Info);
            return;
        }

        _uiTimer.Stop();
        _bridge?.Dispose();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        base.OnFormClosing(e);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (WindowState == FormWindowState.Minimized)
            Hide();
    }

    private void ConnectAndStart()
    {
        _bridge?.Dispose();
        _bridge = null;
        _uiTimer.Stop();

        _lblSerialStatus.Text = "Scanning serial ports...";
        _lblSerialStatus.ForeColor = Color.Orange;
        _lblGamepadStatus.Text = "Initializing...";
        _lblGamepadStatus.ForeColor = Color.Orange;

        var config = AppConfig.Load();
        var modeStyle = (ModeStyle)_cmbModeStyle.SelectedIndex;
        config.ModeStyle = modeStyle;
        config.Save();
        _lblModeStyle.Text = $"Mode: {modeStyle}";

        try
        {
            var serialPort = SerialPortDetector.DetectWithDescription(_portArg);
            var conn = new DumlConnection(serialPort);

            _bridge = new BridgeRunner(conn, modeStyle);
            Thread.Sleep(500); // let ViGEm settle

            _bridge.Start();

            _lblSerialStatus.Text = $"Connected to {conn.PortName}";
            _lblSerialStatus.ForeColor = Color.LimeGreen;
            _lblGamepadStatus.Text = "Xbox 360 Controller Active";
            _lblGamepadStatus.ForeColor = Color.LimeGreen;

            _uiTimer.Start();
        }
        catch (Exception ex)
        {
            _lblSerialStatus.Text = $"Error: {ex.Message}";
            _lblSerialStatus.ForeColor = Color.Red;
            _lblGamepadStatus.Text = "Not connected";
            _lblGamepadStatus.ForeColor = Color.Red;
        }
    }

    private void OnUiTimerTick(object? sender, EventArgs e)
    {
        if (_bridge is null) return;

        var (sticks, buttons) = _bridge.CurrentState;

        // Map int16 (-32768..32767) to progress bar (0..100)
        SetStickBar(_barLH, _lblLH, sticks.LeftHorizontal, "LH");
        SetStickBar(_barLV, _lblLV, sticks.LeftVertical, "LV");
        SetStickBar(_barRH, _lblRH, sticks.RightHorizontal, "RH");
        SetStickBar(_barRV, _lblRV, sticks.RightVertical, "RV");

        // Triggers (0..255)
        _barLT.Value = sticks.GimbalLeftTrigger * 100 / 255;
        _lblLT.Text = $"LT: {sticks.GimbalLeftTrigger}";
        _barRT.Value = sticks.GimbalRightTrigger * 100 / 255;
        _lblRT.Text = $"RT: {sticks.GimbalRightTrigger}";

        // Buttons
        SetButtonLabel(_lblShoot, "Shoot", buttons.CameraShoot);
        SetButtonLabel(_lblFn, "FN", buttons.Fn);
        SetButtonLabel(_lblSwap, "Swap", buttons.CameraSwap);
        SetButtonLabel(_lblRth, "RTH", buttons.Rth);
        _lblMode.Text = $"Mode: {buttons.Mode}";
        _lblMode.ForeColor = buttons.Mode switch
        {
            FlightMode.Cine => Color.DodgerBlue,
            FlightMode.Normal => Color.LimeGreen,
            FlightMode.Sport => Color.OrangeRed,
            _ => Color.White,
        };
    }

    private static void SetStickBar(ProgressBar bar, Label lbl, short value, string name)
    {
        var pct = (value + 32768) * 100 / 65535;
        bar.Value = Math.Clamp(pct, 0, 100);
        lbl.Text = $"{name}: {value,+6}";
    }

    private static void SetButtonLabel(Label lbl, string name, bool pressed)
    {
        lbl.Text = $"{name}: {(pressed ? "ON" : "off")}";
        lbl.ForeColor = pressed ? Color.LimeGreen : Color.Gray;
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private ContextMenuStrip BuildTrayMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Show", null, (_, _) => RestoreFromTray());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) =>
        {
            _trayIcon.Visible = false;
            Application.Exit();
        });
        return menu;
    }

    private void BuildLayout()
    {
        Text = "DJI RC-N3 Simulator Bridge";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(480, 520);
        BackColor = Color.FromArgb(30, 30, 40);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9);

        var y = 12;

        // ── Title ──
        var title = MakeLabel("DJI RC-N3 Simulator Bridge", 12, y, 460, 28);
        title.Font = new Font("Segoe UI", 14, FontStyle.Bold);
        title.ForeColor = Color.DodgerBlue;
        title.TextAlign = ContentAlignment.MiddleCenter;
        Controls.Add(title);
        y += 36;

        // ── Status ──
        _lblSerialStatus = MakeLabel("Not connected", 12, y, 460, 20);
        Controls.Add(_lblSerialStatus);
        y += 22;
        _lblGamepadStatus = MakeLabel("Initializing...", 12, y, 300, 20);
        Controls.Add(_lblGamepadStatus);
        _lblModeStyle = MakeLabel("Mode: Pulse", 320, y, 140, 20);
        _lblModeStyle.TextAlign = ContentAlignment.MiddleRight;
        Controls.Add(_lblModeStyle);
        y += 30;

        // ── Separator ──
        var sep1 = new Label { Location = new Point(12, y), Size = new Size(456, 1), BackColor = Color.FromArgb(60, 60, 80) };
        Controls.Add(sep1);
        y += 8;

        // ── Left Stick ──
        Controls.Add(MakeSectionLabel("Left Stick", 12, y));
        y += 20;
        (_barLH, _lblLH) = MakeStickRow("LH", 12, y); y += 28;
        (_barLV, _lblLV) = MakeStickRow("LV", 12, y); y += 28;

        // ── Right Stick ──
        Controls.Add(MakeSectionLabel("Right Stick", 12, y));
        y += 20;
        (_barRH, _lblRH) = MakeStickRow("RH", 12, y); y += 28;
        (_barRV, _lblRV) = MakeStickRow("RV", 12, y); y += 28;

        // ── Triggers ──
        Controls.Add(MakeSectionLabel("Gimbal Triggers", 12, y));
        y += 20;
        (_barLT, _lblLT) = MakeStickRow("LT", 12, y); y += 28;
        (_barRT, _lblRT) = MakeStickRow("RT", 12, y); y += 28;

        // ── Separator ──
        var sep2 = new Label { Location = new Point(12, y), Size = new Size(456, 1), BackColor = Color.FromArgb(60, 60, 80) };
        Controls.Add(sep2);
        y += 8;

        // ── Buttons ──
        Controls.Add(MakeSectionLabel("Buttons", 12, y));
        y += 22;
        _lblShoot = MakeLabel("Shoot: off", 12, y, 100, 20); _lblShoot.ForeColor = Color.Gray; Controls.Add(_lblShoot);
        _lblFn = MakeLabel("FN: off", 120, y, 80, 20); _lblFn.ForeColor = Color.Gray; Controls.Add(_lblFn);
        _lblSwap = MakeLabel("Swap: off", 210, y, 100, 20); _lblSwap.ForeColor = Color.Gray; Controls.Add(_lblSwap);
        _lblRth = MakeLabel("RTH: off", 320, y, 80, 20); _lblRth.ForeColor = Color.Gray; Controls.Add(_lblRth);
        y += 24;

        _lblMode = MakeLabel("Mode: Normal", 12, y, 200, 20);
        _lblMode.ForeColor = Color.LimeGreen;
        _lblMode.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        Controls.Add(_lblMode);
        y += 34;

        // ── Separator ──
        var sep3 = new Label { Location = new Point(12, y), Size = new Size(456, 1), BackColor = Color.FromArgb(60, 60, 80) };
        Controls.Add(sep3);
        y += 8;

        // ── Mode Style Selector ──
        Controls.Add(MakeSectionLabel("Mode Switch Style", 12, y));
        y += 22;

        _cmbModeStyle = new ComboBox
        {
            Location = new Point(12, y),
            Size = new Size(250, 26),
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(45, 45, 60),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
        };
        _cmbModeStyle.Items.AddRange(["Pulse (D-pad per mode)", "Single (D-pad Down)", "Hold (D-pad held)"]);
        var config = AppConfig.Load();
        _cmbModeStyle.SelectedIndex = (int)config.ModeStyle;
        Controls.Add(_cmbModeStyle);

        _btnReconnect = new Button
        {
            Text = "Reconnect",
            Location = new Point(280, y - 2),
            Size = new Size(100, 28),
            BackColor = Color.FromArgb(50, 50, 70),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
        };
        _btnReconnect.FlatAppearance.BorderColor = Color.DodgerBlue;
        _btnReconnect.Click += (_, _) => ConnectAndStart();
        Controls.Add(_btnReconnect);

        y += 36;

        // ── Footer ──
        var footer = MakeLabel("Minimize or close to system tray", 12, y, 460, 18);
        footer.ForeColor = Color.FromArgb(100, 100, 120);
        footer.TextAlign = ContentAlignment.MiddleCenter;
        footer.Font = new Font("Segoe UI", 8);
        Controls.Add(footer);

        ClientSize = new Size(480, y + 28);
    }

    private (ProgressBar bar, Label lbl) MakeStickRow(string name, int x, int y)
    {
        var lbl = MakeLabel($"{name}:      0", x, y, 90, 20);
        lbl.Font = new Font("Consolas", 9);
        Controls.Add(lbl);

        var bar = new ProgressBar
        {
            Location = new Point(x + 95, y + 2),
            Size = new Size(360, 16),
            Minimum = 0,
            Maximum = 100,
            Value = 50,
            Style = ProgressBarStyle.Continuous,
        };
        Controls.Add(bar);
        return (bar, lbl);
    }

    private Label MakeSectionLabel(string text, int x, int y)
    {
        var lbl = MakeLabel(text, x, y, 200, 18);
        lbl.Font = new Font("Segoe UI", 9, FontStyle.Bold);
        lbl.ForeColor = Color.DodgerBlue;
        return lbl;
    }

    private static Label MakeLabel(string text, int x, int y, int w, int h)
    {
        return new Label
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(w, h),
            ForeColor = Color.White,
            AutoSize = false,
        };
    }

    private static Icon LoadEmbeddedIcon()
    {
        var asm = Assembly.GetExecutingAssembly();
        var resName = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("app.ico", StringComparison.OrdinalIgnoreCase));

        if (resName is not null)
        {
            using var stream = asm.GetManifestResourceStream(resName);
            if (stream is not null)
                return new Icon(stream);
        }

        return SystemIcons.Application;
    }
}
