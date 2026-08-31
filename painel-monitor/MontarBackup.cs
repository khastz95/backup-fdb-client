using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace BackupFdbPainel
{
    public static class MontarBackup
    {
        public static string FindNbackup()
        {
            var list = new List<string>();
            var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            list.Add(@"C:\Program Files\Firebird\Firebird_3_0\nbackup.exe");
            list.Add(@"C:\Program Files (x86)\Firebird\Firebird_3_0\nbackup.exe");
            if (!string.IsNullOrEmpty(pf)) list.Add(Path.Combine(pf, @"Firebird\Firebird_3_0\nbackup.exe"));
            if (!string.IsNullOrEmpty(pf86)) list.Add(Path.Combine(pf86, @"Firebird\Firebird_3_0\nbackup.exe"));
            foreach (var p in list)
            {
                if (File.Exists(p)) return Path.GetFullPath(p);
            }
            throw new Exception("nbackup.exe nao encontrado. Instale o Firebird 3.0 neste computador para montar o .fdb.");
        }

        public static string LocalPath(string destRoot, RemoteItem f)
        {
            var rel = (f.RelDir ?? "").Replace('/', Path.DirectorySeparatorChar);
            if (string.IsNullOrEmpty(rel)) return Path.Combine(destRoot, f.Name);
            return Path.Combine(destRoot, rel, f.Name);
        }

        public static List<string> CadeiaLocal(string pastaCliente, string pastaCadeia)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(pastaCliente) || !Directory.Exists(pastaCliente)) return list;
            var dir = pastaCliente;
            if (!string.IsNullOrWhiteSpace(pastaCadeia))
            {
                var sub = Path.Combine(pastaCliente, pastaCadeia.Replace('/', Path.DirectorySeparatorChar));
                if (Directory.Exists(sub)) dir = sub;
            }
            string completo = null;
            var incs = new List<string>();
            foreach (var f in Directory.GetFiles(dir, "*.nbk"))
            {
                var name = Path.GetFileName(f);
                if (name.StartsWith("Completo_", StringComparison.OrdinalIgnoreCase))
                {
                    if (completo == null || File.GetLastWriteTimeUtc(f) < File.GetLastWriteTimeUtc(completo))
                        completo = f;
                }
                else if (name.StartsWith("Incremental_", StringComparison.OrdinalIgnoreCase))
                    incs.Add(f);
            }
            if (completo == null && string.IsNullOrWhiteSpace(pastaCadeia))
            {
                string melhor = null;
                var melhorData = DateTime.MinValue;
                foreach (var sub in Directory.GetDirectories(pastaCliente))
                {
                    var n = Path.GetFileName(sub);
                    if (!n.StartsWith("Completo_", StringComparison.OrdinalIgnoreCase)
                        && !n.StartsWith("cadeia_", StringComparison.OrdinalIgnoreCase))
                        continue;
                    var t = Directory.GetLastWriteTimeUtc(sub);
                    if (t >= melhorData)
                    {
                        melhorData = t;
                        melhor = n;
                    }
                }
                if (melhor != null) return CadeiaLocal(pastaCliente, melhor);
            }
            if (completo == null) return list;
            incs.Sort((a, b) => File.GetLastWriteTimeUtc(a).CompareTo(File.GetLastWriteTimeUtc(b)));
            list.Add(completo);
            list.AddRange(incs);
            return list;
        }

        public static void Restaurar(IList<string> arquivos, string destFdb, string dbUser, string dbPassword)
        {
            if (arquivos == null || arquivos.Count == 0)
                throw new Exception("Nenhum arquivo .nbk para montar.");
            var level0 = arquivos[0];
            if (!File.Exists(level0))
                throw new Exception("Completo nao encontrado:\n" + level0);
            destFdb = Path.GetFullPath(destFdb ?? "");
            if (!destFdb.EndsWith(".fdb", StringComparison.OrdinalIgnoreCase) && !destFdb.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase))
                destFdb += ".fdb";
            if (File.Exists(destFdb))
                throw new Exception("O destino ja existe:\n" + destFdb + "\n\nEscolha outro nome. O nbackup nao grava em cima de um .fdb que ja existe.");
            var destDir = Path.GetDirectoryName(destFdb);
            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                Directory.CreateDirectory(destDir);

            var used = new List<string>();
            foreach (var f in arquivos)
            {
                if (!string.IsNullOrWhiteSpace(f) && File.Exists(f)) used.Add(f);
            }
            if (used.Count == 0) throw new Exception("Nenhum .nbk encontrado neste computador.");

            var exe = FindNbackup();
            while (true)
            {
                try
                {
                    Invoke(exe, destFdb, used, dbUser, dbPassword);
                    return;
                }
                catch (Exception ex)
                {
                    if (!IsBrokenChain(ex) || used.Count <= 1) throw;
                    if (File.Exists(destFdb))
                    {
                        try { File.Delete(destFdb); } catch { }
                    }
                    used.RemoveAt(used.Count - 1);
                }
            }
        }

        static bool IsBrokenChain(Exception ex)
        {
            var m = ex == null ? "" : (ex.Message ?? "");
            return m.IndexOf("Wrong order", StringComparison.OrdinalIgnoreCase) >= 0
                || m.IndexOf("invalid incremental", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static void Invoke(string exe, string destFdb, IList<string> files, string dbUser, string dbPassword)
        {
            var pwd = Path.Combine(Path.GetTempPath(), "fbk-pwd-" + Guid.NewGuid().ToString("N") + ".tmp");
            File.WriteAllText(pwd, dbPassword ?? "", new UTF8Encoding(false));
            try
            {
                var args = new StringBuilder();
                if (!string.IsNullOrWhiteSpace(dbUser))
                    args.Append("-USER ").Append(dbUser).Append(" ");
                if (!string.IsNullOrEmpty(dbPassword))
                    args.Append("-FETCH_PASSWORD \"").Append(pwd).Append("\" ");
                args.Append("-R \"").Append(destFdb).Append("\"");
                foreach (var f in files)
                    args.Append(" \"").Append(f).Append("\"");
                var p = new Process();
                p.StartInfo.FileName = exe;
                p.StartInfo.Arguments = args.ToString();
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.RedirectStandardError = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.WorkingDirectory = Path.GetDirectoryName(files[0]) ?? "";
                p.Start();
                var err = p.StandardError.ReadToEnd();
                p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                if (p.ExitCode != 0)
                    throw new Exception("nbackup retornou " + p.ExitCode + (string.IsNullOrWhiteSpace(err) ? "" : ": " + err.Trim()));
                if (!File.Exists(destFdb))
                    throw new Exception("nbackup terminou, mas o .fdb nao foi criado.");
            }
            finally
            {
                try { File.WriteAllText(pwd, new string('x', 64)); File.Delete(pwd); }
                catch { }
            }
        }
    }
}
