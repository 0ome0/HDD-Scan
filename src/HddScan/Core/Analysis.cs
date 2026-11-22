using System.Drawing;
using System.IO.Enumeration;

namespace HddScan.Core;

public enum Safety { Safe, Caution, Danger, Info }

public sealed class Insight
{
    public string Title = "";
    public Safety Level;
    public string Advice = "";
    public long Size;
    public List<PathRef> Items = new();
}

public sealed class TypeStat { public string Name = ""; public long Size; public long Count; }
public sealed class ExtRow { public string Ext = ""; public string Type = ""; public long Size; public long Count; }

/// <summary>A folder where a usage category starts, with the bytes that belong to that category below it.</summary>
public sealed class UsageRoot { public DirNode Node = null!; public int Cat; public long Size; }

public sealed class AnalysisResult
{
    public long[] Usage = new long[UsageCats.Names.Length];
    public List<UsageRoot> UsageRoots = new();
    public List<FileItem> SysFiles = new();
    public List<Insight> Insights = new();
    public List<TypeStat> Types = new();
    public List<ExtRow> Exts = new();
}

/// <summary>
/// Path pattern relative to the drive root, e.g. <c>Users\*\AppData\Local\Temp</c>.
/// A leading <c>**</c> matches at any depth, e.g. <c>**\node_modules</c>.
/// </summary>
public sealed class Pattern
{
    readonly string[] _segs;
    readonly bool[] _wild;
    readonly bool _anyPrefix;

    public Pattern(string p)
    {
        var s = p.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
        _anyPrefix = s.Length > 0 && s[0] == "**";
        _segs = _anyPrefix ? s[1..] : s;
        _wild = _segs.Select(x => x.IndexOfAny(new[] { '*', '?' }) >= 0).ToArray();
    }

    public bool IsMatch(List<string> path)
    {
        int off;
        if (_anyPrefix)
        {
            if (path.Count < _segs.Length) return false;
            off = path.Count - _segs.Length;
        }
        else
        {
            if (path.Count != _segs.Length) return false;
            off = 0;
        }
        for (int i = _segs.Length - 1; i >= 0; i--)
        {
            var seg = path[off + i];
            bool ok = _wild[i]
                ? FileSystemName.MatchesSimpleExpression(_segs[i], seg, ignoreCase: true)
                : string.Equals(_segs[i], seg, StringComparison.OrdinalIgnoreCase);
            if (!ok) return false;
        }
        return true;
    }
}

public static class FileTypes
{
    public const string Other = "อื่นๆ";
    public const string NoExt = "ไม่มีนามสกุล";

    static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase);
    static readonly List<string> Order = new();

    static void Add(string cat, string exts)
    {
        Order.Add(cat);
        foreach (var e in exts.Split(' ', StringSplitOptions.RemoveEmptyEntries)) Map["." + e] = cat;
    }

    static FileTypes()
    {
        Add("วิดีโอ", "mp4 mkv avi mov wmv flv webm m4v mts m2ts 3gp mpg mpeg vob");
        Add("รูปภาพ", "jpg jpeg png gif bmp tif tiff heic heif webp raw cr2 cr3 nef arw raf dng orf psd ai svg ico");
        Add("เสียง / เพลง", "mp3 wav flac aac m4a ogg wma opus aiff");
        Add("เอกสาร", "pdf doc docx xls xlsx xlsm ppt pptx txt csv odt ods odp rtf md epub");
        Add("โปรแกรม / ไฟล์ติดตั้ง / ISO", "exe msi msix msixbundle appx appxbundle iso img cab msu esd wim apk");
        Add("ไฟล์บีบอัด", "zip rar 7z tar gz bz2 xz tgz zst");
        Add("VM / ดิสก์เสมือน", "vhd vhdx avhdx vmdk vdi qcow2 vmem vmsn");
        Add("ฐานข้อมูล / อีเมล / แบ็กอัป", "db sqlite sqlite3 mdf ldf ndf ost pst bak sql dump ibd edb");
        Add("ไลบรารี / ไฟล์ระบบ", "dll sys so lib pdb drv mui ocx winmd node");
        Add("โค้ด / Dev", "cs js mjs cjs ts tsx jsx py java kt go rs php rb c cpp h hpp json xml yml yaml map class jar o obj nupkg whl pyc css scss html vue");
        Add("ไฟล์เกม", "pak vpk bundle assets ucas utoc bsa ba2 wad pck forge big");
        Add("Log / Temp / Dump", "log etl tmp temp dmp mdmp old evtx");
        Add("ฟอนต์", "ttf otf ttc woff woff2");
        Order.Add(NoExt);
        Order.Add(Other);
    }

    public static IReadOnlyList<string> All => Order;

    public static string Of(string ext) =>
        ext.Length == 0 ? NoExt : Map.TryGetValue(ext, out var c) ? c : Other;

    public static string OfPath(string path) => Of(Path.GetExtension(path));
}

