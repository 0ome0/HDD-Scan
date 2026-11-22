using HddScan.Core;

namespace HddScan.UI;

/// <summary>Squarified treemap of a folder (2 levels deep). Left-click drills in, right-click goes up.</summary>
public sealed class TreemapView : Control
{
    public event Action<DirNode>? NodeActivated;
    public Func<DirNode, Color>? ColorOf;

    sealed record CellBox(RectangleF Rect, DirNode? Node, string Label, long Size, int Level, Color Color);

    readonly List<CellBox> _cells = new();
    readonly ToolTip _tip = new() { InitialDelay = 300, ReshowDelay = 100 };
    DirNode? _root;
    bool _dirty = true;
    int _hover = -1;

    public DirNode? Root
    {
        get => _root;
        set { _root = value; _dirty = true; Invalidate(); }
    }

    public TreemapView()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        BackColor = Color.White;
    }

    protected override void OnResize(EventArgs e) { base.OnResize(e); _dirty = true; }

    public void Refresh2() { _dirty = true; Invalidate(); }

    void Build()
    {
        _cells.Clear();
        _dirty = false;
        if (_root == null || Width < 10 || Height < 10) return;
        LayoutLevel(_root, new RectangleF(1, 1, Width - 3, Height - 3), 0);
    }

    void LayoutLevel(DirNode n, RectangleF rect, int level)
    {
        var items = new List<(DirNode? Node, string Label, long Size)>();
        const int MaxItems = 250;
        long rest = 0;
        int restCount = 0;
        foreach (var c in n.Children)
        {
            if (c.TotalSize <= 0) continue;
            if (items.Count < MaxItems) items.Add((c, c.Name, c.TotalSize));
            else { rest += c.TotalSize; restCount++; }
        }
        if (n.OwnSize > 0) items.Add((null, $"[ไฟล์ใน {n.Name}]", n.OwnSize));
        if (rest > 0) items.Add((null, $"[อีก {restCount} โฟลเดอร์]", rest));
        items.Sort((a, b) => b.Size.CompareTo(a.Size));
        if (items.Count == 0) return;

        double total = items.Sum(i => (double)i.Size);
        double area = rect.Width * rect.Height;
        if (total <= 0 || area <= 0) return;
        var sizes = items.Select(i => i.Size / total * area).ToArray();
        var rects = new RectangleF[sizes.Length];
        Squarify(sizes, rect, rects);

        for (int i = 0; i < items.Count; i++)
        {
            var r = rects[i];
            if (r.Width < 1 || r.Height < 1) continue;
            var it = items[i];
            var col = it.Node != null && ColorOf != null ? ColorOf(it.Node) : Color.FromArgb(176, 184, 192);
            if (level == 1) col = Blend(col, Color.White, 0.35f);
            _cells.Add(new CellBox(r, it.Node, it.Label, it.Size, level, col));

            float header = Font.Height + 4;
            if (level == 0 && it.Node != null && it.Node.Children.Count > 0 && r.Width > 90 && r.Height > header + 40)
                LayoutLevel(it.Node, new RectangleF(r.X + 3, r.Y + header, r.Width - 6, r.Height - header - 3), 1);
        }
    }

    static Color Blend(Color a, Color b, float t) => Color.FromArgb(
        (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));

    /// <summary>Squarified treemap (Bruls, Huizing, van Wijk). sizes must be sorted descending.</summary>
    static void Squarify(double[] sizes, RectangleF rect, RectangleF[] result)
    {
        int i = 0, n = sizes.Length;
        var r = rect;
        while (i < n)
        {
            double side = Math.Min(r.Width, r.Height);
            if (side <= 0) break;
            int j = i;
            double sum = 0, worst = double.MaxValue;
            while (j < n)
            {
                double s2 = sum + sizes[j];
                double w = Worst(sizes[i], sizes[j], s2, side);
                if (j > i && w > worst) break;
                worst = w;
                sum = s2;
                j++;
            }
            if (r.Width >= r.Height)
            {
                float colW = (float)(sum / r.Height);
                float y = r.Y;
                for (int k = i; k < j; k++)
                {
                    float h = (float)(sizes[k] / colW);
                    result[k] = new RectangleF(r.X, y, colW, h);
                    y += h;
                }
                r = new RectangleF(r.X + colW, r.Y, r.Width - colW, r.Height);
            }
            else
            {
                float rowH = (float)(sum / r.Width);
                float x = r.X;
                for (int k = i; k < j; k++)
                {
                    float w = (float)(sizes[k] / rowH);
                    result[k] = new RectangleF(x, r.Y, w, rowH);
                    x += w;
                }
                r = new RectangleF(r.X, r.Y + rowH, r.Width, r.Height - rowH);
            }
            i = j;
        }
    }

    static double Worst(double max, double min, double sum, double side)
    {
        double s2 = side * side, sum2 = sum * sum;
        return Math.Max(s2 * max / sum2, sum2 / (s2 * Math.Max(min, 1e-9)));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (_dirty) Build();
        var g = e.Graphics;
        g.Clear(BackColor);
        if (_root == null)
        {
            TextRenderer.DrawText(g, "สแกนก่อน แล้วเลือกโฟลเดอร์เพื่อดู Treemap", Font, ClientRectangle, Color.Gray,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }
        using var border = new Pen(Color.White, 1);
        using var hoverPen = new Pen(Color.Black, 2);
        for (int i = 0; i < _cells.Count; i++)
        {
            var c = _cells[i];
            using (var b = new SolidBrush(c.Color)) g.FillRectangle(b, c.Rect);
            g.DrawRectangle(border, c.Rect.X, c.Rect.Y, c.Rect.Width, c.Rect.Height);
            if (c.Rect.Width > 46 && c.Rect.Height > Font.Height + 2)
            {
                var tr = Rectangle.Round(new RectangleF(c.Rect.X + 3, c.Rect.Y + 2, c.Rect.Width - 6, Font.Height + 2));
                var txt = $"{c.Label}  {Fmt.Size(c.Size)}";
                var fore = c.Color.GetBrightness() < 0.55f && c.Level == 0 ? Color.White : Color.FromArgb(25, 25, 25);
                TextRenderer.DrawText(g, txt, Font, tr, fore, TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            }
        }
        if (_hover >= 0 && _hover < _cells.Count)
        {
            var r = _cells[_hover].Rect;
            g.DrawRectangle(hoverPen, r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2);
        }
    }

    int HitTest(Point p)
    {
        for (int i = _cells.Count - 1; i >= 0; i--)
            if (_cells[i].Rect.Contains(p)) return i;
        return -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int h = HitTest(e.Location);
        if (h == _hover) return;
        _hover = h;
        Invalidate();
        if (h >= 0 && _root != null)
        {
            var c = _cells[h];
            var path = c.Node?.FullPath ?? _root.FullPath;
            double pct = _root.TotalSize > 0 ? (double)c.Size / _root.TotalSize : 0;
            _tip.SetToolTip(this, $"{c.Label}\n{Fmt.Size(c.Size)}  ({Fmt.Pct(pct)} ของโฟลเดอร์นี้)\n{path}\n\nคลิกซ้าย = เข้าไปดู • คลิกขวา = ย้อนขึ้น");
        }
        else _tip.SetToolTip(this, null);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = -1;
        Invalidate();
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (_root == null) return;
        if (e.Button == MouseButtons.Right)
        {
            if (_root.Parent != null) NodeActivated?.Invoke(_root.Parent);
            return;
        }
        int h = HitTest(e.Location);
        if (h >= 0 && _cells[h].Node is DirNode n) NodeActivated?.Invoke(n);
    }
}
