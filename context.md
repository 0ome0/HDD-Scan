# HDD-Scan — Context

> เอกสารรวมรายละเอียดโปรเจกต์ทั้งหมด สำหรับใช้ทำงานต่อ (คน หรือ AI) อัปเดตล่าสุด 2026-11-22

## 1. ภาพรวม

| | |
|---|---|
| ชื่อ | HDD-Scan |
| เป้าหมาย | แอป Windows ที่บอกว่าพื้นที่ไดรฟ์เต็มเพราะอะไร: โฟลเดอร์ไหน ใช้ไปเท่าไหร่ และมาจากการใช้งานแบบไหน |
| เจ้าของ | Devdee (Akarapon N.) |
| สถานะ | v1.0.0 — build แล้ว ยังไม่ได้ทดสอบหน้าจอบน Windows จริง |
| เทคโนโลยี | C# / .NET 8 / WinForms (`net8.0-windows`) |
| Repo | https://github.com/0ome0/HDD-Scan (private, branch `main`) |
| โฟลเดอร์ในเครื่อง | `C:\Project\HDD-Scan` (มี `HddScan.exe` ที่ build แล้ว; ยังไม่ได้ผูก git) |
| ผลลัพธ์ build | `HddScan.exe` ไฟล์เดียว self-contained ประมาณ 70 MB (ไม่ต้องลง .NET) — ไม่ได้ commit ขึ้น git |

## 2. ฟีเจอร์ (v1.0.0)

### หน้าจอหลัก
- เลือกไดรฟ์ (แสดงใช้ไป/ทั้งหมด/% เต็ม, ค่าเริ่มต้นคือไดรฟ์ระบบ) หรือเลือกโฟลเดอร์
- ปุ่ม สแกน / ยกเลิก / Export CSV / รันแบบ Admin (ซ่อนเมื่อรันเป็น Admin อยู่แล้ว)
- แถบไดรฟ์: น้ำเงิน = สแกนเจอ, ส้ม = พื้นที่ที่สแกนมองไม่เห็น, เทา = ว่าง
- Status bar แสดงความคืบหน้าระหว่างสแกน (จำนวนไฟล์/โฟลเดอร์/ไบต์ + path ปัจจุบัน) และ progress % เมื่อสแกนทั้งไดรฟ์

### แท็บ
1. **โฟลเดอร์** — TreeView (lazy-load, สูงสุด 500 ลูกต่อโหนด) + รายการโฟลเดอร์ย่อยและไฟล์ในโฟลเดอร์ที่เลือก พร้อมแถบสัดส่วน, จำนวนไฟล์, วันที่แก้ไขล่าสุด, สีหมวดการใช้งาน
2. **Treemap** — squarified treemap ลึก 2 ชั้น สีตามหมวดการใช้งาน คลิกซ้ายเข้าไปดู คลิกขวาย้อนขึ้น มี tooltip และ legend
3. **ตัวการที่พบบ่อย** — ตรวจจุดกินพื้นที่ที่พบบ่อยประมาณ 30 รายการ พร้อมระดับ ✔ ลบได้ / ⚠ ระวัง / ⛔ ห้ามลบเอง / ℹ ข้อมูล, คำแนะนำวิธีจัดการ และรายการตำแหน่งที่พบ สรุปยอด "ล้างได้ปลอดภัยรวม"
4. **ตามการใช้งาน** — แยกพื้นที่ 16 หมวด (Windows, ไฟล์ระบบ, Program Files, ProgramData, เกม, Dev, Docker/WSL/VM, เบราว์เซอร์, Temp, Recycle Bin, Windows.old, ไฟล์ส่วนตัว, AppData, ผู้ใช้อื่นๆ, Cloud sync, อื่นๆ) คลิกหมวดเพื่อดูว่าอยู่ที่โฟลเดอร์ไหน
5. **ประเภทไฟล์** — 15 กลุ่ม (วิดีโอ, รูป, เสียง, เอกสาร, โปรแกรม/ISO, บีบอัด, VM, ฐานข้อมูล/อีเมล, ไลบรารี, โค้ด, เกม, Log/Temp, ฟอนต์, ไม่มีนามสกุล, อื่นๆ) + ตารางตามนามสกุล ดับเบิลคลิกเพื่อกรองในแท็บไฟล์ใหญ่สุด
6. **ไฟล์ใหญ่สุด** — 1,000 อันดับ กรองตามขนาดขั้นต่ำ, อายุ (ไม่ได้แก้ไขเกิน 3/6 เดือน, 1/2 ปี), ประเภท, คำค้น
7. **เทียบครั้งก่อน** — เทียบกับผลสแกนครั้งก่อนของตำแหน่งเดียวกัน แสดงโฟลเดอร์ที่เปลี่ยน ≥ 20 MB และมีตัวเลือกซ่อนโฟลเดอร์แม่ที่ซ้ำ