public static class UsageCats
{
    public const int Other = 0, Windows = 1, SysFiles = 2, Programs = 3, ProgramData = 4, Games = 5,
        Dev = 6, Vm = 7, Browser = 8, Temp = 9, Recycle = 10, WinOld = 11, Personal = 12,
        AppData = 13, UserOther = 14, Cloud = 15;

    public static readonly string[] Names =
    {
        "อื่นๆ",
        "Windows (ระบบปฏิบัติการ)",
        "ไฟล์ระบบ (pagefile / hiberfil)",
        "โปรแกรมที่ติดตั้ง (Program Files)",
        "ข้อมูลโปรแกรม (ProgramData)",
        "เกม",
        "Dev tools / แคชนักพัฒนา",
        "Docker / WSL / VM",
        "เบราว์เซอร์ (Chrome / Edge / Firefox)",
        "ไฟล์ชั่วคราว / Temp / แคชอัปเดต",
        "ถังขยะ (Recycle Bin)",
        "Windows.old (Windows เวอร์ชันเก่า)",
        "ไฟล์ส่วนตัว (Desktop / Documents / Downloads …)",
        "ข้อมูลแอปของผู้ใช้ (AppData)",
        "โฟลเดอร์ผู้ใช้ (อื่นๆ)",
        "Cloud sync (OneDrive / Dropbox / Google Drive)",
    };

    public static readonly Color[] Colors =
    {
        Color.FromArgb(158, 158, 158),
        Color.FromArgb(66, 133, 244),
        Color.FromArgb(48, 63, 159),
        Color.FromArgb(0, 150, 136),
        Color.FromArgb(77, 182, 172),
        Color.FromArgb(156, 39, 176),
        Color.FromArgb(255, 152, 0),
        Color.FromArgb(121, 85, 72),
        Color.FromArgb(244, 67, 54),
        Color.FromArgb(255, 193, 7),
        Color.FromArgb(233, 30, 99),
        Color.FromArgb(96, 125, 139),
        Color.FromArgb(76, 175, 80),
        Color.FromArgb(139, 195, 74),
        Color.FromArgb(205, 220, 57),
        Color.FromArgb(3, 169, 244),
    };

