namespace BreakReminder;

internal enum PopupResult { Done, Snooze, TimedOut }

/// <summary>Small corner popup that never takes focus and is hidden from screen capture.</summary>
internal sealed class PopupForm : Form
{
    private static readonly Color Background = Color.FromArgb(32, 32, 36);
    private static readonly Color Foreground = Color.FromArgb(240, 240, 240);
    private static readonly Color Muted = Color.FromArgb(170, 170, 178);

    private readonly Color _accent;
    private readonly int _accentWidth;
    private readonly System.Windows.Forms.Timer _secondTimer = new() { Interval = 1000 };
    private readonly System.Windows.Forms.Timer _slideTimer = new() { Interval = 15 };
    private readonly Func<TimeSpan> _idle;
    private readonly Label? _countdown;
    private readonly int _countdownStart;
    private int _countdownLeft;
    private int _autoHideLeft;
    private int _targetTop;
    private bool _finished;

    public event Action<PopupForm, PopupResult>? Finished;

    public ReminderKind Kind { get; }

    public PopupForm(ReminderKind kind, string title, string body, int countdownSeconds,
        int autoHideSeconds, bool allowSnooze, Color accent, Func<TimeSpan> idle)
    {
        Kind = kind;
        _accent = accent;
        _idle = idle;
        _autoHideLeft = autoHideSeconds;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Background;
        Font = new Font("Segoe UI", 9.5f);

        int Scale(int v) => LogicalToDeviceUnits(v);
        _accentWidth = Scale(4);
        int pad = Scale(14);
        int width = Scale(320);
        int textLeft = _accentWidth + pad;
        int textWidth = width - textLeft - pad;
        int y = Scale(12);

        var titleLabel = new Label
        {
            Text = title,
            Font = new Font(Font, FontStyle.Bold),
            ForeColor = Foreground,
            AutoSize = false,
            Location = new Point(textLeft, y),
        };
        titleLabel.Size = new Size(textWidth, titleLabel.PreferredHeight);
        Controls.Add(titleLabel);
        y = titleLabel.Bottom + Scale(4);

        var bodySize = TextRenderer.MeasureText(body, Font, new Size(textWidth, int.MaxValue),
            TextFormatFlags.WordBreak);
        var bodyLabel = new Label
        {
            Text = body,
            ForeColor = Foreground,
            AutoSize = false,
            Location = new Point(textLeft, y),
            Size = new Size(textWidth, bodySize.Height + Scale(2)),
        };
        Controls.Add(bodyLabel);
        y = bodyLabel.Bottom + Scale(10);

        int buttonHeight = Scale(28);
        int x = width - pad;
        x = AddButton(allowSnooze ? "Snooze 5 min" : null, PopupResult.Snooze, x, y, buttonHeight, Scale);
        AddButton("Done", PopupResult.Done, x, y, buttonHeight, Scale, primary: true);

        if (countdownSeconds > 0)
        {
            _countdownStart = _countdownLeft = countdownSeconds;
            _countdown = new Label
            {
                ForeColor = Muted,
                AutoSize = true,
                Location = new Point(textLeft, y + Scale(5)),
            };
            Controls.Add(_countdown);
            UpdateCountdown();
        }

        ClientSize = new Size(width, y + buttonHeight + Scale(12));

        _secondTimer.Tick += OnSecond;
        _slideTimer.Tick += OnSlide;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= NativeMethods.WS_EX_TOPMOST | NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE;
            return cp;
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WM_MOUSEACTIVATE)
        {
            m.Result = NativeMethods.MA_NOACTIVATE;
            return;
        }
        base.WndProc(ref m);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        NativeMethods.SetWindowDisplayAffinity(Handle, NativeMethods.WDA_EXCLUDEFROMCAPTURE);
    }

    /// <summary>Shows the popup in the bottom-right corner of the screen under the cursor.</summary>
    public void ShowInCorner()
    {
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        int margin = LogicalToDeviceUnits(12);
        _targetTop = area.Bottom - Height - margin;
        Location = new Point(area.Right - Width - margin, area.Bottom);
        Show();
        _slideTimer.Start();
        _secondTimer.Start();
    }

    /// <summary>Closes without raising <see cref="Finished"/>.</summary>
    public void CloseSilently()
    {
        _finished = true;
        Close();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var accent = new SolidBrush(_accent);
        e.Graphics.FillRectangle(accent, 0, 0, _accentWidth, ClientSize.Height);
        using var border = new Pen(Color.FromArgb(70, 70, 78));
        e.Graphics.DrawRectangle(border, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _secondTimer.Dispose();
            _slideTimer.Dispose();
        }
        base.Dispose(disposing);
    }

    private int AddButton(string? text, PopupResult result, int right, int top, int height,
        Func<int, int> scale, bool primary = false)
    {
        if (text == null) return right;
        var button = new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            ForeColor = primary ? Color.Black : Foreground,
            BackColor = primary ? _accent : Color.FromArgb(56, 56, 62),
            Height = height,
            TabStop = false,
            Cursor = Cursors.Hand,
        };
        button.FlatAppearance.BorderSize = 0;
        button.Width = TextRenderer.MeasureText(text, Font).Width + scale(24);
        button.Location = new Point(right - button.Width, top);
        button.Click += (_, _) => Finish(result);
        Controls.Add(button);
        return button.Left - scale(8);
    }

    private void OnSlide(object? sender, EventArgs e)
    {
        int step = Math.Max(1, (Top - _targetTop) / 3);
        Top = Math.Max(_targetTop, Top - step);
        if (Top == _targetTop) _slideTimer.Stop();
    }

    private void OnSecond(object? sender, EventArgs e)
    {
        if (_countdown != null && _countdownLeft > 0)
        {
            _countdownLeft--;
            UpdateCountdown();
            // No input during the countdown means the user looked away; count it as done.
            if (_countdownLeft == 0 && _idle() >= TimeSpan.FromSeconds(_countdownStart - 2))
            {
                Finish(PopupResult.Done);
                return;
            }
        }
        if (--_autoHideLeft <= 0) Finish(PopupResult.TimedOut);
    }

    private void UpdateCountdown() =>
        _countdown!.Text = _countdownLeft > 0 ? $"{_countdownLeft} s" : "Time's up";

    private void Finish(PopupResult result)
    {
        if (_finished) return;
        _finished = true;
        Finished?.Invoke(this, result);
        Close();
    }
}