### Action (คลิกขวา)
- เปิดใน Explorer (ไฟล์จะ `/select`)
- ไปที่โฟลเดอร์ในแท็บโฟลเดอร์
- คัดลอก path
- ย้ายไป Recycle Bin (ยืนยันก่อน, ห้ามลบ root ของไดรฟ์/root ที่สแกน/ไฟล์ใต้ `C:\Windows`) แล้วอัปเดตขนาดในต้นไม้ทันที

### Export CSV
UTF-8 BOM (เปิดใน Excel ภาษาไทยได้) มี 5 ส่วน: ตัวการที่พบบ่อย, ตามการใช้งาน, ประเภทไฟล์, โฟลเดอร์ (ลึก ≤ 4 ชั้น และ ≥ 100 MB), ไฟล์ใหญ่สุด

## 3. สถาปัตยกรรม

```
src/HddScan/
  Program.cs              entry point; รับ args[0] = path ที่จะสแกนอัตโนมัติ (ใช้ตอน restart เป็น Admin)
  HddScan.csproj          net8.0-windows, WinForms, PerMonitorV2 DPI, EnableWindowsTargeting
  app.manifest            asInvoker, longPathAware
  Core/                   (ไม่พึ่ง WinForms — ทดสอบบน Linux ได้)
    Models.cs             DirNode, FileItem, PathRef, ExtStat, ScanResult
    Scanner.cs            สแกนแบบขนาน
    Analysis.cs           Pattern, FileTypes, UsageCats, Analyzer (insights)
    History.cs            Snapshot, เก็บ/โหลด/เทียบผลสแกน
    Fmt.cs                format ขนาด/วันที่/%
  UI/
    MainForm.cs           หน้าจอหลักทั้งหมด (สร้าง UI ด้วยโค้ด ไม่มี Designer)
    BarListView.cs        ListView owner-draw: แถบในเซลล์, swatch สี, เรียงตามคอลัมน์ (Cell record เก็บ sort key)
    TreemapView.cs        squarified treemap + hit test + tooltip
    DriveBar.cs           แถบสัดส่วนไดรฟ์
```

### Scanner (Core/Scanner.cs)
- ใช้ `FileSystemEnumerable<Entry>` (NtQueryDirectoryFile) ไม่สร้าง `FileInfo` ทำให้เร็วและประหยัด memory
- ขนานด้วย `Parallel.ForEach` ที่ความลึก < 4 ลึกกว่านั้นทำแบบลำดับ
- **ข้าม reparse point** (junction/symlink) เพื่อไม่นับซ้ำ → นับไว้ใน `LinkDirs`
- **ไม่นับไฟล์ cloud-only** (attribute `RECALL_ON_DATA_ACCESS` 0x400000, `RECALL_ON_OPEN` 0x40000, `Offline`) → แยกไว้ใน `CloudOnlyBytes/Files`
- โฟลเดอร์ที่เข้าไม่ได้ (UnauthorizedAccess/IO/Security) → `AccessDenied = true`, นับใน `DeniedDirs`
- top-N ไฟล์ใหญ่: min-heap (`PriorityQueue`) แยกต่อ thread (`ThreadLocal`) แล้วรวมตอนจบ (N = 1000)
- สถิตินามสกุล: `Dictionary` ต่อ thread แล้วรวม
- เก็บไฟล์ที่อยู่ตรง root (`RootFiles`) เพื่อหา pagefile/hiberfil/swapfile
- รวมขนาดจากล่างขึ้นบน และเรียงลูกจากใหญ่ไปเล็กตอนจบแต่ละโฟลเดอร์
- `Unaccounted` = (ใช้ไปของไดรฟ์) − (ที่สแกนเจอ) เฉพาะกรณีสแกนทั้งไดรฟ์
- ขนาดที่ใช้คือ logical length (ไม่ใช่ size on disk)