    static readonly (Pattern P, int Cat)[] Rules = new (string, int)[]
    {
        (@"Windows", Windows),
        (@"Windows\Temp", Temp),
        (@"Windows\SoftwareDistribution", Temp),
        (@"Program Files", Programs),
        (@"Program Files (x86)", Programs),
        (@"ProgramData", ProgramData),
        (@"ProgramData\DockerDesktop", Vm),
        (@"ProgramData\Docker", Vm),
        (@"$Recycle.Bin", Recycle),
        (@"Windows.old", WinOld),
        (@"XboxGames", Games),
        (@"Users", UserOther),
        (@"Users\*\AppData", AppData),
        (@"Users\*\AppData\Local\Temp", Temp),
        (@"Users\*\AppData\Local\Google\Chrome", Browser),
        (@"Users\*\AppData\Local\Microsoft\Edge", Browser),
        (@"Users\*\AppData\Local\BraveSoftware", Browser),
        (@"Users\*\AppData\Local\Mozilla", Browser),
        (@"Users\*\AppData\Roaming\Mozilla", Browser),
        (@"Users\*\AppData\Local\Docker", Vm),
        (@"Users\*\AppData\Local\wsl", Vm),
        (@"Users\*\AppData\Local\Packages\*CanonicalGroup*", Vm),
        (@"Users\*\AppData\Local\Packages\*Debian*", Vm),
        (@"Users\*\AppData\Local\Packages\*KaliLinux*", Vm),
        (@"**\VirtualBox VMs", Vm),
        (@"**\Virtual Machines", Vm),
        (@"**\Virtual Hard Disks", Vm),
        (@"Users\*\.nuget", Dev),
        (@"Users\*\.gradle", Dev),
        (@"Users\*\.m2", Dev),
        (@"Users\*\.android", Dev),
        (@"Users\*\.cargo", Dev),
        (@"Users\*\.rustup", Dev),
        (@"Users\*\.vscode", Dev),
        (@"Users\*\AppData\Local\Android", Dev),
        (@"Users\*\AppData\Local\npm-cache", Dev),
        (@"Users\*\AppData\Roaming\npm-cache", Dev),
        (@"Users\*\AppData\Local\pip", Dev),
        (@"Users\*\AppData\Local\Yarn", Dev),
        (@"Users\*\AppData\Local\pnpm", Dev),
        (@"Users\*\AppData\Local\pnpm-cache", Dev),
        (@"Users\*\AppData\Local\Composer", Dev),
        (@"Users\*\AppData\Roaming\Composer", Dev),
        (@"Users\*\AppData\Local\JetBrains", Dev),
        (@"Users\*\AppData\Local\Microsoft\VisualStudio", Dev),
        (@"**\node_modules", Dev),
        (@"**\steamapps", Games),
        (@"**\Epic Games", Games),
        (@"**\Riot Games", Games),
        (@"**\GOG Games", Games),
        (@"**\EA Games", Games),
        (@"Users\*\Desktop", Personal),
        (@"Users\*\Documents", Personal),
        (@"Users\*\Downloads", Personal),
        (@"Users\*\Pictures", Personal),
        (@"Users\*\Videos", Personal),
        (@"Users\*\Music", Personal),
        (@"Users\*\OneDrive*", Cloud),
        (@"Users\*\Dropbox", Cloud),
        (@"Users\*\Google Drive", Cloud),
    }.Select(r => (new Pattern(r.Item1), r.Item2)).ToArray();

    public static int? Classify(List<string> segs)
    {
        foreach (var (p, cat) in Rules)
            if (p.IsMatch(segs)) return cat;
        return null;
    }

