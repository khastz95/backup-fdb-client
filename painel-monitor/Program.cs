using System;
using System.Windows.Forms;

namespace BackupFdbPainel
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += (s, e) =>
            {
                MessageBox.Show(e.Exception.Message, "Painel de backups", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            var cfg = PainelConfig.Load();
            if (!cfg.HasConta)
            {
                using (var login = new ContaPainelForm(cfg))
                {
                    login.StartPosition = FormStartPosition.CenterScreen;
                    if (login.ShowDialog() != DialogResult.OK) return;
                    cfg = login.Result;
                    cfg.Save();
                }
            }

            Application.Run(new PainelForm(cfg));
        }
    }
}
