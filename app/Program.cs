using System;
using System.Threading;
using System.Windows.Forms;

namespace BackupFdbCliente
{
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => ShowFatal(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => ShowFatal(e.ExceptionObject as Exception);

            var mode = "";
            foreach (var a in args)
            {
                if (string.Equals(a, "--backup", StringComparison.OrdinalIgnoreCase)) mode = "backup";
                else if (string.Equals(a, "--local", StringComparison.OrdinalIgnoreCase)) mode = "local";
                else if (string.Equals(a, "--test", StringComparison.OrdinalIgnoreCase)) mode = "test";
                else if (string.Equals(a, "--tray", StringComparison.OrdinalIgnoreCase)) mode = "tray";
            }

            if (mode == "backup" || mode == "local" || mode == "test")
                return RunSilent(mode);

            bool created;
            using (var mutex = new Mutex(true, @"Local\BackupFdbClienteUi", out created))
            {
                var show = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\BackupFdbClienteShow");
                if (!created)
                {
                    if (mode != "tray") show.Set();
                    return 0;
                }
                Application.Run(new MainForm(mode == "tray", show));
                return 0;
            }
        }

        static int RunSilent(string mode)
        {
            try
            {
                var c = Ini.Load(AppPaths.ConfigFile);
                Engine.EnsureFolders(c);
                if (mode == "backup") Engine.RunBackup(c, c.EnviarFtp);
                else if (mode == "local") Engine.RunBackup(c, false);
                else Engine.RunTests(c);
                return 0;
            }
            catch (Exception ex)
            {
                Log.Write(ex.Message, "ERROR");
                StatusFile.Set("erro", ex.Message, -1);
                return 1;
            }
        }

        static void ShowFatal(Exception ex)
        {
            try
            {
                var msg = ex == null ? "Erro inesperado." : ex.Message;
                Log.Write(msg, "ERROR");
                MessageBox.Show(msg, "Backup do sistema", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch { }
        }
    }
}
