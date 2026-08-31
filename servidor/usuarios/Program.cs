using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace BackupFdbUsuarios
{
    static class Program
    {
        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [STAThread]
        static int Main(string[] args)
        {
            try
            {
                if (args != null && args.Length > 0)
                {
                    var a0 = args[0];
                    if (string.Equals(a0, "--check", StringComparison.OrdinalIgnoreCase))
                        return CmdCheck();
                    if (string.Equals(a0, "--init", StringComparison.OrdinalIgnoreCase))
                        return CmdInit();
                }

                var hwnd = GetConsoleWindow();
                if (hwnd != IntPtr.Zero) ShowWindow(hwnd, 0);

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                UsersDb.EnsureCreated();
                using (var login = new LoginForm())
                {
                    if (login.ShowDialog() != DialogResult.OK)
                        return 0;
                    Application.Run(new UsuariosForm(login.Email));
                }
                return 0;
            }
            catch (Exception ex)
            {
                try
                {
                    Console.Error.WriteLine(ex.Message);
                }
                catch { }
                if (args == null || args.Length == 0)
                    MessageBox.Show(ex.Message, "Usuarios de download", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return 1;
            }
        }

        static int CmdInit()
        {
            UsersDb.EnsureCreated();
            Console.WriteLine("OK " + UsersDb.DbPath);
            return 0;
        }

        static int CmdCheck()
        {
            UsersDb.EnsureCreated();
            var email = Console.In.ReadLine();
            var pass = Console.In.ReadLine();
            if (email == null) email = "";
            if (pass == null) pass = "";
            var u = UsersDb.Authenticate(email.Trim(), pass);
            if (u == null)
            {
                Console.WriteLine("FAIL");
                return 1;
            }
            Console.WriteLine(u.IsSuperuser ? "OK admin" : "OK download");
            return 0;
        }
    }
}
