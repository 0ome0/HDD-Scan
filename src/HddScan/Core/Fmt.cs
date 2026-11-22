namespace HddScan.Core;

public static class Fmt
{
    static readonly string[] Units = { "B", "KB", "MB", "GB", "TB", "PB" };

    public static string Size(long bytes)
    {
        if (bytes < 0) return "-" + Size(-bytes);
        if (bytes < 1024) return $"{bytes} B";
        double v = bytes;
        int i = 0;
        while (v >= 1024 && i < Units.Length - 1) { v /= 1024; i++; }
        return v >= 100 ? $"{v:0} {Units[i]}" : $"{v:0.0} {Units[i]}";
    }

    public static string SignedSize(long bytes) => bytes > 0 ? "+" + Size(bytes) : Size(bytes);

    public static string Pct(double fraction) => $"{fraction * 100:0.0}%";

    public static string Num(long n) => n.ToString("N0");

    public static string Date(DateTime utc) =>
        utc == default ? "" : utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
}