### Analysis (Core/Analysis.cs)
- `Pattern`: path relative กับ root ของไดรฟ์ เช่น `Users\*\AppData\Local\Temp` รองรับ `*`/`?` ต่อ segment และ `**` นำหน้า (match ที่ความลึกใดก็ได้ เช่น `**\node_modules`) — ไม่มี `**` ต้องยาวเท่ากันพอดี จึงเร็ว
- **Usage**: เดินต้นไม้จากบนลงล่าง ถ้าโฟลเดอร์ match กฎ จะเปลี่ยนหมวดสำหรับทั้ง subtree (กฎที่ลึกกว่าชนะ เช่น node_modules ใน Documents = Dev) นับเฉพาะ `OwnSize` ของแต่ละโหนด ผลรวมทุกหมวด = ขนาดที่สแกน (ทดสอบแล้ว) และเก็บ `UsageRoots` (โฟลเดอร์จุดเริ่มหมวด + ขนาดเฉพาะหมวดนั้น)
- **Insights**: DFS พร้อม bitmask ต่อ check — พอ match แล้วไม่นับซ้ำใน subtree ของ check เดียวกัน ส่วน file check (hiberfil.sys, pagefile.sys, swapfile.sys, Windows\MEMORY.DMP) ดูจาก RootFiles + TopFiles เพิ่ม insight ข้อมูล: พื้นที่มองไม่เห็น (> 1 GB), โฟลเดอร์ที่เข้าไม่ได้, ไฟล์ cloud-only
- `Run(result, baseOverride)` — `baseOverride` ใช้ตอนทดสอบบน Linux ให้ทำเหมือนสแกนจาก root ของไดรฟ์

### History (Core/History.cs)
- เก็บที่ `%LOCALAPPDATA%\HddScan\history\<root ที่แปลงเป็นชื่อไฟล์>\yyyyMMdd_HHmmss_<total>.json`
- เก็บเฉพาะโฟลเดอร์ลึก ≤ 6 ชั้นที่ ≥ 10 MB (path → TotalSize) เก็บล่าสุด 40 ไฟล์
- `Diff` = union ของ path, ตัดที่เปลี่ยน < minDelta; `Collapse` = ซ่อนแม่ถ้ามีลูกอธิบายการเปลี่ยน ≥ 90% (ทิศเดียวกัน)

