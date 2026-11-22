namespace HddScan.UI;

/// <summary>Horizontal bar: scanned (blue) · not found by scan (orange) · free (grey).</summary>
public sealed class DriveBar : Control
{
    public long Total { get; set; }
    public long Free { get; set; }
    /// <summary>Bytes found by the scan, or -1 if not scanned / not a whole drive.</summary>
    public long Scanned { get; set; } = -1;

    public static readonly Color ScannedColor = Color.FromArgb(66, 133, 244);
    public static readonly Color UnknownColor = Color.FromArgb(255, 152, 0);
    public static readonly Color UsedColor = Color.FromArgb(120, 144, 156);
    public static readonly Color FreeColor = Color.FromArgb(225, 228, 232);

    public DriveBar()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var r = new Rectangle(0, 3, Width - 1, Height - 7);
        using (var free = new SolidBrush(FreeColor)) g.FillRectangle(free, r);
        if (Total > 0)
        {
            long used = Math.Max(0, Total - Free);
            if (Scanned >= 0)
            {
                long sc = Math.Min(Scanned, used);
                int w1 = (int)(r.Width * (double)sc / Total);
                int w2 = (int)(r.Width * (double)(used - sc) / Total);
                using var b1 = new SolidBrush(ScannedColor);
                using var b2 = new SolidBrush(UnknownColor);
                g.FillRectangle(b1, r.X, r.Y, w1, r.Height);
                g.FillRectangle(b2, r.X + w1, r.Y, w2, r.Height);
            }
            else
            {
                int w = (int)(r.Width * (double)used / Total);
                using var b = new SolidBrush(UsedColor);
                g.FillRectangle(b, r.X, r.Y, w, r.Height);
            }
        }
        using var pen = new Pen(Color.FromArgb(180, 185, 190));
        g.DrawRectangle(pen, r);
    }
}