    public static bool IsSystemRootFile(string name) =>
        name.Equals("pagefile.sys", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("hiberfil.sys", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("swapfile.sys", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("DumpStack.log", StringComparison.OrdinalIgnoreCase);
}

public static class Analyzer
{
    sealed record Check(string Title, Safety Level, string Advice, params string[] Patterns);

    static readonly Check[] DirChecks =
    {
        new("ถังขยะ (Recycle Bin)", Safety.Safe,
            "ไฟล์ที่ลบไปแล้วยังกินพื้นที่อยู่จนกว่าจะล้างถังขยะ\n\nวิธีล้าง: คลิกขวาที่ Recycle Bin บน Desktop → Empty Recycle Bin\n(ตรวจดูก่อนว่าไม่มีไฟล์ที่ต้องกู้คืน)",
            @"$Recycle.Bin"),
        new("ไฟล์ Temp ของผู้ใช้ (%TEMP%)", Safety.Safe,
            "ไฟล์ชั่วคราวที่โปรแกรมสร้างไว้แล้วไม่ได้ลบทิ้ง ลบได้ปลอดภัย\n\nวิธีล้าง: Settings → System → Storage → Temporary files → เลือก Temporary files → Remove files\nหรือกด Win+R พิมพ์ %temp% แล้วลบทั้งหมด (ไฟล์ที่โปรแกรมใช้อยู่จะลบไม่ได้ ให้กด Skip)",
            @"Users\*\AppData\Local\Temp"),
        new("ไฟล์ Temp ของ Windows", Safety.Safe,
            "ไฟล์ชั่วคราวของระบบ ลบได้ปลอดภัย\n\nวิธีล้าง: Settings → System → Storage → Temporary files หรือ Disk Cleanup (cleanmgr)",
            @"Windows\Temp"),
        new("แคช Windows Update", Safety.Safe,
            "ไฟล์อัปเดตที่ดาวน์โหลดมาแล้วติดตั้งเสร็จ\n\nวิธีล้าง: Settings → System → Storage → Temporary files → ติ๊ก Windows Update Cleanup\nหรือเปิด Disk Cleanup → Clean up system files",
            @"Windows\SoftwareDistribution\Download"),
        new("แคช Delivery Optimization", Safety.Safe,
            "แคชที่ Windows ใช้แชร์อัปเดตกับเครื่องอื่น\n\nวิธีล้าง: Settings → System → Storage → Temporary files → ติ๊ก Delivery Optimization Files",
            @"Windows\ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization",
            @"Windows\SoftwareDistribution\DeliveryOptimization"),
        new("Windows เวอร์ชันเก่า (Windows.old)", Safety.Safe,
            "สำรองของ Windows ก่อนอัปเกรดใหญ่ ปกติระบบจะลบเองหลัง 10 วัน\n\nวิธีลบ: Disk Cleanup → Clean up system files → ติ๊ก Previous Windows installation(s)\n⚠ ลบแล้วจะย้อนกลับไป Windows เวอร์ชันก่อนหน้าไม่ได้",
            @"Windows.old"),
        new("Crash dump / รายงานข้อผิดพลาด", Safety.Safe,
            "ไฟล์บันทึกตอนโปรแกรมหรือระบบล่ม ใช้สำหรับวิเคราะห์ปัญหาเท่านั้น ลบได้\n\nวิธีลบ: Disk Cleanup → Clean up system files → ติ๊ก System error memory dump files / Windows error reports",
            @"Users\*\AppData\Local\CrashDumps", @"Windows\Minidump", @"Windows\LiveKernelReports",
            @"ProgramData\Microsoft\Windows\WER"),
        new("แคชเบราว์เซอร์ (Chrome / Edge / Brave)", Safety.Safe,
            "รูปภาพ สคริปต์ และวิดีโอที่เบราว์เซอร์เก็บไว้ จะโหลดใหม่เองเมื่อเข้าเว็บ\n\nวิธีล้าง: เปิดเบราว์เซอร์ กด Ctrl+Shift+Delete → เลือก Cached images and files → All time\n(ถ้าจะลบโฟลเดอร์ตรงๆ ให้ปิดเบราว์เซอร์ก่อน)",
            @"Users\*\AppData\Local\Google\Chrome\User Data\*\Cache",
            @"Users\*\AppData\Local\Google\Chrome\User Data\*\Code Cache",
            @"Users\*\AppData\Local\Google\Chrome\User Data\*\GPUCache",
            @"Users\*\AppData\Local\Google\Chrome\User Data\*\Service Worker\CacheStorage",
            @"Users\*\AppData\Local\Microsoft\Edge\User Data\*\Cache",
            @"Users\*\AppData\Local\Microsoft\Edge\User Data\*\Code Cache",
            @"Users\*\AppData\Local\Microsoft\Edge\User Data\*\GPUCache",
            @"Users\*\AppData\Local\Microsoft\Edge\User Data\*\Service Worker\CacheStorage",
            @"Users\*\AppData\Local\BraveSoftware\Brave-Browser\User Data\*\Cache",
            @"Users\*\AppData\Local\BraveSoftware\Brave-Browser\User Data\*\Code Cache"),
        new("node_modules (โปรเจกต์ Node.js)", Safety.Safe,
            "dependency ของโปรเจกต์ JavaScript ติดตั้งใหม่ได้ด้วย npm install\n\nวิธีลบ: ลบ node_modules ของโปรเจกต์ที่ไม่ได้ทำแล้ว (คลิกขวาที่ path ด้านขวา → ย้ายไป Recycle Bin)\nหรือรัน  npx npkill  เพื่อเลือกลบทีละโปรเจกต์",
            @"**\node_modules"),
        new("แคช npm / yarn / pnpm", Safety.Safe,
            "แพ็กเกจที่เคยดาวน์โหลด เก็บไว้ให้ติดตั้งเร็วขึ้น\n\nวิธีล้าง:\n  npm cache clean --force\n  yarn cache clean\n  pnpm store prune",
            @"Users\*\AppData\Local\npm-cache", @"Users\*\AppData\Roaming\npm-cache",
            @"Users\*\AppData\Local\Yarn\Cache", @"Users\*\AppData\Local\pnpm-cache", @"Users\*\AppData\Local\pnpm\store"),
        new("แคช NuGet (.NET)", Safety.Safe,
            "แพ็กเกจ NuGet ที่ดาวน์โหลดไว้ทุกเวอร์ชัน จะโหลดใหม่ตอน build\n\nวิธีล้าง:  dotnet nuget locals all --clear",
            @"Users\*\.nuget\packages"),
        new("แคช Gradle / Maven", Safety.Safe,
            "dependency ของ Java/Android ที่ดาวน์โหลดไว้ จะโหลดใหม่ตอน build\n\nวิธีล้าง: ปิด Android Studio / IDE ก่อน แล้วลบโฟลเดอร์ .gradle\\caches และ .m2\\repository",
            @"Users\*\.gradle\caches", @"Users\*\.m2\repository"),
        new("แคช pip / Composer", Safety.Safe,
            "แพ็กเกจ Python / PHP ที่เคยดาวน์โหลด\n\nวิธีล้าง:\n  pip cache purge\n  composer clear-cache",
            @"Users\*\AppData\Local\pip\cache", @"Users\*\AppData\Local\Composer", @"Users\*\AppData\Roaming\Composer"),
        new("Downloads", Safety.Caution,
            "ไฟล์ที่ดาวน์โหลดมา มักมีไฟล์ติดตั้ง ZIP หรือวิดีโอที่ไม่ได้ใช้แล้วค้างอยู่\n\nแนะนำ: ไปที่แท็บ \"ไฟล์ใหญ่สุด\" แล้วพิมพ์ Downloads ในช่องค้นหา เพื่อดูว่าไฟล์ไหนใหญ่และไม่ได้เปิดมานาน",
            @"Users\*\Downloads"),
        new("Docker Desktop", Safety.Caution,
            "image / container / volume ของ Docker อยู่ในไฟล์ดิสก์เสมือน (.vhdx) ซึ่งไม่หดตัวเองแม้จะลบของข้างในแล้ว\n\nวิธีลด:\n1) docker system prune -a --volumes   (ลบ image/container/volume ที่ไม่ใช้ ⚠ ตรวจก่อน)\n2) ปิด Docker Desktop แล้วรัน  wsl --shutdown\n3) Docker Desktop → Troubleshoot → Clean / Purge data หรือ compact ไฟล์ .vhdx ด้วย diskpart (select vdisk file=\"...\" → compact vdisk)",
            @"Users\*\AppData\Local\Docker", @"ProgramData\DockerDesktop"),
        new("WSL (Linux บน Windows)", Safety.Caution,
            "ไฟล์ทั้งหมดของ Linux ใน WSL อยู่ในไฟล์ ext4.vhdx ซึ่งโตขึ้นได้แต่ไม่หดเอง\n\nวิธีลด:\n1) ลบไฟล์ที่ไม่ใช้ใน Linux ก่อน\n2) wsl --shutdown\n3) wsl --manage <ชื่อ distro> --set-sparse true  (WSL รุ่นใหม่)\n   หรือ diskpart → select vdisk file=\"...\\ext4.vhdx\" → compact vdisk",
            @"Users\*\AppData\Local\Packages\*CanonicalGroup*", @"Users\*\AppData\Local\Packages\*Debian*",
            @"Users\*\AppData\Local\Packages\*KaliLinux*", @"Users\*\AppData\Local\wsl"),
        new("Android SDK / Emulator", Safety.Caution,
            "Emulator แต่ละตัวกินหลาย GB และ SDK มี system image หลายเวอร์ชัน\n\nวิธีลด: Android Studio → Device Manager → ลบ emulator ที่ไม่ใช้\nและ SDK Manager → ถอน system image / platform เวอร์ชันเก่า",
            @"Users\*\.android\avd", @"Users\*\AppData\Local\Android\Sdk"),
        new("Visual Studio / VS Code", Safety.Caution,
            "ProgramData\\Package Cache คือไฟล์ติดตั้งของ Visual Studio — ห้ามลบเอง ให้ใช้ Visual Studio Installer ถอน workload ที่ไม่ใช้\nแคชของ VS Code (Cache, CachedData, CachedExtensionVSIXs) ลบได้ตอนปิดโปรแกรม",
            @"ProgramData\Package Cache", @"Users\*\AppData\Local\Microsoft\VisualStudio",
            @"Users\*\AppData\Roaming\Code\Cache", @"Users\*\AppData\Roaming\Code\CachedData",
            @"Users\*\AppData\Roaming\Code\CachedExtensionVSIXs"),
        new("แอปแชท / ประชุม (LINE, Teams, Discord, Zoom)", Safety.Caution,
            "เก็บรูป วิดีโอ และไฟล์จากแชทไว้ในเครื่อง\n\nวิธีลด: ล้างแคชจากในแอป เช่น LINE → Settings → Chats → Delete data / Teams → Settings → ล้างแคช\n(ถ้าลบโฟลเดอร์ตรงๆ อาจทำให้รูป/ไฟล์ในแชทเก่าหาย)",
            @"Users\*\AppData\Local\LINE", @"Users\*\AppData\Roaming\discord",
            @"Users\*\AppData\Local\Packages\MSTeams_*", @"Users\*\AppData\Roaming\Microsoft\Teams",
            @"Users\*\AppData\Roaming\Zoom"),
        new("เกม (Steam / Epic / Xbox / Riot)", Safety.Caution,
            "เกมหนึ่งเกมอาจกิน 50–150 GB\n\nวิธีลด: ถอนเกมที่ไม่ได้เล่นผ่าน launcher ของแต่ละเจ้า (Steam / Epic / Xbox app)\nอย่าลบโฟลเดอร์เกมตรงๆ เพราะ launcher จะยังคิดว่าติดตั้งอยู่",
            @"**\steamapps\common", @"**\Epic Games", @"XboxGames", @"**\Riot Games"),
        new("Virtual Machine (VirtualBox / Hyper-V / VMware)", Safety.Caution,
            "ไฟล์ดิสก์ของ VM และ snapshot\n\nวิธีลด: ลบ VM หรือ snapshot ที่ไม่ใช้จากในโปรแกรม VM (อย่าลบไฟล์ .vhdx/.vmdk ตรงๆ ถ้ายังมี snapshot)",
            @"**\VirtualBox VMs", @"**\Virtual Machines", @"**\Virtual Hard Disks"),
        new("OneDrive / Cloud sync", Safety.Caution,
            "ไฟล์ที่ sync ลงเครื่อง (ไฟล์ที่อยู่แต่บนคลาวด์ไม่ได้นับ)\n\nวิธีลด: เปิด OneDrive → Settings → Files On-Demand แล้วคลิกขวาโฟลเดอร์ใหญ่ → Free up space\nไฟล์ยังอยู่บนคลาวด์ จะโหลดกลับมาเมื่อเปิด",
            @"Users\*\OneDrive*", @"Users\*\Dropbox", @"Users\*\Google Drive"),
        new("Windows component store (WinSxS)", Safety.Danger,
            "⛔ ห้ามลบเอง — เก็บส่วนประกอบของ Windows\nขนาดที่เห็นมักเกินจริง เพราะไฟล์จำนวนมากเป็น hardlink ที่ใช้ร่วมกับ C:\\Windows\\System32\n\nวิธีลดอย่างถูกต้อง (เปิด CMD แบบ Admin):\n  DISM /Online /Cleanup-Image /StartComponentCleanup",
            @"Windows\WinSxS"),
        new("Windows Installer cache", Safety.Danger,
            "⛔ ห้ามลบเอง — ใช้ตอนถอน ซ่อม หรืออัปเดตโปรแกรม ถ้าลบจะถอนโปรแกรมไม่ได้",
            @"Windows\Installer"),
    };

    sealed record FileCheck(string Title, Safety Level, string Advice, string Pattern);

    static readonly FileCheck[] FileChecks =
    {
        new("ไฟล์ Hibernate (hiberfil.sys)", Safety.Caution,
            "ใช้สำหรับโหมด Hibernate และ Fast Startup ขนาดประมาณ 40–75% ของ RAM\n\nถ้าไม่ใช้ Hibernate: เปิด CMD แบบ Admin แล้วพิมพ์\n  powercfg /h off            (ปิด ไฟล์จะหายไปทันที)\n  powercfg /h /type reduced  (ลดขนาด แต่ยังใช้ Fast Startup ได้)",
            "hiberfil.sys"),
        new("Virtual memory (pagefile.sys)", Safety.Danger,
            "⛔ ห้ามลบ — Windows ใช้เป็นหน่วยความจำสำรอง\nถ้าใหญ่ผิดปกติ ปรับได้ที่: System → About → Advanced system settings → Performance Settings → Advanced → Virtual memory\n(ไม่แนะนำให้ปิด)",
            "pagefile.sys"),
        new("swapfile.sys", Safety.Danger, "⛔ ห้ามลบ — ระบบจัดการเอง ใช้กับแอปจาก Microsoft Store", "swapfile.sys"),
        new("Memory dump (MEMORY.DMP)", Safety.Safe,
            "ไฟล์บันทึกหน่วยความจำตอนจอฟ้า (BSOD) ใช้วิเคราะห์ปัญหาเท่านั้น ลบได้\n\nวิธีลบ: Disk Cleanup → Clean up system files → ติ๊ก System error memory dump files",
            @"Windows\MEMORY.DMP"),
    };

    public static List<string> BaseSegments(string rootPath)
    {
        var root = Path.GetPathRoot(rootPath) ?? "";
        var rest = rootPath.Substring(root.Length);
        return rest.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    public static AnalysisResult Run(ScanResult r, List<string>? baseOverride = null)
    {
        var a = new AnalysisResult();
        var baseSegs = baseOverride ?? BaseSegments(r.RootPath);
        bool driveRoot = r.IsDriveRoot || (baseOverride != null && baseOverride.Count == 0);

        // ---------- usage categories ----------
        int rootCat = UsageCats.Other;
        for (int k = 1; k <= baseSegs.Count; k++)
            if (UsageCats.Classify(baseSegs.Take(k).ToList()) is int c) rootCat = c;

        var rootUsage = new UsageRoot { Node = r.Root, Cat = rootCat };
        a.UsageRoots.Add(rootUsage);
        if (driveRoot)
            a.SysFiles = r.RootFiles.Where(f => UsageCats.IsSystemRootFile(Path.GetFileName(f.Path))).ToList();
        WalkUsage(r.Root, new List<string>(baseSegs), rootCat, rootUsage, a);

        // ---------- insights ----------
        var pats = DirChecks.Select(c => c.Patterns.Select(p => new Pattern(p)).ToArray()).ToArray();
        var ins = DirChecks.Select(c => new Insight { Title = c.Title, Level = c.Level, Advice = c.Advice }).ToArray();
        var segs = new List<string>(baseSegs);
        CheckNode(r.Root, segs, 0UL, pats, ins);
        WalkChecks(r.Root, segs, 0UL, pats, ins);
        foreach (var i in ins) i.Items = i.Items.OrderByDescending(x => x.Size).ToList();
        a.Insights.AddRange(ins.Where(i => i.Size > 0));

        var files = r.RootFiles.Concat(r.TopFiles).GroupBy(f => f.Path, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();
        foreach (var fc in FileChecks)
        {
            var p = new Pattern(fc.Pattern);
            var hits = files.Where(f =>
            {
                var rel = Path.GetRelativePath(r.RootPath, f.Path);
                var s = baseSegs.Concat(rel.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries)).ToList();
                return p.IsMatch(s);
            }).ToList();
            if (hits.Count == 0) continue;
            a.Insights.Add(new Insight
            {
                Title = fc.Title, Level = fc.Level, Advice = fc.Advice,
                Size = hits.Sum(h => h.Size),
                Items = hits.Select(h => new PathRef(h.Path, false, h.Size)).ToList(),
            });
        }

        a.Insights.Sort((x, y) => y.Size.CompareTo(x.Size));

        if (r.IsDriveRoot && r.Unaccounted > 1L << 30)
        {
            a.Insights.Add(new Insight
            {
                Title = "พื้นที่ที่สแกนมองไม่เห็น", Level = Safety.Info, Size = r.Unaccounted,
                Advice = "พื้นที่ที่ Windows บอกว่าใช้ไป แต่การสแกนหาไฟล์ไม่เจอ ส่วนใหญ่มาจาก:\n" +
                         "• System Restore / Shadow Copy ใน System Volume Information\n" +
                         "• โฟลเดอร์ที่ไม่มีสิทธิ์เข้า (ลองกดปุ่ม \"รันแบบ Admin\" แล้วสแกนใหม่)\n" +
                         "• ขนาดจริงบนดิสก์ (cluster) ที่ใหญ่กว่าขนาดไฟล์ เมื่อมีไฟล์เล็กจำนวนมาก\n\n" +
                         "เช็คขนาด Shadow Copy (CMD แบบ Admin):  vssadmin list shadowstorage\n" +
                         "ลดขนาด: ค้นหา \"Create a restore point\" → เลือกไดรฟ์ → Configure → ปรับ Max Usage\n" +
                         "หรือ Disk Cleanup → Clean up system files → More Options → System Restore and Shadow Copies → Clean up",
            });
        }
        if (r.DeniedDirs > 0)
        {
            a.Insights.Add(new Insight
            {
                Title = $"โฟลเดอร์ที่เข้าไม่ได้ {r.DeniedDirs:N0} แห่ง", Level = Safety.Info,
                Advice = "ไม่มีสิทธิ์อ่านโฟลเดอร์เหล่านี้ จึงไม่ได้นับขนาด\nกดปุ่ม \"รันแบบ Admin\" ด้านบนแล้วสแกนใหม่เพื่อให้ตัวเลขครบขึ้น",
                Items = CollectDenied(r.Root).Take(500).Select(n => new PathRef(n.FullPath, true, 0, n)).ToList(),
            });
        }
        if (r.CloudOnlyFiles > 0)
        {
            a.Insights.Add(new Insight
            {
                Title = $"ไฟล์ที่อยู่บนคลาวด์อย่างเดียว {r.CloudOnlyFiles:N0} ไฟล์ ({Fmt.Size(r.CloudOnlyBytes)})", Level = Safety.Info,
                Advice = "ไฟล์ OneDrive แบบ Files On-Demand ที่ยังไม่ได้ดาวน์โหลด ไม่ได้กินพื้นที่ในเครื่อง จึงไม่นับรวม",
            });
        }

        // ---------- file types ----------
        var types = new Dictionary<string, TypeStat>();
        foreach (var (ext, st) in r.Extensions)
        {
            var t = FileTypes.Of(ext);
            if (!types.TryGetValue(t, out var ts)) types[t] = ts = new TypeStat { Name = t };
            ts.Size += st.Size;
            ts.Count += st.Count;
            a.Exts.Add(new ExtRow { Ext = ext.Length == 0 ? "(ไม่มี)" : ext, Type = t, Size = st.Size, Count = st.Count });
        }
        a.Types = types.Values.OrderByDescending(t => t.Size).ToList();
        a.Exts.Sort((x, y) => y.Size.CompareTo(x.Size));
        return a;
    }

    static IEnumerable<DirNode> CollectDenied(DirNode n)
    {
        var stack = new Stack<DirNode>();
        stack.Push(n);
        while (stack.Count > 0)
        {
            var x = stack.Pop();
            if (x.AccessDenied) yield return x;
            foreach (var c in x.Children) stack.Push(c);
        }
    }

    static void WalkUsage(DirNode n, List<string> segs, int cat, UsageRoot cur, AnalysisResult a)
    {
        n.Usage = (byte)cat;
        long own = n.OwnSize;
        if (n.Parent == null && a.SysFiles.Count > 0)
        {
            long sys = a.SysFiles.Sum(f => f.Size);
            a.Usage[UsageCats.SysFiles] += sys;
            own -= sys;
        }
        a.Usage[cat] += own;
        cur.Size += own;

        foreach (var c in n.Children)
        {
            segs.Add(c.Name);
            var cc = UsageCats.Classify(segs);
            if (cc is int x && x != cat)
            {
                var nr = new UsageRoot { Node = c, Cat = x };
                a.UsageRoots.Add(nr);
                WalkUsage(c, segs, x, nr, a);
            }
            else
            {
                WalkUsage(c, segs, cat, cur, a);
            }
            segs.RemoveAt(segs.Count - 1);
        }
    }

    static ulong CheckNode(DirNode n, List<string> segs, ulong mask, Pattern[][] pats, Insight[] ins)
    {
        for (int i = 0; i < pats.Length; i++)
        {
            if ((mask & (1UL << i)) != 0) continue;
            foreach (var p in pats[i])
            {
                if (!p.IsMatch(segs)) continue;
                ins[i].Size += n.TotalSize;
                ins[i].Items.Add(new PathRef(n.FullPath, true, n.TotalSize, n));
                mask |= 1UL << i;
                break;
            }
        }
        return mask;
    }

    static void WalkChecks(DirNode n, List<string> segs, ulong mask, Pattern[][] pats, Insight[] ins)
    {
        foreach (var c in n.Children)
        {
            segs.Add(c.Name);
            var m = CheckNode(c, segs, mask, pats, ins);
            WalkChecks(c, segs, m, pats, ins);
            segs.RemoveAt(segs.Count - 1);
        }
    }
}