### UI notes
- ฟอนต์ `Leelawadee UI 9.5` (รองรับไทย); ขนาด px คูณ `DeviceDpi/96` ผ่าน `S()`
- ไม่มี Designer — Dock order: เพิ่ม Fill ก่อน แล้ว Top จากล่างขึ้นบน
- `SplitContainer` ตั้ง `SplitterDistance` ตอน SizeChanged/HandleCreated ครั้งแรก (กัน exception ตอนขนาดยังเล็ก)
- งานหนัก (scan + analyze + history) อยู่ใน `Task.Run`; UI poll ความคืบหน้าทุก 250 ms
- รันแบบ Admin: `Process.Start` + `Verb = "runas"` ส่ง path ไปเป็น argument (path ที่ลงท้าย `\` ต้องเติม `\` อีกตัวก่อนใส่ quote)
- ลบไป Recycle Bin ใช้ `Microsoft.VisualBasic.FileIO.FileSystem` แล้ว `Analyzer.Run` ใหม่ และ `ShowResult(keepPath)`

## 4. Build / Run / Test

```bash
# รันจาก source (Windows, .NET 8 SDK)
cd src/HddScan && dotnet run -c Release

# exe ไฟล์เดียว self-contained (build จาก Linux ได้เพราะ EnableWindowsTargeting)
dotnet publish src/HddScan -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true -p:DebugType=none -o publish
```

ทดสอบ Core บน Linux: สร้าง console project `net8.0` ที่ `<Compile Include="src/HddScan/Core/*.cs" />` แล้วเรียก `Scanner.Scan` + `Analyzer.Run(r, new List<string>())` บนโฟลเดอร์จำลองแบบ Windows (Users/*/AppData/Local/Temp, node_modules, WinSxS, hiberfil.sys …)
ผลที่ยืนยันแล้ว: /usr 6.7 GB, 150k ไฟล์ สแกนใน 2.2 วินาที; ผลรวมหมวดการใช้งาน = ขนาดรวมพอดี; หมวดและ insight ถูกต้องบนโฟลเดอร์จำลอง

## 5. การตัดสินใจสำคัญ

- **WinForms แทน WPF** — เบา เร็ว build จาก Linux ได้ ไฟล์เดียวจบ
- **Self-contained exe** — ผู้ใช้ไม่ต้องลง .NET (แลกกับขนาด 70 MB)
- **ไม่ commit exe ขึ้น git** — เกิน 50 MB ที่ GitHub แนะนำ; ถ้าจะแจกให้ทำ GitHub Release แล้วอัปโหลดเอง
- **asInvoker + ปุ่มรันแบบ Admin** แทนการบังคับ Admin ทุกครั้ง
- **ไม่นับ cloud-only, ข้าม junction** เพื่อให้ตัวเลขตรงกับพื้นที่จริง
- **ห้ามลบใต้ C:\Windows จากแอป** — ให้ทำตามคำแนะนำ (Disk Cleanup / DISM / powercfg) แทน
- **ลบ = ส่ง Recycle Bin เท่านั้น** (ยังไม่มีลบถาวร) และเตือนว่ายังกินพื้นที่จนกว่าจะ Empty

## 6. ข้อจำกัดที่รู้อยู่

- ยังไม่ได้เปิดทดสอบหน้าจอบน Windows จริง (computer use เปิดแอปที่ไม่ได้ติดตั้งไม่ได้)
- ขนาดเป็น logical size ไม่ใช่ size on disk (ไฟล์ compressed/sparse/ไฟล์เล็กจำนวนมากอาจคลาด)
- WinSxS ดูใหญ่เกินจริงเพราะ hardlink (มีหมายเหตุใน insight)
- สแกนแบบ enumerate ปกติ ช้ากว่าการอ่าน MFT (WizTree) บนไดรฟ์ใหญ่มากๆ
- TreeView แสดงลูกสูงสุด 500 ต่อโหนด (ส่วนที่เหลือดูในรายการด้านขวา)
- Glyph บางตัว (✔ ⚠ ⛔ ▶ ⬆) ขึ้นกับ font fallback ของ GDI
- GitHub Release สร้างจาก session cloud ไม่ได้

## 7. งานต่อไป (Roadmap)

- [ ] ทดสอบบน Windows จริง (C: แบบปกติ และแบบ Admin) แล้วเก็บ bug UI
- [ ] ผูก `C:\Project\HDD-Scan` กับ repo (`git init` → `remote add` → `fetch` → `reset origin/main`)
- [ ] ทำ GitHub Release v1.0.0 พร้อมแนบ `HddScan.exe`
- [ ] สแกนเร็วด้วยการอ่าน NTFS MFT (ต้องใช้ Admin)
- [ ] แสดง size on disk (`GetCompressedFileSize`)
- [ ] หาไฟล์ซ้ำ (ขนาด → hash บางส่วน → hash เต็ม)
- [ ] ปุ่มล้าง Temp/แคชที่ปลอดภัยในคลิกเดียว
- [ ] แจ้งเตือนเมื่อพื้นที่เหลือต่ำกว่าที่ตั้งไว้ (tray app / scheduled scan)
- [ ] ไอคอนแอป + เซ็นโค้ด (ลดการเตือนจาก SmartScreen)

## 8. ประวัติ

- 2026-11-21 — วางแผนฟีเจอร์, เขียน Core + UI, ทดสอบ Core บน Linux, build exe ไปไว้ที่ `C:\Project\HDD-Scan`
- 2026-11-22 — สร้าง repo private `0ome0/HDD-Scan` และ push commit แรก (`34af736`), เขียน context.md, บันทึกลง Obsidian
