using System.Collections;

namespace HddScan.UI;

/// <summary>Cell content with an optional sort key, bar fraction, colour swatch and text colour.</summary>
public sealed record Cell(string Text, IComparable? Key = null, double? Bar = null, Color? Swatch = null, Color? Fore = null)
{
    public static Cell T(string text, Color? fore = null) => new(text, text, Fore: fore);
    public static Cell Size(long bytes) => new(Core.Fmt.Size(bytes), bytes);
    public static Cell Num(long n) => new(Core.Fmt.Num(n), n);
    public static Cell Date(DateTime utc) => new(Core.Fmt.Date(utc), utc);
    public static Cell Frac(double fraction, string? text = null) =>
        new(text ?? Core.Fmt.Pct(fraction), double.IsFinite(fraction) ? fraction : 0, double.IsFinite(fraction) ? fraction : 0);
}

/// <summary>Details ListView with an in-cell bar column, colour swatches and click-to-sort.</summary>
public sealed class BarListView : ListView
{
    public int BarColumn { get; set; } = -1;
    public Color BarColor { get; set; } = Color.FromArgb(120, 170, 225);

    int _sortCol = -1;
    bool _sortAsc;

    public BarListView()
    {
        View = View.Details;
        FullRowSelect = true;
        OwnerDraw = true;
        DoubleBuffered = true;
        HideSelection = false;
        MultiSelect = false;
        Dock = DockStyle.Fill;
    }

    public void AddColumns(params (string Text, int Width, HorizontalAlignment Align)[] cols)
    {
        float scale = DeviceDpi / 96f;
        foreach (var c in cols) Columns.Add(c.Text, (int)(c.Width * scale), c.Align);
    }

    public static ListViewItem Row(object? tag, params Cell[] cells)
    {
        var it = new ListViewItem(cells[0].Text) { Tag = tag };
        it.SubItems[0].Tag = cells[0];
        for (int i = 1; i < cells.Length; i++)
        {
            var s = it.SubItems.Add(cells[i].Text);
            s.Tag = cells[i];
        }
        return it;
    }

    public void SetRows(IEnumerable<ListViewItem> rows)
    {
        BeginUpdate();
        ListViewItemSorter = null;
        _sortCol = -1;
        Items.Clear();
        Items.AddRange(rows.ToArray());
        EndUpdate();
    }

    protected override void OnDrawColumnHeader(DrawListViewColumnHeaderEventArgs e) => e.DrawDefault = true;

    protected override void OnDrawItem(DrawListViewItemEventArgs e) => e.DrawDefault = false;

    protected override void OnDrawSubItem(DrawListViewSubItemEventArgs e)
    {
        var item = e.Item!;
        var b = e.Bounds;
        if (e.ColumnIndex == 0) b = new Rectangle(b.X, b.Y, Columns[0].Width, b.Height);

        bool sel = item.Selected;
        var back = sel ? (Focused ? SystemColors.Highlight : Color.FromArgb(204, 228, 247)) : item.BackColor;
        var cell = e.SubItem?.Tag as Cell;
        var fore = sel && Focused ? SystemColors.HighlightText : (cell?.Fore ?? item.ForeColor);

        using (var bg = new SolidBrush(back)) e.Graphics.FillRectangle(bg, b);

        var text = e.SubItem?.Text ?? "";
        var tr = Rectangle.Inflate(b, -4, 0);

        if (cell?.Swatch is Color sw)
        {
            int s = Math.Min(12 * DeviceDpi / 96, b.Height - 4);
            var r = new Rectangle(tr.X, b.Y + (b.Height - s) / 2, s, s);
            using var br = new SolidBrush(sw);
            e.Graphics.FillRectangle(br, r);
            tr.X += s + 6;
            tr.Width -= s + 6;
        }

        if (cell?.Bar is double f)
        {
            var br = new Rectangle(b.X + 3, b.Y + 3, Math.Max(0, b.Width - 6), Math.Max(0, b.Height - 6));
            using (var track = new SolidBrush(Color.FromArgb(236, 238, 241))) e.Graphics.FillRectangle(track, br);
            int w = (int)(br.Width * Math.Clamp(f, 0, 1));
            if (w > 0)
                using (var fill = new SolidBrush(BarColor)) e.Graphics.FillRectangle(fill, br.X, br.Y, w, br.Height);
            TextRenderer.DrawText(e.Graphics, text, Font, br, Color.FromArgb(30, 30, 30),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            return;
        }

        var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;
        flags |= Columns[e.ColumnIndex].TextAlign switch
        {
            HorizontalAlignment.Right => TextFormatFlags.Right,
            HorizontalAlignment.Center => TextFormatFlags.HorizontalCenter,
            _ => TextFormatFlags.Left,
        };
        TextRenderer.DrawText(e.Graphics, text, Font, tr, fore, flags);
    }

    protected override void OnColumnClick(ColumnClickEventArgs e)
    {
        base.OnColumnClick(e);
        if (_sortCol == e.Column) _sortAsc = !_sortAsc;
        else
        {
            _sortCol = e.Column;
            // text columns ascending first, numbers descending first
            var sample = Items.Count > 0 && Items[0].SubItems.Count > e.Column ? Items[0].SubItems[e.Column].Tag as Cell : null;
            _sortAsc = sample?.Key is string;
        }
        ListViewItemSorter = new Comparer(_sortCol, _sortAsc);
    }

    sealed class Comparer : IComparer
    {
        readonly int _col;
        readonly bool _asc;
        public Comparer(int col, bool asc) { _col = col; _asc = asc; }

        public int Compare(object? x, object? y)
        {
            var a = x as ListViewItem;
            var b = y as ListViewItem;
            if (a == null || b == null) return 0;
            var ka = Key(a);
            var kb = Key(b);
            int r;
            if (ka == null && kb == null) r = 0;
            else if (ka == null) r = -1;
            else if (kb == null) r = 1;
            else if (ka.GetType() == kb.GetType()) r = ka.CompareTo(kb);
            else r = string.Compare(ka.ToString(), kb.ToString(), StringComparison.CurrentCultureIgnoreCase);
            if (ka is string sa && kb is string sb) r = string.Compare(sa, sb, StringComparison.CurrentCultureIgnoreCase);
            return _asc ? r : -r;
        }

        IComparable? Key(ListViewItem it) =>
            it.SubItems.Count > _col ? (it.SubItems[_col].Tag as Cell)?.Key ?? it.SubItems[_col].Text : null;
    }
}
