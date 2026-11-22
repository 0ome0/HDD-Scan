using HddScan.UI;

namespace HddScan;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
            MessageBox.Show(e.Exception.ToString(), "HDD-Scan — เกิดข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
        Application.Run(new MainForm(args.Length > 0 ? args[0] : null));
    }
}
