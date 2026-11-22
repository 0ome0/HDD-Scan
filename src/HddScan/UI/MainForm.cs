using System.Diagnostics;
using System.Security.Principal;
using System.Text;
using HddScan.Core;
using Microsoft.VisualBasic.FileIO;

namespace HddScan.UI;

public sealed class MainForm : Form
{
    sealed record TargetItem(string Path, string Text) { public override string ToString() => Text; }

    // ---------- toolbar ----------
    readonly ComboBox _cboTarget = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    readonly Button _btnBrowse = Btn("เลือกโฟลเดอร์…");
    readonly Button _btnScan = Btn("▶  สแกน");
    readonly Button _btnCancel = Btn("ยกเลิก");
    readonly Button _btnExport = Btn("Export CSV");
    readonly Button _btnAdmin = Btn("รันแบบ Admin (เห็นครบกว่า)");
    readonly Label _lblDrive = new() { Dock = DockStyle.Top, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft };
    readonly DriveBar _driveBar = new() { Dock = DockStyle.Fill };
    readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
    readonly StatusStrip _statusStrip = new();
    readonly ToolStripStatusLabel _status = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft, Text = "เลือกไดรฟ์แล้วกด สแกน" };
    readonly ToolStripProgressBar _prog = new() { Visible = false };
    readonly System.Windows.Forms.Timer _timer = new() { Interval = 250 };

    // ---------- tab: folders ----------
    readonly TreeView _tree = new() { Dock = DockStyle.Fill, HideSelection = false };
    readonly BarListView _lvContents = new() { BarColumn = 2 };

    // ---------- tab: treemap ----------
    readonly TreemapView _treemap = new() { Dock = DockStyle.Fill };
    readonly Label _lblTreemap = new() { AutoSize = true, Margin = new Padding(8, 9, 3, 3) };
    readonly FlowLayoutPanel _legend = new() { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(4) };

    // ---------- tab: insights ----------
    readonly BarListView _lvInsights = new() { BarColumn = 3 };
    readonly BarListView _lvInsightPaths = new() { BarColumn = 2 };
    readonly TextBox _txtAdvice = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = SystemColors.Window };
    readonly Label _lblInsightSum = new() { Dock = DockStyle.Top, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(6, 0, 0, 0) };

    // ---------- tab: usage ----------
    readonly BarListView _lvUsage = new() { BarColumn = 2 };
    readonly BarListView _lvUsageDetail = new() { BarColumn = 2 };

    // ---------- tab: types ----------
    readonly BarListView _lvTypes = new() { BarColumn = 2 };
    readonly BarListView _lvExts = new() { BarColumn = 3 };

    // ---------- tab: big files ----------
    readonly BarListView _lvFiles = new() { BarColumn = 2 };
    readonly NumericUpDown _numMinMb = new() { Minimum = 0, Maximum = 10_000_000, Increment = 100, Width = 90 };
    readonly ComboBox _cboAge = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 170 };
    readonly ComboBox _cboFileType = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    readonly TextBox _txtFileFilter = new() { Width = 220, PlaceholderText = "ชื่อไฟล์ / โฟลเดอร์" };
    readonly Label _lblFiles = new() { AutoSize = true, Margin = new Padding(12, 9, 3, 3) };

    // ---------- tab: history ----------
    readonly ComboBox _cboSnap = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 280 };
    readonly CheckBox _chkCollapse = new() { Text = "ซ่อนโฟลเดอร์แม่ (แสดงจุดที่โตจริง)", Checked = true, AutoSize = true, Margin = new Padding(12, 7, 3, 3) };
    readonly Label _lblDiff = new() { AutoSize = true, Margin = new Padding(12, 9, 3, 3) };
    readonly BarListView _lvDiff = new() { BarColumn = 4 };

    readonly ContextMenuStrip _ctx = new();
    PathRef? _ctxRef;

    ScanResult? _result;
    AnalysisResult? _analysis;
    Scanner? _scanner;
    CancellationTokenSource? _cts;
    Snapshot? _currentSnap;
    List<SnapshotInfo> _snaps = new();
    bool _scanIsDrive;
    readonly bool _isAdmin;
    readonly string? _autoPath;

    static Button Btn(string text) => new() { Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(6, 2, 6, 2) };

    int S(int px) => px * DeviceDpi / 96;

    public MainForm(string? autoPath)
    {
        _autoPath = autoPath;
        _isAdmin = IsAdmin();
        Text = "HDD-Scan — พื้นที่ดิสก์หายไปไหน" + (_isAdmin ? "   [Administrator]" : "");
        Font = new Font("Leelawadee UI", 9.5f);
        Size = new Size(S(1320), S(860));
        MinimumSize = new Size(S(900), S(600));
        StartPosition = FormStartPosition.CenterScreen;

        BuildUi();
        LoadTargets();

        _timer.Tick += (_, _) => UpdateProgress();
        Shown += (_, _) =>
        {
            if (_autoPath != null && Directory.Exists(_autoPath))
            {
                SelectTarget(_autoPath);
                StartScan(_autoPath);
            }
        };
    }

    static bool IsAdmin()
    {
        try { return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator); }
        catch { return false; }
    }

    // =====================================================================
    // UI construction
    // =====================================================================
    void BuildUi()
    {
        _cboTarget.Width = S(470);
        _lblDrive.Height = S(24);
        _lblInsightSum.Height = S(34);

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(6, 6, 6, 2), WrapContents = false };
        top.Controls.Add(new Label { Text = "ไดรฟ์ / โฟลเดอร์:", AutoSize = true, Margin = new Padding(3, 8, 3, 3) });
        top.Controls.AddRange(new Control[] { _cboTarget, _btnBrowse, _btnScan, _btnCancel, _btnExport, _btnAdmin });
        _btnAdmin.Visible = !_isAdmin;
        _btnCancel.Enabled = false;
        _btnExport.Enabled = false;

        var driveBox = new Panel { Dock = DockStyle.Top, Height = S(52), Padding = new Padding(8, 0, 8, 4) };
        driveBox.Controls.Add(_driveBar);
        driveBox.Controls.Add(_lblDrive);

        _prog.Width = S(200);
        _statusStrip.Items.Add(_status);
        _statusStrip.Items.Add(_prog);

        _tabs.TabPages.Add(TabFolders());
        _tabs.TabPages.Add(TabTreemap());
        _tabs.TabPages.Add(TabInsights());
        _tabs.TabPages.Add(TabUsage());
        _tabs.TabPages.Add(TabTypes());
        _tabs.TabPages.Add(TabFiles());
        _tabs.TabPages.Add(TabHistory());
        _tabs.Padding = new Point(12, 4);

        Controls.Add(_tabs);
        Controls.Add(driveBox);
        Controls.Add(top);
        Controls.Add(_statusStrip);

        BuildContextMenu();

        _cboTarget.SelectedIndexChanged += (_, _) => UpdateDriveInfo();
        _btnBrowse.Click += (_, _) => Browse();
        _btnScan.Click += (_, _) => { if (_cboTarget.SelectedItem is TargetItem t) StartScan(t.Path); };
        _btnCancel.Click += (_, _) => _cts?.Cancel();
        _btnExport.Click += (_, _) => ExportCsv();
        _btnAdmin.Click += (_, _) => RestartAsAdmin();
    }

    SplitContainer MakeSplit(Orientation o, Control p1, Control p2, int distance)
    {
        var s = new SplitContainer { Dock = DockStyle.Fill, Orientation = o };
        s.Panel1.Controls.Add(p1);
        s.Panel2.Controls.Add(p2);
        int d = S(distance);
        bool done = false;
        void Apply()
        {
            if (done) return;
            int total = o == Orientation.Vertical ? s.Width : s.Height;
            if (total <= d + 60) return;
            try { s.SplitterDistance = d; done = true; } catch { }
        }
        s.SizeChanged += (_, _) => Apply();
        s.HandleCreated += (_, _) => Apply();
        return s;
    }

    static Label Hint(string text) => new()
    {
        Text = text, Dock = DockStyle.Top, AutoSize = false, Height = 26, ForeColor = Color.FromArgb(90, 90, 90),
        TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(6, 0, 0, 0),
    };

    TabPage TabFolders()
    {
        var tp = new TabPage("โฟลเดอร์");
        _lvContents.AddColumns(("ชื่อ", 340, HorizontalAlignment.Left), ("ขนาด", 90, HorizontalAlignment.Right),
            ("สัดส่วนในโฟลเดอร์นี้", 170, HorizontalAlignment.Center), ("จำนวนไฟล์", 90, HorizontalAlignment.Right),
            ("แก้ไขล่าสุด", 130, HorizontalAlignment.Left), ("ประเภท", 170, HorizontalAlignment.Left));
        tp.Controls.Add(MakeSplit(Orientation.Vertical, _tree, _lvContents, 440));
        tp.Controls.Add(Hint("ต้นไม้ซ้าย = โฟลเดอร์เรียงจากใหญ่ไปเล็ก • ดับเบิลคลิกรายการขวาเพื่อเข้าไปดู • คลิกขวาเพื่อเปิดใน Explorer / ย้ายไป Recycle Bin"));

        _tree.BeforeExpand += Tree_BeforeExpand;
        _tree.AfterSelect += (_, e) => { if (e.Node?.Tag is DirNode d) OnFolderSelected(d); };
        _tree.NodeMouseClick += (_, e) => { if (e.Button == MouseButtons.Right) _tree.SelectedNode = e.Node; };
        _lvContents.DoubleClick += (_, _) =>
        {
            if (Selected(_lvContents) is PathRef p)
            {
                if (p.IsDir && p.Node != null) SelectFolder(p.Node);
                else OpenInExplorer(p);
            }
        };
        return tp;
    }

    TabPage TabTreemap()
    {
        var tp = new TabPage("Treemap");
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(4) };
        var up = Btn("⬆  ขึ้นบน");
        up.Click += (_, _) => { if (_treemap.Root?.Parent is DirNode p) SelectFolder(p); };
        bar.Controls.Add(up);
        bar.Controls.Add(_lblTreemap);
        tp.Controls.Add(_treemap);
        tp.Controls.Add(_legend);
        tp.Controls.Add(bar);
        _treemap.ColorOf = n => UsageCats.Colors[n.Usage];
        _treemap.NodeActivated += SelectFolder;
        return tp;
    }

    TabPage TabInsights()
    {
        var tp = new TabPage("ตัวการที่พบบ่อย");
        _lvInsights.AddColumns(("ระดับ", 100, HorizontalAlignment.Left), ("รายการ", 380, HorizontalAlignment.Left),
            ("ขนาด", 90, HorizontalAlignment.Right), ("% ของที่สแกน", 170, HorizontalAlignment.Center),
            ("พบ (ตำแหน่ง)", 100, HorizontalAlignment.Right));
        _lvInsightPaths.AddColumns(("ตำแหน่ง", 520, HorizontalAlignment.Left), ("ขนาด", 90, HorizontalAlignment.Right),
            ("สัดส่วน", 140, HorizontalAlignment.Center));
        var bottom = MakeSplit(Orientation.Vertical, _txtAdvice, _lvInsightPaths, 470);
        tp.Controls.Add(MakeSplit(Orientation.Horizontal, _lvInsights, bottom, 300));
        tp.Controls.Add(_lblInsightSum);

        _lvInsights.SelectedIndexChanged += (_, _) => ShowInsight();
        _lvInsightPaths.DoubleClick += (_, _) => { if (Selected(_lvInsightPaths) is PathRef p) OpenInExplorer(p); };
        return tp;
    }

    TabPage TabUsage()
    {
        var tp = new TabPage("ตามการใช้งาน");
        _lvUsage.AddColumns(("การใช้งาน", 400, HorizontalAlignment.Left), ("ขนาด", 100, HorizontalAlignment.Right),
            ("% ของที่สแกน", 220, HorizontalAlignment.Center));
        _lvUsageDetail.AddColumns(("โฟลเดอร์ในหมวดนี้", 620, HorizontalAlignment.Left), ("ขนาด (เฉพาะหมวดนี้)", 130, HorizontalAlignment.Right),
            ("สัดส่วนในหมวด", 170, HorizontalAlignment.Center));
        tp.Controls.Add(MakeSplit(Orientation.Horizontal, _lvUsage, _lvUsageDetail, 330));
        tp.Controls.Add(Hint("แยกพื้นที่ตามที่มา (ระบบ, โปรแกรม, เกม, Dev, ไฟล์ส่วนตัว …) — คลิกหมวดเพื่อดูว่าอยู่ที่โฟลเดอร์ไหน • node_modules ใน Documents นับเป็น Dev"));
        _lvUsage.SelectedIndexChanged += (_, _) => ShowUsageDetail();
        _lvUsageDetail.DoubleClick += (_, _) =>
        {
            if (Selected(_lvUsageDetail) is PathRef p)
            {
                if (p.Node != null) { SelectFolder(p.Node); _tabs.SelectedIndex = 0; }
                else OpenInExplorer(p);
            }
        };
        return tp;
    }

    TabPage TabTypes()
    {
        var tp = new TabPage("ประเภทไฟล์");
        _lvTypes.AddColumns(("ประเภท", 300, HorizontalAlignment.Left), ("ขนาด", 100, HorizontalAlignment.Right),
            ("% ของที่สแกน", 220, HorizontalAlignment.Center), ("จำนวนไฟล์", 110, HorizontalAlignment.Right));
        _lvExts.AddColumns(("นามสกุล", 130, HorizontalAlignment.Left), ("ประเภท", 240, HorizontalAlignment.Left),
            ("ขนาด", 100, HorizontalAlignment.Right), ("% ของที่สแกน", 200, HorizontalAlignment.Center),
            ("จำนวนไฟล์", 110, HorizontalAlignment.Right));
        tp.Controls.Add(MakeSplit(Orientation.Horizontal, _lvTypes, _lvExts, 300));
        tp.Controls.Add(Hint("คลิกประเภทเพื่อดูนามสกุลไฟล์ • ดับเบิลคลิกเพื่อดูไฟล์ใหญ่สุดของประเภทนั้น"));
        _lvTypes.SelectedIndexChanged += (_, _) => FillExts();
        _lvTypes.DoubleClick += (_, _) =>
        {
            if (_lvTypes.SelectedItems.Count == 0 || _lvTypes.SelectedItems[0].Tag is not string t) return;
            int idx = _cboFileType.Items.IndexOf(t);
            if (idx >= 0) _cboFileType.SelectedIndex = idx;
            _tabs.SelectedIndex = 5;
        };
        return tp;
    }

    TabPage TabFiles()
    {
        var tp = new TabPage("ไฟล์ใหญ่สุด");
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(4), WrapContents = true };
        Label L(string t) => new() { Text = t, AutoSize = true, Margin = new Padding(10, 8, 2, 3) };
        _numMinMb.Width = S(90);
        _cboAge.Width = S(170);
        _cboFileType.Width = S(220);
        _txtFileFilter.Width = S(220);
        bar.Controls.AddRange(new Control[] { L("ขนาดขั้นต่ำ (MB)"), _numMinMb, L("ไม่ได้แก้ไขมานาน"), _cboAge, L("ประเภท"), _cboFileType, L("ค้นหา"), _txtFileFilter, _lblFiles });
        _cboAge.Items.AddRange(new object[] { "ทั้งหมด", "เกิน 3 เดือน", "เกิน 6 เดือน", "เกิน 1 ปี", "เกิน 2 ปี" });
        _cboAge.SelectedIndex = 0;
        _cboFileType.Items.Add("ทุกประเภท");
        foreach (var t in FileTypes.All) _cboFileType.Items.Add(t);
        _cboFileType.SelectedIndex = 0;

        _lvFiles.AddColumns(("ชื่อไฟล์", 300, HorizontalAlignment.Left), ("ขนาด", 90, HorizontalAlignment.Right),
            ("% ของที่สแกน", 150, HorizontalAlignment.Center), ("ประเภท", 170, HorizontalAlignment.Left),
            ("แก้ไขล่าสุด", 130, HorizontalAlignment.Left), ("โฟลเดอร์", 520, HorizontalAlignment.Left));
        tp.Controls.Add(_lvFiles);
        tp.Controls.Add(bar);

        _numMinMb.ValueChanged += (_, _) => FilterFiles();
        _cboAge.SelectedIndexChanged += (_, _) => FilterFiles();
        _cboFileType.SelectedIndexChanged += (_, _) => FilterFiles();
        _txtFileFilter.TextChanged += (_, _) => FilterFiles();
        _lvFiles.DoubleClick += (_, _) => { if (Selected(_lvFiles) is PathRef p) OpenInExplorer(p); };
        return tp;
    }

    TabPage TabHistory()
    {
        var tp = new TabPage("เทียบครั้งก่อน");
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(4), WrapContents = true };
        _cboSnap.Width = S(280);
        bar.Controls.AddRange(new Control[] { new Label { Text = "เทียบกับการสแกนเมื่อ:", AutoSize = true, Margin = new Padding(6, 8, 3, 3) }, _cboSnap, _chkCollapse, _lblDiff });
        _lvDiff.AddColumns(("โฟลเดอร์", 560, HorizontalAlignment.Left), ("ก่อน", 90, HorizontalAlignment.Right),
            ("ตอนนี้", 90, HorizontalAlignment.Right), ("เปลี่ยนแปลง", 110, HorizontalAlignment.Right),
            ("ขนาดการเปลี่ยน", 170, HorizontalAlignment.Center));
        _lvDiff.BarColor = Color.FromArgb(244, 160, 140);
        tp.Controls.Add(_lvDiff);
        tp.Controls.Add(Hint("ทุกครั้งที่สแกน แอปจะเก็บสรุปไว้ — สแกนซ้ำวันหลังเพื่อดูว่าโฟลเดอร์ไหนโตขึ้น (ตอบคำถาม \"ทำไมจู่ๆ เต็ม\" ได้ตรงที่สุด)"));
        tp.Controls.Add(bar);
        _cboSnap.SelectedIndexChanged += (_, _) => ShowDiff();
        _chkCollapse.CheckedChanged += (_, _) => ShowDiff();
        _lvDiff.DoubleClick += (_, _) =>
        {
            if (Selected(_lvDiff) is PathRef p)
            {
                if (p.Node != null) { SelectFolder(p.Node); _tabs.SelectedIndex = 0; }
                else OpenInExplorer(p);
            }
        };
        return tp;
    }

    void BuildContextMenu()
    {
        var open = new ToolStripMenuItem("เปิดใน Explorer", null, (_, _) => { if (_ctxRef != null) OpenInExplorer(_ctxRef); });
        var go = new ToolStripMenuItem("ไปที่โฟลเดอร์นี้ (แท็บโฟลเดอร์)", null, (_, _) =>
        {
            if (_ctxRef?.Node != null) { SelectFolder(_ctxRef.Node); _tabs.SelectedIndex = 0; }
        });
        var copy = new ToolStripMenuItem("คัดลอก path", null, (_, _) => { if (_ctxRef != null) Clipboard.SetText(_ctxRef.Path); });
        var del = new ToolStripMenuItem("ย้ายไป Recycle Bin…", null, (_, _) => { if (_ctxRef != null) DeleteToRecycle(_ctxRef); });
        _ctx.Items.AddRange(new ToolStripItem[] { open, go, copy, new ToolStripSeparator(), del });
        _ctx.Opening += (_, e) =>
        {
            _ctxRef = CurrentRef(_ctx.SourceControl);
            if (_ctxRef == null) { e.Cancel = true; return; }
            go.Visible = _ctxRef.Node != null;
        };
        foreach (Control c in new Control[] { _tree, _lvContents, _lvFiles, _lvUsageDetail, _lvInsightPaths, _lvDiff })
            c.ContextMenuStrip = _ctx;
    }

    static object? Selected(ListView lv) => lv.SelectedItems.Count > 0 ? lv.SelectedItems[0].Tag : null;

    static PathRef? CurrentRef(Control? c) => c switch
    {
        TreeView tv when tv.SelectedNode?.Tag is DirNode d => new PathRef(d.FullPath, true, d.TotalSize, d),
        ListView lv => Selected(lv) as PathRef,
        _ => null,
    };

    // =====================================================================
    // Targets & drive info
    // =====================================================================
    void LoadTargets()
    {
        _cboTarget.Items.Clear();
        int sysIdx = 0;
        var sysDrive = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)) ?? "C:\\";
        foreach (var d in DriveInfo.GetDrives())
        {
            try
            {
                if (!d.IsReady || d.DriveType is not (DriveType.Fixed or DriveType.Removable)) continue;
                long used = d.TotalSize - d.TotalFreeSpace;
                var label = string.IsNullOrWhiteSpace(d.VolumeLabel) ? "" : $"[{d.VolumeLabel}]";
                double pct = d.TotalSize > 0 ? (double)used / d.TotalSize : 0;
                if (string.Equals(d.Name, sysDrive, StringComparison.OrdinalIgnoreCase)) sysIdx = _cboTarget.Items.Count;
                _cboTarget.Items.Add(new TargetItem(d.RootDirectory.FullName,
                    $"{d.Name}  {label}   ใช้ไป {Fmt.Size(used)} / {Fmt.Size(d.TotalSize)}   (เต็ม {pct * 100:0}%)"));
            }
            catch { }
        }
        if (_cboTarget.Items.Count > 0) _cboTarget.SelectedIndex = sysIdx;
    }

    void SelectTarget(string path)
    {
        path = Path.GetFullPath(path);
        for (int i = 0; i < _cboTarget.Items.Count; i++)
            if (_cboTarget.Items[i] is TargetItem t && string.Equals(t.Path, path, StringComparison.OrdinalIgnoreCase))
            {
                _cboTarget.SelectedIndex = i;
                return;
            }
        _cboTarget.Items.Add(new TargetItem(path, "โฟลเดอร์: " + path));
        _cboTarget.SelectedIndex = _cboTarget.Items.Count - 1;
    }

    void Browse()
    {
        using var dlg = new FolderBrowserDialog { Description = "เลือกโฟลเดอร์ที่จะสแกน", UseDescriptionForTitle = true };
        if (dlg.ShowDialog(this) == DialogResult.OK) SelectTarget(dlg.SelectedPath);
    }

    void UpdateDriveInfo()
    {
        if (_cboTarget.SelectedItem is not TargetItem t) return;
        try
        {
            var di = new DriveInfo(Path.GetPathRoot(t.Path)!);
            _driveBar.Total = di.TotalSize;
            _driveBar.Free = di.TotalFreeSpace;
        }
        catch { _driveBar.Total = 0; _driveBar.Free = 0; }
        if (_result == null || !string.Equals(_result.RootPath, Path.GetFullPath(t.Path), StringComparison.OrdinalIgnoreCase))
            _driveBar.Scanned = -1;
        _driveBar.Invalidate();
        UpdateDriveLabel();
    }

    void UpdateDriveLabel()
    {
        long used = Math.Max(0, _driveBar.Total - _driveBar.Free);
        var sb = new StringBuilder();
        sb.Append($"ใช้ไป {Fmt.Size(used)} จาก {Fmt.Size(_driveBar.Total)}   •   ว่าง {Fmt.Size(_driveBar.Free)}");
        if (_driveBar.Scanned >= 0)
        {
            sb.Append($"   •   สแกนเจอ {Fmt.Size(_driveBar.Scanned)} (สีน้ำเงิน)");
            long unk = Math.Max(0, used - _driveBar.Scanned);
            if (unk > 0) sb.Append($"   •   มองไม่เห็น {Fmt.Size(unk)} (สีส้ม — ดูแท็บ \"ตัวการที่พบบ่อย\")");
        }
        _lblDrive.Text = sb.ToString();
    }

    // =====================================================================
    // Scanning
    // =====================================================================
    async void StartScan(string path)
    {
        if (_cts != null) return;
        if (!Directory.Exists(path))
        {
            MessageBox.Show(this, "ไม่พบโฟลเดอร์นี้: " + path, "HDD-Scan", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        var full = Path.GetFullPath(path);
        var droot = Path.GetPathRoot(full) ?? full;
        _scanIsDrive = string.Equals(full.TrimEnd('\\'), droot.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
        UpdateDriveInfo();

        _cts = new CancellationTokenSource();
        var scanner = _scanner = new Scanner();
        var ct = _cts.Token;
        SetScanning(true);
        try
        {
            var (res, an, snaps, snap) = await Task.Run(() =>
            {
                var r = scanner.Scan(full, ct);
                var a = Analyzer.Run(r);
                List<SnapshotInfo> prev;
                try { prev = History.List(r.RootPath); } catch { prev = new(); }
                var s = History.Build(r);
                try { History.Save(s); } catch { }
                return (r, a, prev, s);
            });
            _result = res;
            _analysis = an;
            _snaps = snaps;
            _currentSnap = snap;
            ShowResult(null);
            _status.Text = $"สแกนเสร็จใน {res.Elapsed.TotalSeconds:0.0} วินาที — {Fmt.Num(res.Root.TotalFiles)} ไฟล์, " +
                           $"{Fmt.Num(res.Root.TotalDirs)} โฟลเดอร์, รวม {Fmt.Size(res.Root.TotalSize)}" +
                           (res.DeniedDirs > 0 ? $"   •   เข้าไม่ได้ {Fmt.Num(res.DeniedDirs)} โฟลเดอร์" + (_isAdmin ? "" : " (ลองรันแบบ Admin)") : "");
        }
        catch (OperationCanceledException)
        {
            _status.Text = "ยกเลิกการสแกนแล้ว";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.ToString(), "เกิดข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _status.Text = "สแกนไม่สำเร็จ";
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            _scanner = null;
            SetScanning(false);
        }
    }

    void SetScanning(bool on)
    {
        _btnScan.Enabled = !on;
        _btnCancel.Enabled = on;
        _btnBrowse.Enabled = !on;
        _cboTarget.Enabled = !on;
        _btnExport.Enabled = !on && _result != null;
        _prog.Visible = on;
        _prog.Value = 0;
        _prog.Style = _scanIsDrive ? ProgressBarStyle.Continuous : ProgressBarStyle.Marquee;
        UseWaitCursor = false;
        if (on) _timer.Start(); else _timer.Stop();
    }

    void UpdateProgress()
    {
        var s = _scanner;
        if (s == null) return;
        _status.Text = $"กำลังสแกน…  {Fmt.Num(s.Files)} ไฟล์  •  {Fmt.Num(s.Dirs)} โฟลเดอร์  •  {Fmt.Size(s.Bytes)}     {s.CurrentPath}";
        if (_scanIsDrive)
        {
            long used = Math.Max(1, _driveBar.Total - _driveBar.Free);
            _prog.Value = (int)Math.Clamp(s.Bytes * 100 / used, 0, 100);
        }
    }

    // =====================================================================
    // Showing results
    // =====================================================================
    void ShowResult(string? keepPath)
    {
        var r = _result!;
        _driveBar.Scanned = r.IsDriveRoot ? r.Root.TotalSize : -1;
        _driveBar.Invalidate();
        UpdateDriveLabel();

        _tree.BeginUpdate();
        _tree.Nodes.Clear();
        var rootTn = MakeTreeNode(r.Root);
        _tree.Nodes.Add(rootTn);
        _tree.EndUpdate();
        rootTn.Expand();

        var sel = keepPath != null ? FindNode(keepPath) : null;
        SelectFolder(sel ?? r.Root);

        FillInsights();
        FillUsage();
        FillTypes();
        FillFiles();
        FillHistory();
        FillLegend();
        _btnExport.Enabled = true;
    }

    // ---------- folders ----------
    static string NodeText(DirNode d)
    {
        long parent = d.Parent?.TotalSize ?? d.TotalSize;
        double f = parent > 0 ? (double)d.TotalSize / parent : 1;
        return $"{d.Name}    {Fmt.Size(d.TotalSize)}   ({f * 100:0.0}%)" + (d.AccessDenied ? "   [เข้าไม่ได้]" : "");
    }

    static TreeNode MakeTreeNode(DirNode d)
    {
        var tn = new TreeNode(NodeText(d)) { Tag = d };
        if (d.AccessDenied) tn.ForeColor = Color.Gray;
        if (d.Children.Count > 0) tn.Nodes.Add(new TreeNode("…"));
        return tn;
    }

    void Tree_BeforeExpand(object? sender, TreeViewCancelEventArgs e)
    {
        var tn = e.Node!;
        if (tn.Nodes.Count != 1 || tn.Nodes[0].Tag != null || tn.Tag is not DirNode d) return;
        _tree.BeginUpdate();
        tn.Nodes.Clear();
        int i = 0;
        foreach (var c in d.Children)
        {
            if (i++ >= 500)
            {
                tn.Nodes.Add(new TreeNode($"… อีก {d.Children.Count - 500:N0} โฟลเดอร์ (ดูในรายการด้านขวา)") { ForeColor = Color.Gray, Tag = "more" });
                break;
            }
            tn.Nodes.Add(MakeTreeNode(c));
        }
        _tree.EndUpdate();
    }

    void SelectFolder(DirNode d)
    {
        var chain = new Stack<DirNode>();
        for (var n = d; n != null; n = n.Parent) chain.Push(n);
        TreeNodeCollection col = _tree.Nodes;
        TreeNode? tn = null;
        bool complete = true;
        while (chain.Count > 0)
        {
            var n = chain.Pop();
            tn = col.Cast<TreeNode>().FirstOrDefault(x => ReferenceEquals(x.Tag, n));
            if (tn == null) { complete = false; break; }
            if (chain.Count > 0) { tn.Expand(); col = tn.Nodes; }
        }
        if (complete && tn != null)
        {
            if (_tree.SelectedNode == tn) OnFolderSelected(d);
            else _tree.SelectedNode = tn;
            tn.EnsureVisible();
        }
        else OnFolderSelected(d);
    }

    void OnFolderSelected(DirNode d)
    {
        ShowContents(d);
        _treemap.Root = d;
        _lblTreemap.Text = $"{d.FullPath}    {Fmt.Size(d.TotalSize)}     (คลิกซ้าย = เข้าไปดู • คลิกขวา = ย้อนขึ้น)";
    }

    void ShowContents(DirNode d)
    {
        long denom = Math.Max(1, d.TotalSize);
        var rows = new List<ListViewItem>();
        var folderColor = Color.FromArgb(0, 70, 140);
        foreach (var c in d.Children)
        {
            rows.Add(BarListView.Row(new PathRef(c.FullPath, true, c.TotalSize, c),
                new Cell(c.Name, c.Name, Swatch: UsageCats.Colors[c.Usage], Fore: folderColor),
                Cell.Size(c.TotalSize), Cell.Frac((double)c.TotalSize / denom), Cell.Num(c.TotalFiles),
                Cell.Date(c.LastWriteUtc), Cell.T(c.AccessDenied ? "โฟลเดอร์ (เข้าไม่ได้)" : "โฟลเดอร์")));
        }
        try
        {
            var opts = new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = 0 };
            var files = new DirectoryInfo(d.FullPath).EnumerateFiles("*", opts)
                .Select(f => { try { return (f.FullName, f.Name, f.Length, f.LastWriteTimeUtc); } catch { return (f.FullName, f.Name, 0L, default(DateTime)); } })
                .OrderByDescending(f => f.Item3).Take(3000);
            foreach (var f in files)
                rows.Add(BarListView.Row(new PathRef(f.FullName, false, f.Item3),
                    Cell.T(f.Name), Cell.Size(f.Item3), Cell.Frac((double)f.Item3 / denom), new Cell("", -1L),
                    Cell.Date(f.Item4), Cell.T(FileTypes.OfPath(f.Name))));
        }
        catch { }
        rows.Sort((a, b) => (((PathRef)b.Tag!).Size).CompareTo(((PathRef)a.Tag!).Size));
        _lvContents.SetRows(rows);
    }

    DirNode? FindNode(string path)
    {
        var r = _result;
        if (r == null) return null;
        if (string.Equals(path.TrimEnd('\\'), r.RootPath.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return r.Root;
        if (!path.StartsWith(r.RootPath, StringComparison.OrdinalIgnoreCase)) return null;
        var rel = Path.GetRelativePath(r.RootPath, path);
        var n = r.Root;
        foreach (var seg in rel.Split('\\', StringSplitOptions.RemoveEmptyEntries))
        {
            n = n.Children.FirstOrDefault(c => string.Equals(c.Name, seg, StringComparison.OrdinalIgnoreCase));
            if (n == null) return null;
        }
        return n;
    }

    void FillLegend()
    {
        _legend.SuspendLayout();
        _legend.Controls.Clear();
        var a = _analysis!;
        for (int i = 0; i < a.Usage.Length; i++)
        {
            if (a.Usage[i] <= 0) continue;
            _legend.Controls.Add(new Label { Text = "■", ForeColor = UsageCats.Colors[i], AutoSize = true, Margin = new Padding(8, 3, 0, 3), Font = new Font(Font.FontFamily, 12f) });
            _legend.Controls.Add(new Label { Text = UsageCats.Names[i], AutoSize = true, Margin = new Padding(0, 6, 6, 3) });
        }
        _legend.ResumeLayout();
    }

    // ---------- insights ----------
    static string LevelText(Safety s) => s switch
    {
        Safety.Safe => "✔ ลบได้",
        Safety.Caution => "⚠ ระวัง",
        Safety.Danger => "⛔ ห้ามลบเอง",
        _ => "ℹ ข้อมูล",
    };

    static Color LevelColor(Safety s) => s switch
    {
        Safety.Safe => Color.FromArgb(232, 245, 233),
        Safety.Caution => Color.FromArgb(255, 248, 225),
        Safety.Danger => Color.FromArgb(255, 235, 238),
        _ => Color.FromArgb(227, 242, 253),
    };

    void FillInsights()
    {
        var r = _result!;
        var list = _analysis!.Insights;
        double denom = Math.Max(1, r.IsDriveRoot ? r.DriveUsed : r.Root.TotalSize);
        long max = Math.Max(1, list.Count > 0 ? list.Max(i => i.Size) : 1);
        var rows = list.Select(i =>
        {
            var it = BarListView.Row(i, Cell.T(LevelText(i.Level)), Cell.T(i.Title), Cell.Size(i.Size),
                new Cell(i.Size > 0 ? Fmt.Pct(i.Size / denom) : "", i.Size, (double)i.Size / max),
                Cell.Num(i.Items.Count));
            it.BackColor = LevelColor(i.Level);
            return it;
        });
        _lvInsights.SetRows(rows);

        long safe = list.Where(i => i.Level == Safety.Safe).Sum(i => i.Size);
        long caution = list.Where(i => i.Level == Safety.Caution).Sum(i => i.Size);
        _lblInsightSum.Text = $"✔ ล้างได้ปลอดภัยรวมประมาณ {Fmt.Size(safe)}     ⚠ ควรพิจารณา {Fmt.Size(caution)}" +
                              "     — คลิกรายการเพื่อดูวิธีจัดการ • คลิกขวาที่ตำแหน่งด้านล่างขวาเพื่อเปิด / ลบ";
        if (_lvInsights.Items.Count > 0) _lvInsights.Items[0].Selected = true;
        else { _txtAdvice.Text = "ไม่พบตัวการที่พบบ่อย"; _lvInsightPaths.SetRows(Array.Empty<ListViewItem>()); }
    }

    void ShowInsight()
    {
        if (Selected(_lvInsights) is not Insight ins) return;
        _txtAdvice.Text = $"{ins.Title}\r\n" + (ins.Size > 0 ? $"ขนาด: {Fmt.Size(ins.Size)}\r\n" : "") +
                          "\r\n" + ins.Advice.Replace("\r\n", "\n").Replace("\n", "\r\n");
        double denom = Math.Max(1, ins.Size);
        _lvInsightPaths.SetRows(ins.Items.Take(500).Select(p =>
            BarListView.Row(p, Cell.T(p.Path), Cell.Size(p.Size), Cell.Frac(p.Size / denom))));
    }

    // ---------- usage ----------
    void FillUsage()
    {
        var a = _analysis!;
        double total = Math.Max(1, a.Usage.Sum());
        var rows = Enumerable.Range(0, a.Usage.Length)
            .Where(i => a.Usage[i] > 0)
            .OrderByDescending(i => a.Usage[i])
            .Select(i => BarListView.Row(i,
                new Cell(UsageCats.Names[i], UsageCats.Names[i], Swatch: UsageCats.Colors[i]),
                Cell.Size(a.Usage[i]), Cell.Frac(a.Usage[i] / total)));
        _lvUsage.SetRows(rows);
        if (_lvUsage.Items.Count > 0) _lvUsage.Items[0].Selected = true;
    }

    void ShowUsageDetail()
    {
        if (Selected(_lvUsage) is not int cat) return;
        var a = _analysis!;
        double denom = Math.Max(1, a.Usage[cat]);
        IEnumerable<ListViewItem> rows;
        if (cat == UsageCats.SysFiles)
            rows = a.SysFiles.Select(f => BarListView.Row(new PathRef(f.Path, false, f.Size), Cell.T(f.Path), Cell.Size(f.Size), Cell.Frac(f.Size / denom)));
        else
            rows = a.UsageRoots.Where(u => u.Cat == cat && u.Size > 0).OrderByDescending(u => u.Size).Take(500)
                .Select(u => BarListView.Row(new PathRef(u.Node.FullPath, true, u.Size, u.Node),
                    Cell.T(u.Node.FullPath), Cell.Size(u.Size), Cell.Frac(u.Size / denom)));
        _lvUsageDetail.SetRows(rows);
    }

    // ---------- types ----------
    void FillTypes()
    {
        var a = _analysis!;
        double total = Math.Max(1, a.Types.Sum(t => t.Size));
        _lvTypes.SetRows(a.Types.Select(t => BarListView.Row(t.Name, Cell.T(t.Name), Cell.Size(t.Size), Cell.Frac(t.Size / total), Cell.Num(t.Count))));
        FillExts();
    }

    void FillExts()
    {
        var a = _analysis;
        if (a == null) return;
        string? type = Selected(_lvTypes) as string;
        double total = Math.Max(1, a.Types.Sum(t => t.Size));
        var rows = a.Exts.Where(e => type == null || e.Type == type).Take(400)
            .Select(e => BarListView.Row(e, Cell.T(e.Ext), Cell.T(e.Type), Cell.Size(e.Size), Cell.Frac(e.Size / total), Cell.Num(e.Count)));
        _lvExts.SetRows(rows);
    }

    // ---------- big files ----------
    void FillFiles() => FilterFiles();

    void FilterFiles()
    {
        var r = _result;
        if (r == null) return;
        long min = (long)_numMinMb.Value * 1024 * 1024;
        DateTime? older = _cboAge.SelectedIndex switch
        {
            1 => DateTime.UtcNow.AddMonths(-3),
            2 => DateTime.UtcNow.AddMonths(-6),
            3 => DateTime.UtcNow.AddYears(-1),
            4 => DateTime.UtcNow.AddYears(-2),
            _ => null,
        };
        string? type = _cboFileType.SelectedIndex > 0 ? _cboFileType.SelectedItem as string : null;
        string q = _txtFileFilter.Text.Trim();
        var files = r.TopFiles.Where(f =>
            f.Size >= min &&
            (older == null || f.ModifiedUtc < older) &&
            (type == null || FileTypes.OfPath(f.Path) == type) &&
            (q.Length == 0 || f.Path.Contains(q, StringComparison.OrdinalIgnoreCase))).ToList();
        double denom = Math.Max(1, r.Root.TotalSize);
        long max = Math.Max(1, files.Count > 0 ? files[0].Size : 1);
        _lvFiles.SetRows(files.Select(f => BarListView.Row(new PathRef(f.Path, false, f.Size),
            Cell.T(Path.GetFileName(f.Path)), Cell.Size(f.Size), new Cell(Fmt.Pct(f.Size / denom), f.Size, (double)f.Size / max),
            Cell.T(FileTypes.OfPath(f.Path)), Cell.Date(f.ModifiedUtc), Cell.T(Path.GetDirectoryName(f.Path) ?? ""))));
        _lblFiles.Text = $"แสดง {files.Count:N0} ไฟล์  รวม {Fmt.Size(files.Sum(f => f.Size))}   (จาก {Scanner.TopN:N0} ไฟล์ใหญ่สุด)";
    }

    // ---------- history ----------
    void FillHistory()
    {
        _cboSnap.Items.Clear();
        foreach (var s in _snaps) _cboSnap.Items.Add(s);
        if (_cboSnap.Items.Count > 0) _cboSnap.SelectedIndex = 0;
        else
        {
            _lblDiff.Text = "ยังไม่มีผลสแกนครั้งก่อนของตำแหน่งนี้ — บันทึกครั้งนี้ไว้แล้ว สแกนอีกครั้งวันหลังเพื่อดูว่าอะไรโตขึ้น";
            _lvDiff.SetRows(Array.Empty<ListViewItem>());
        }
    }

    void ShowDiff()
    {
        if (_cboSnap.SelectedItem is not SnapshotInfo info || _currentSnap == null) return;
        var old = History.Load(info.File);
        if (old == null) { _lblDiff.Text = "อ่านไฟล์ประวัติไม่ได้"; return; }
        var rows = History.Diff(old, _currentSnap, 20L << 20);
        if (_chkCollapse.Checked) rows = History.Collapse(rows);
        long change = _currentSnap.Total - old.Total;
        _lblDiff.Text = $"รวม: {Fmt.Size(old.Total)} → {Fmt.Size(_currentSnap.Total)}  ({Fmt.SignedSize(change)})   •   {rows.Count:N0} โฟลเดอร์ที่เปลี่ยน ≥ 20 MB";
        long maxAbs = Math.Max(1, rows.Count > 0 ? rows.Max(x => Math.Abs(x.Delta)) : 1);
        var up = Color.FromArgb(198, 40, 40);
        var down = Color.FromArgb(46, 125, 50);
        _lvDiff.SetRows(rows.Select(x => BarListView.Row(new PathRef(x.Path, true, x.After, FindNode(x.Path)),
            Cell.T(x.Path), Cell.Size(x.Before), Cell.Size(x.After),
            new Cell(Fmt.SignedSize(x.Delta), x.Delta, Fore: x.Delta > 0 ? up : down),
            new Cell("", Math.Abs(x.Delta), (double)Math.Abs(x.Delta) / maxAbs))));
    }

    // =====================================================================
    // Actions
    // =====================================================================
    void OpenInExplorer(PathRef p)
    {
        try
        {
            if (p.IsDir) Process.Start("explorer.exe", $"\"{p.Path}\"");
            else Process.Start("explorer.exe", $"/select,\"{p.Path}\"");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "เปิด Explorer ไม่ได้", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    void DeleteToRecycle(PathRef p)
    {
        var win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        static string T(string x) => x.TrimEnd('\\');
        bool isRoot = string.Equals(T(p.Path), T(Path.GetPathRoot(p.Path) ?? ""), StringComparison.OrdinalIgnoreCase) ||
                      (_result != null && string.Equals(T(p.Path), T(_result.RootPath), StringComparison.OrdinalIgnoreCase));
        if (isRoot || (win.Length > 0 && (string.Equals(T(p.Path), T(win), StringComparison.OrdinalIgnoreCase) || p.Path.StartsWith(T(win) + "\\", StringComparison.OrdinalIgnoreCase))))
        {
            MessageBox.Show(this, "ไม่อนุญาตให้ลบไดรฟ์ทั้งลูกหรือไฟล์ในโฟลเดอร์ Windows จากแอปนี้\nให้ใช้วิธีที่แนะนำในแท็บ \"ตัวการที่พบบ่อย\" แทน",
                "HDD-Scan", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var msg = $"ย้ายไป Recycle Bin?\n\n{p.Path}\nขนาด {Fmt.Size(p.Size)}\n\n" +
                  "หมายเหตุ: ไฟล์ใน Recycle Bin ยังกินพื้นที่อยู่จนกว่าจะกด Empty Recycle Bin";
        if (MessageBox.Show(this, msg, "ยืนยัน", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            return;
        try
        {
            if (p.IsDir) FileSystem.DeleteDirectory(p.Path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            else FileSystem.DeleteFile(p.Path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
        }
        catch (OperationCanceledException) { return; }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "ลบไม่สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        string keep;
        var node = p.IsDir ? p.Node ?? FindNode(p.Path) : null;
        if (node?.Parent is DirNode parent)
        {
            parent.Children.Remove(node);
            parent.Subtract(node.TotalSize, node.TotalFiles);
            keep = parent.FullPath;
        }
        else if (!p.IsDir)
        {
            var dirPath = Path.GetDirectoryName(p.Path) ?? "";
            var dir = FindNode(dirPath);
            if (dir != null)
            {
                dir.OwnSize -= p.Size;
                dir.OwnFiles--;
                dir.Subtract(p.Size, 1);
            }
            _result!.TopFiles.RemoveAll(f => string.Equals(f.Path, p.Path, StringComparison.OrdinalIgnoreCase));
            keep = dirPath;
        }
        else keep = _result!.RootPath;

        _analysis = Analyzer.Run(_result!);
        int tab = _tabs.SelectedIndex;
        ShowResult(keep);
        _tabs.SelectedIndex = tab;
        _status.Text = $"ย้ายไป Recycle Bin แล้ว: {p.Path} ({Fmt.Size(p.Size)})";
    }

    void RestartAsAdmin()
    {
        var exe = Environment.ProcessPath;
        if (exe == null) return;
        var arg = (_cboTarget.SelectedItem as TargetItem)?.Path;
        var psi = new ProcessStartInfo(exe) { UseShellExecute = true, Verb = "runas" };
        if (arg != null) psi.Arguments = "\"" + (arg.EndsWith('\\') ? arg + "\\" : arg) + "\"";
        try
        {
            Process.Start(psi);
            Close();
        }
        catch (System.ComponentModel.Win32Exception) { /* user pressed No on UAC */ }
    }

    void ExportCsv()
    {
        var r = _result;
        var a = _analysis;
        if (r == null || a == null) return;
        var name = new string(r.RootPath.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray()).Trim('_');
        using var dlg = new SaveFileDialog
        {
            Filter = "CSV (*.csv)|*.csv",
            FileName = $"hddscan_{name}_{DateTime.Now:yyyyMMdd_HHmm}.csv",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        static string Q(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";
        var sb = new StringBuilder();
        sb.AppendLine($"HDD-Scan,{Q(r.RootPath)},{DateTime.Now:yyyy-MM-dd HH:mm}");
        sb.AppendLine();
        sb.AppendLine("## ตัวการที่พบบ่อย");
        sb.AppendLine("ระดับ,รายการ,ขนาด (bytes),ขนาด,จำนวนตำแหน่ง");
        foreach (var i in a.Insights) sb.AppendLine($"{Q(LevelText(i.Level))},{Q(i.Title)},{i.Size},{Q(Fmt.Size(i.Size))},{i.Items.Count}");
        sb.AppendLine();
        sb.AppendLine("## ตามการใช้งาน");
        sb.AppendLine("หมวด,ขนาด (bytes),ขนาด");
        for (int i = 0; i < a.Usage.Length; i++)
            if (a.Usage[i] > 0) sb.AppendLine($"{Q(UsageCats.Names[i])},{a.Usage[i]},{Q(Fmt.Size(a.Usage[i]))}");
        sb.AppendLine();
        sb.AppendLine("## ประเภทไฟล์");
        sb.AppendLine("ประเภท,ขนาด (bytes),ขนาด,จำนวนไฟล์");
        foreach (var t in a.Types) sb.AppendLine($"{Q(t.Name)},{t.Size},{Q(Fmt.Size(t.Size))},{t.Count}");
        sb.AppendLine();
        sb.AppendLine("## โฟลเดอร์ (ลึกไม่เกิน 4 ชั้น, ≥ 100 MB)");
        sb.AppendLine("โฟลเดอร์,ระดับ,ขนาด (bytes),ขนาด,จำนวนไฟล์,แก้ไขล่าสุด");
        void Walk(DirNode n, int depth)
        {
            sb.AppendLine($"{Q(n.FullPath)},{depth},{n.TotalSize},{Q(Fmt.Size(n.TotalSize))},{n.TotalFiles},{Q(Fmt.Date(n.LastWriteUtc))}");
            if (depth >= 4) return;
            foreach (var c in n.Children)
            {
                if (c.TotalSize < 100L << 20) break;
                Walk(c, depth + 1);
            }
        }
        Walk(r.Root, 0);
        sb.AppendLine();
        sb.AppendLine("## ไฟล์ใหญ่สุด");
        sb.AppendLine("ไฟล์,ขนาด (bytes),ขนาด,แก้ไขล่าสุด,ประเภท");
        foreach (var f in r.TopFiles) sb.AppendLine($"{Q(f.Path)},{f.Size},{Q(Fmt.Size(f.Size))},{Q(Fmt.Date(f.ModifiedUtc))},{Q(FileTypes.OfPath(f.Path))}");

        try
        {
            File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
            _status.Text = "บันทึกแล้ว: " + dlg.FileName;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "บันทึกไม่สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
