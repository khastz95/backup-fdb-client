using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace BackupFdbCliente
{
    public sealed class AppConfig
    {
        public string ConfigPath;
        public string Cnpj = "";
        public string Nome = "";
        public string GbakPath = "";
        public string Database = "";
        public string DbUser = "SYSDBA";
        public string DbPassword = "masterkey";
        public string LocalFolder = @"C:\BackupFirebird";
        public int DiasEntreFull = 7;
        public int ManterCadeias = 4;
        public string FtpHost = "";
        public int FtpPort = 9099;
        public string FtpUser = "backupclientes";
        public string FtpPassword = "";
        public string FtpPastaRemota = "E:/Backup2";
        public int FtpTimeoutSegundos = 3600;
        public int FtpTentativas = 3;
        public bool EnviarFtp = true;
        public string Hora = "23:00";
        public string NomeTarefa = "Backup Firebird Cliente";
        public string SenhaOperacao = "159357";

        public string ClientFolderName
        {
            get { return Naming.ClientFolder(Cnpj, Nome); }
        }

        public string WindowsBackupPath
        {
            get { return @"E:\Backup2\" + ClientFolderName; }
        }
    }

    public static class AppPaths
    {
        public static string Root
        {
            get { return AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\'); }
        }

        public static string ConfigFile
        {
            get { return Path.Combine(Root, "config.ini"); }
        }

        public static string LogDir
        {
            get { return Path.Combine(Root, "logs"); }
        }
    }

    public static class Log
    {
        public static event Action<string> LineWritten;

        public static void Write(string message, string level = "INFO")
        {
            try
            {
                if (!Directory.Exists(AppPaths.LogDir)) Directory.CreateDirectory(AppPaths.LogDir);
                var line = string.Format("[{0:yyyy-MM-dd HH:mm:ss}] [{1}] {2}", DateTime.Now, level, message);
                File.AppendAllText(Path.Combine(AppPaths.LogDir, "backup-" + DateTime.Now.ToString("yyyyMMdd") + ".log"), line + Environment.NewLine, Encoding.UTF8);
                var ev = LineWritten;
                if (ev != null) ev(line);
            }
            catch { }
        }
    }

    public static class StatusFile
    {
        static string PathName { get { return Path.Combine(AppPaths.LogDir, "status.json"); } }

        public static void Set(string phase, string message, int percent)
        {
            try
            {
                if (!Directory.Exists(AppPaths.LogDir)) Directory.CreateDirectory(AppPaths.LogDir);
                var json = "{\"time\":\"" + DateTime.Now.ToString("s") + "\",\"phase\":\"" + Esc(phase) + "\",\"message\":\"" + Esc(message) + "\",\"percent\":" + percent + "}";
                File.WriteAllText(PathName, json, new UTF8Encoding(false));
            }
            catch { }
        }

        static string Esc(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", " ").Replace("\n", " ");
        }
    }

    public static class Naming
    {
        public static string Digits(string value)
        {
            return Regex.Replace(value ?? "", @"\D", "");
        }

        public static string ClientFolder(string cnpj, string nome)
        {
            var d = Digits(cnpj);
            if (d.Length == 0) d = "CLIENTE";
            return d + "_" + Pascal(nome);
        }

        public static string Stamp()
        {
            return DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        }

        public static string CycleFolder(string stamp)
        {
            return "Completo_" + stamp;
        }

        public static string FileName(int level, string stamp)
        {
            return (level == 0 ? "Completo_" : "Incremental_") + stamp + ".nbk";
        }

        public static bool IsCycleFolder(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return name.StartsWith("Completo_", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("cadeia_", StringComparison.OrdinalIgnoreCase);
        }

        public static string Pascal(string nome)
        {
            if (string.IsNullOrWhiteSpace(nome)) return "Cliente";
            var n = nome.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var ch in n)
            {
                var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (cat != UnicodeCategory.NonSpacingMark) sb.Append(ch);
            }
            var parts = Regex.Replace(sb.ToString(), "[^A-Za-z0-9]+", " ").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            sb.Length = 0;
            foreach (var p in parts)
            {
                var w = p.ToLowerInvariant();
                sb.Append(char.ToUpperInvariant(w[0]));
                if (w.Length > 1) sb.Append(w.Substring(1));
            }
            return sb.Length == 0 ? "Cliente" : sb.ToString();
        }

        public static string Size(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("0.##") + " KB";
            if (bytes < 1024L * 1024 * 1024) return (bytes / (1024.0 * 1024)).ToString("0.##") + " MB";
            return (bytes / (1024.0 * 1024 * 1024)).ToString("0.##") + " GB";
        }
    }

    public static class Ini
    {
        public static AppConfig Load(string path)
        {
            var c = new AppConfig { ConfigPath = path };
            if (!File.Exists(path)) return c;
            string section = "";
            foreach (var raw in File.ReadAllLines(path, Encoding.UTF8))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#")) continue;
                if (line.StartsWith("[") && line.EndsWith("]")) { section = line.Substring(1, line.Length - 2); continue; }
                var eq = line.IndexOf('=');
                if (eq < 1) continue;
                var key = line.Substring(0, eq).Trim();
                var val = line.Substring(eq + 1).Trim();
                if (section == "Cliente")
                {
                    if (key == "CNPJ" || key == "Codigo") c.Cnpj = val;
                    if (key == "Nome") c.Nome = val;
                }
                else if (section == "Firebird")
                {
                    if (key == "GbakPath") c.GbakPath = val;
                    if (key == "Database") c.Database = val;
                    if (key == "User") c.DbUser = val;
                    if (key == "Password") c.DbPassword = val;
                }
                else if (section == "Backup")
                {
                    if (key == "LocalFolder" && val.Length > 0) c.LocalFolder = val;
                    if (key == "DiasEntreFull") c.DiasEntreFull = ParseInt(val, 7);
                    if (key == "ManterCadeias") c.ManterCadeias = ParseInt(val, 4);
                    if (key == "EnviarFtp") c.EnviarFtp = ParseInt(val, 1) != 0;
                }
                else if (section == "FTP")
                {
                    if (key == "Host") c.FtpHost = val;
                    if (key == "Port") c.FtpPort = ParseInt(val, 9099);
                    if (key == "User") c.FtpUser = val;
                    if (key == "Password") c.FtpPassword = val;
                    if (key == "PastaRemota") c.FtpPastaRemota = val;
                    if (key == "TimeoutSegundos") c.FtpTimeoutSegundos = ParseInt(val, 3600);
                    if (key == "Tentativas") c.FtpTentativas = ParseInt(val, 3);
                }
                else if (section == "Agendamento")
                {
                    if (key == "Hora") c.Hora = val;
                    if (key == "NomeTarefa") c.NomeTarefa = val;
                }
                else if (section == "Seguranca")
                {
                    if (key == "SenhaOperacao") c.SenhaOperacao = val;
                }
            }
            if (string.IsNullOrWhiteSpace(c.LocalFolder) || IsCloud(c.LocalFolder))
                c.LocalFolder = @"C:\BackupFirebird";
            return c;
        }

        public static void Save(AppConfig c)
        {
            var lines = new[]
            {
                "; Backup Firebird 3.0 + envio FTP",
                "; Gerado em " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                "",
                "[Cliente]",
                "CNPJ=" + c.Cnpj,
                "Codigo=" + c.Cnpj,
                "Nome=" + c.Nome,
                "",
                "[Firebird]",
                "GbakPath=" + c.GbakPath,
                "Database=" + c.Database,
                "User=" + c.DbUser,
                "Password=" + c.DbPassword,
                "Host=localhost",
                "Port=3050",
                "",
                "[Backup]",
                "LocalFolder=" + c.LocalFolder,
                "Compactar=0",
                "DiasEntreFull=" + c.DiasEntreFull,
                "ManterCadeias=" + c.ManterCadeias,
                "EnviarFtp=" + (c.EnviarFtp ? "1" : "0"),
                "",
                "[FTP]",
                "Host=" + c.FtpHost,
                "Port=" + c.FtpPort,
                "User=" + c.FtpUser,
                "Password=" + c.FtpPassword,
                "PastaRemota=" + c.FtpPastaRemota,
                "UsePassive=1",
                "SkipPasvIp=1",
                "UseFTPS=0",
                "TimeoutSegundos=" + c.FtpTimeoutSegundos,
                "Tentativas=" + c.FtpTentativas,
                "",
                "[Agendamento]",
                "Hora=" + c.Hora,
                "NomeTarefa=" + c.NomeTarefa,
                "",
                "[Seguranca]",
                "SenhaOperacao=" + c.SenhaOperacao,
                ""
            };
            File.WriteAllLines(c.ConfigPath, lines, new UTF8Encoding(true));
        }

        public static bool IsCloud(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            var p = path.ToLowerInvariant().Replace('/', '\\');
            return p.Contains("\\onedrive") || p.Contains("\\dropbox") || p.Contains("\\google drive");
        }

        static int ParseInt(string s, int d)
        {
            int n;
            return int.TryParse(s, out n) ? n : d;
        }
    }

    public sealed class BackupResult
    {
        public int Level;
        public string FilePath;
        public string ChainName;
        public string ChainFolder;
        public List<string> FilesToUpload = new List<string>();
    }

    public sealed class RestoreChoice
    {
        public string Origin;
        public string Cycle;
        public string Level0;
        public List<string> Incrementals = new List<string>();
        public DateTime Time;
    }

    public sealed class LocalOkFtpFailedException : Exception
    {
        public LocalOkFtpFailedException(string message) : base(message) { }
    }

    public static class Engine
    {
        static readonly object JobLock = new object();

        public static string FindNbackup(string configured)
        {
            var list = new List<string>();
            if (!string.IsNullOrWhiteSpace(configured))
            {
                if (Directory.Exists(configured)) list.Add(Path.Combine(configured, "nbackup.exe"));
                else list.Add(configured);
            }
            var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            list.Add(@"C:\Program Files\Firebird\Firebird_3_0\nbackup.exe");
            list.Add(@"C:\Program Files (x86)\Firebird\Firebird_3_0\nbackup.exe");
            list.Add(Path.Combine(pf, @"Firebird\Firebird_3_0\nbackup.exe"));
            if (!string.IsNullOrEmpty(pf86)) list.Add(Path.Combine(pf86, @"Firebird\Firebird_3_0\nbackup.exe"));
            foreach (var p in list)
            {
                if (File.Exists(p)) return Path.GetFullPath(p);
            }
            throw new Exception("nbackup.exe nao encontrado. Instale o Firebird 3.0.");
        }

        public static bool TryFindNbackup(string configured, out string path)
        {
            try
            {
                path = FindNbackup(configured);
                return true;
            }
            catch
            {
                path = "";
                return false;
            }
        }

        public static string FirebirdFolder(string nbackupExe)
        {
            if (string.IsNullOrWhiteSpace(nbackupExe)) return "";
            return Path.GetDirectoryName(nbackupExe) ?? "";
        }

        public static void SuggestRestoreFiles(string path, out string level0, out string level1)
        {
            var ch = ChoiceFromPath(path);
            level0 = ch == null ? "" : (ch.Level0 ?? "");
            level1 = ch == null || ch.Incrementals == null || ch.Incrementals.Count == 0
                ? ""
                : ch.Incrementals[ch.Incrementals.Count - 1];
        }

        public static RestoreChoice ChoiceFromPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            string dir = null;
            if (File.Exists(path)) dir = Path.GetDirectoryName(path);
            else if (Directory.Exists(path)) dir = path;
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return null;

            var completes = new List<string>();
            var incrementals = new List<string>();
            CollectNbk(dir, completes, incrementals);
            if (completes.Count == 0) return null;
            completes.Sort((a, b) => File.GetLastWriteTimeUtc(a).CompareTo(File.GetLastWriteTimeUtc(b)));
            var level0 = completes[completes.Count - 1];
            if (File.Exists(path) && IsLevel0Name(Path.GetFileName(path)))
                level0 = path;
            var incs = FilterAndSortIncrementals(level0, incrementals);
            if (File.Exists(path) && IsLevel1Name(Path.GetFileName(path)) && IncrementalBelongsTo(level0, path))
            {
                incs.Clear();
                incs.Add(path);
            }
            return new RestoreChoice
            {
                Origin = "Neste PC",
                Cycle = Path.GetFileName(dir),
                Level0 = level0,
                Incrementals = incs,
                Time = File.GetLastWriteTime(level0)
            };
        }

        static void CollectNbk(string dir, List<string> completes, List<string> incrementals)
        {
            if (!Directory.Exists(dir)) return;
            foreach (var f in Directory.GetFiles(dir, "*.nbk"))
            {
                var name = Path.GetFileName(f);
                if (IsLevel0Name(name)) completes.Add(f);
                else if (IsLevel1Name(name)) incrementals.Add(f);
            }
        }

        static bool IsLevel0Name(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return name.StartsWith("Completo_", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith("_L0.nbk", StringComparison.OrdinalIgnoreCase);
        }

        static bool IsLevel1Name(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return name.StartsWith("Incremental_", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith("_L1.nbk", StringComparison.OrdinalIgnoreCase);
        }

        static bool IncrementalBelongsTo(string level0, string level1)
        {
            if (string.IsNullOrWhiteSpace(level0) || string.IsNullOrWhiteSpace(level1)) return false;
            if (!File.Exists(level0) || !File.Exists(level1)) return false;
            DateTime t0, t1;
            try
            {
                t0 = File.GetLastWriteTimeUtc(level0);
                t1 = File.GetLastWriteTimeUtc(level1);
            }
            catch { return false; }
            if (t1 < t0.AddSeconds(-2)) return false;

            var dirs = new List<string>();
            AddRoot(dirs, Path.GetDirectoryName(level0));
            AddRoot(dirs, Path.GetDirectoryName(level1));
            AddRoot(dirs, Path.GetDirectoryName(Path.GetDirectoryName(level0)));
            AddRoot(dirs, Path.GetDirectoryName(Path.GetDirectoryName(level1)));
            foreach (var dir in dirs)
            {
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) continue;
                string[] files;
                try { files = Directory.GetFiles(dir, "*.nbk", SearchOption.AllDirectories); }
                catch { continue; }
                foreach (var f in files)
                {
                    if (string.Equals(f, level0, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!IsLevel0Name(Path.GetFileName(f))) continue;
                    DateTime t;
                    try { t = File.GetLastWriteTimeUtc(f); } catch { continue; }
                    if (t > t0.AddSeconds(2) && t < t1.AddSeconds(-2))
                        return false;
                }
            }
            return true;
        }

        public static List<string> FilterAndSortIncrementals(string level0, IList<string> incrementals)
        {
            var list = new List<string>();
            if (incrementals == null) return list;
            foreach (var inc in incrementals)
            {
                if (string.IsNullOrWhiteSpace(inc) || !File.Exists(inc)) continue;
                if (!IncrementalBelongsTo(level0, inc)) continue;
                if (list.Exists(x => string.Equals(x, inc, StringComparison.OrdinalIgnoreCase))) continue;
                list.Add(inc);
            }
            list.Sort((a, b) => File.GetLastWriteTimeUtc(a).CompareTo(File.GetLastWriteTimeUtc(b)));
            return list;
        }

        public static List<string> BuildRestoreFiles(string level0, IList<string> incrementals)
        {
            var files = new List<string>();
            if (string.IsNullOrWhiteSpace(level0) || !File.Exists(level0)) return files;
            files.Add(level0);
            var ok = FilterAndSortIncrementals(level0, incrementals);
            var byLevel = new SortedDictionary<int, string>();
            foreach (var inc in ok)
            {
                var lv = ReadNbackupLevel(inc);
                if (lv <= 0) lv = 1;
                byLevel[lv] = inc;
            }
            foreach (var kv in byLevel)
                files.Add(kv.Value);
            return files;
        }

        public static string RestoreOrderText(IList<string> files)
        {
            if (files == null || files.Count == 0) return "";
            var sb = new StringBuilder();
            for (var i = 0; i < files.Count; i++)
            {
                if (i > 0) sb.Append("  →  ");
                sb.Append(i + 1).Append(") ").Append(Path.GetFileName(files[i]));
            }
            return sb.ToString();
        }

        static int ReadNbackupLevel(string path)
        {
            var name = Path.GetFileName(path);
            if (IsLevel0Name(name)) return 0;
            try
            {
                using (var fs = File.OpenRead(path))
                {
                    var buf = new byte[8];
                    if (fs.Read(buf, 0, 8) < 8) return 1;
                    if (buf[0] == (byte)'N' && buf[1] == (byte)'B' && buf[2] == (byte)'A' && buf[3] == (byte)'K')
                        return BitConverter.ToUInt16(buf, 6);
                }
            }
            catch { }
            if (IsLevel1Name(name)) return 1;
            return 1;
        }

        static bool IsBrokenChainError(Exception ex)
        {
            var m = ex == null ? "" : (ex.Message ?? "");
            return m.IndexOf("Wrong order", StringComparison.OrdinalIgnoreCase) >= 0
                || m.IndexOf("invalid incremental", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static void Restore(AppConfig c, RestoreChoice choice, string destFdb)
        {
            if (choice == null) throw new Exception("Escolha o backup completo.");
            Restore(c, choice.Level0, choice.Incrementals, destFdb);
        }

        public static void Restore(AppConfig c, string level0, string level1, string destFdb)
        {
            var incs = new List<string>();
            if (!string.IsNullOrWhiteSpace(level1)) incs.Add(level1);
            Restore(c, level0, incs, destFdb);
        }

        public static void Restore(AppConfig c, string level0, IList<string> incrementals, string destFdb)
        {
            if (!Monitor.TryEnter(JobLock)) throw new Exception("Ja existe um backup ou restauracao em andamento.");
            try { RestoreCore(c, level0, incrementals, destFdb); }
            finally { Monitor.Exit(JobLock); }
        }

        static void RestoreCore(AppConfig c, string level0, IList<string> incrementals, string destFdb)
        {
            if (string.IsNullOrWhiteSpace(level0) || !File.Exists(level0))
                throw new Exception("Escolha o arquivo Completo_*.nbk (ou *_L0.nbk).");
            if (string.IsNullOrWhiteSpace(destFdb))
                throw new Exception("Informe o arquivo .fdb de destino.");
            destFdb = Path.GetFullPath(destFdb);
            if (!destFdb.EndsWith(".fdb", StringComparison.OrdinalIgnoreCase) && !destFdb.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase))
                destFdb += ".fdb";
            if (!string.IsNullOrWhiteSpace(c.Database))
            {
                try
                {
                    if (string.Equals(destFdb, Path.GetFullPath(c.Database), StringComparison.OrdinalIgnoreCase))
                        throw new Exception("Nao restaure em cima do banco em uso. Use outro caminho, por exemplo C:\\restaurado\\GDI.FDB. Com o GDI fechado, substitua o original depois.");
                }
                catch (Exception ex)
                {
                    if (ex.Message.StartsWith("Nao restaure", StringComparison.Ordinal)) throw;
                }
            }
            if (File.Exists(destFdb))
                throw new Exception("O destino ja existe:\n" + destFdb + "\n\nEscolha outro nome ou apague esse .fdb restaurado.");
            var destDir = Path.GetDirectoryName(destFdb);
            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                Directory.CreateDirectory(destDir);

            var files = BuildRestoreFiles(level0, incrementals);
            if (files.Count == 0) throw new Exception("Nenhum arquivo de backup para restaurar.");
            var exe = FindNbackup(c.GbakPath);
            StatusFile.Set("restore", "Recuperando o banco de dados...", 10);
            Log.Write("Restaurando com " + exe);
            foreach (var f in files) Log.Write((IsLevel0Name(Path.GetFileName(f)) ? "Completo: " : "Incremental: ") + f);
            Log.Write("Destino: " + destFdb);

            var used = new List<string>(files);
            while (true)
            {
                try
                {
                    InvokeNbackupRestore(c, exe, destFdb, used);
                    break;
                }
                catch (Exception ex)
                {
                    if (!IsBrokenChainError(ex) || used.Count <= 1) throw;
                    var dropped = used[used.Count - 1];
                    Log.Write("Nao deu para aplicar " + Path.GetFileName(dropped) + ". Tentando sem este incremental.", "WARN");
                    used.RemoveAt(used.Count - 1);
                    try { if (File.Exists(destFdb)) File.Delete(destFdb); } catch { }
                }
            }

            if (!File.Exists(destFdb) || new FileInfo(destFdb).Length <= 0)
                throw new Exception("A recuperacao nao gerou o arquivo do banco.");
            Log.Write("Banco restaurado (" + Naming.Size(new FileInfo(destFdb).Length) + "): " + destFdb, "OK");
            if (used.Count > 1)
                StatusFile.Set("ok", "Banco recuperado em " + destFdb, 100);
            else if (files.Count > 1)
                StatusFile.Set("aviso", "Recuperado so com a copia completa em " + destFdb, 100);
            else
                StatusFile.Set("ok", "Banco recuperado em " + destFdb, 100);
        }

        static void InvokeNbackupRestore(AppConfig c, string exe, string destFdb, IList<string> backupFiles)
        {
            if (backupFiles == null || backupFiles.Count == 0)
                throw new Exception("Nenhum arquivo de backup para restaurar.");
            var pwd = Path.Combine(Path.GetTempPath(), "fbk-pwd-" + Guid.NewGuid().ToString("N") + ".tmp");
            File.WriteAllText(pwd, c.DbPassword ?? "", new UTF8Encoding(false));
            try
            {
                var args = new StringBuilder();
                AppendNbackupAuth(args, c, pwd);
                args.Append("-R \"").Append(destFdb).Append("\"");
                foreach (var f in backupFiles)
                    args.Append(" \"").Append(f).Append("\"");
                var p = new Process();
                p.StartInfo.FileName = exe;
                p.StartInfo.Arguments = args.ToString();
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.RedirectStandardError = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.WorkingDirectory = Path.GetDirectoryName(backupFiles[0]) ?? AppPaths.Root;
                p.Start();
                var err = p.StandardError.ReadToEnd();
                var output = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                if (!string.IsNullOrWhiteSpace(output)) Log.Write(output.Trim());
                if (p.ExitCode != 0)
                    throw new Exception("nbackup retornou " + p.ExitCode + (string.IsNullOrWhiteSpace(err) ? "" : ": " + err.Trim()));
            }
            finally
            {
                try { File.WriteAllText(pwd, new string('x', 64)); File.Delete(pwd); } catch { }
            }
        }

        static void AppendNbackupAuth(StringBuilder args, AppConfig c, string pwdFile)
        {
            if (!string.IsNullOrWhiteSpace(c.DbUser))
                args.Append("-USER ").Append(c.DbUser).Append(" ");
            if (!string.IsNullOrWhiteSpace(c.DbPassword) && !string.IsNullOrEmpty(pwdFile))
                args.Append("-FETCH_PASSWORD \"").Append(pwdFile).Append("\" ");
        }

        public static void EnsureFolders(AppConfig c)
        {
            if (!Directory.Exists(c.LocalFolder)) Directory.CreateDirectory(c.LocalFolder);
            if (!Directory.Exists(AppPaths.LogDir)) Directory.CreateDirectory(AppPaths.LogDir);
        }

        public static void RunBackup(AppConfig c, bool ftp)
        {
            if (!Monitor.TryEnter(JobLock)) throw new Exception("Ja existe um backup em andamento.");
            try
            {
                RunBackupCore(c, -1, ftp);
            }
            finally { Monitor.Exit(JobLock); }
        }

        public static string RunMontarTeste(AppConfig c)
        {
            if (!Monitor.TryEnter(JobLock)) throw new Exception("Ja existe um backup em andamento.");
            try
            {
                var pasta = Path.Combine(c.LocalFolder, "_teste-montar");
                if (Directory.Exists(pasta)) Directory.Delete(pasta, true);
                Directory.CreateDirectory(pasta);
                var test = Clone(c);
                test.LocalFolder = pasta;
                test.ManterCadeias = 10;
                EnsureFolders(test);

                StatusFile.Set("backup", "Copia completa de teste...", 15);
                var a = RunBackupCore(test, 0, false);
                if (a.Level != 0 || !File.Exists(a.FilePath))
                    throw new Exception("Nao gerou a copia completa de teste.");

                Thread.Sleep(1500);
                StatusFile.Set("backup", "Copia do dia 1/2...", 45);
                var b = RunBackupCore(test, 1, false);
                if (b.Level != 1 || !File.Exists(b.FilePath))
                    throw new Exception("Nao gerou a primeira copia do dia.");

                Thread.Sleep(1500);
                StatusFile.Set("backup", "Copia do dia 2/2...", 75);
                var d = RunBackupCore(test, 1, false);
                if (d.Level != 1 || !File.Exists(d.FilePath))
                    throw new Exception("Nao gerou a segunda copia do dia.");

                var linhas = new[]
                {
                    "COPIAS PARA MONTAR A BASE",
                    "Completo: " + Path.GetFileName(a.FilePath),
                    "Do dia 1: " + Path.GetFileName(b.FilePath),
                    "Do dia 2: " + Path.GetFileName(d.FilePath),
                    "",
                    "Pasta: " + pasta,
                    "No programa: Recuperar... escolha esta pasta.",
                    "Grave em C:\\restaurado. Nao use o GDI.FDB aberto."
                };
                File.WriteAllLines(Path.Combine(pasta, "como-montar.txt"), linhas, Encoding.UTF8);
                foreach (var l in linhas) Log.Write(l);
                StatusFile.Set("ok", "Copias de teste prontas para recuperar.", 100);
                return a.FilePath;
            }
            finally
            {
                try
                {
                    State.Clear(c);
                    Log.Write("Cadeia oficial reiniciada. O proximo backup sera COMPLETO.", "WARN");
                }
                catch { }
                Monitor.Exit(JobLock);
            }
        }

        public static string RunTests(AppConfig c)
        {
            if (!Monitor.TryEnter(JobLock)) throw new Exception("Ja existe um backup em andamento.");
            try
            {
                var pasta = Path.Combine(c.LocalFolder, "_teste-rotinas");
                if (Directory.Exists(pasta)) Directory.Delete(pasta, true);
                Directory.CreateDirectory(pasta);
                var test = Clone(c);
                test.LocalFolder = pasta;
                test.ManterCadeias = 1;
                EnsureFolders(test);
                var ftpRel = c.ClientFolderName + "/_teste-rotinas";

                StatusFile.Set("backup", "Teste 1/3: copia completa...", 10);
                var a = RunBackupCore(test, 0, false);
                var completoOk = a.Level == 0 && File.Exists(a.FilePath);

                FtpSession ftp = null;
                try
                {
                    if (c.EnviarFtp)
                    {
                        ftp = new FtpSession(c);
                        Log.Write("Conectando FTP " + c.FtpHost + ":" + c.FtpPort + " (teste das rotinas)");
                        ftp.Connect();
                        SendFiles(ftp, test, a.FilesToUpload, "_teste-rotinas/" + a.ChainName, ftpRel);
                    }
                    else
                        Log.Write("Teste sem envio ao servidor (somente neste computador).");

                    Thread.Sleep(1000);
                    StatusFile.Set("backup", "Teste 2/3: copia do dia...", 40);
                    var b = RunBackupCore(test, 1, false);
                    var incrementalOk = b.Level == 1 && b.ChainName == a.ChainName && File.Exists(b.FilePath);
                    if (ftp != null)
                        SendFiles(ftp, test, b.FilesToUpload, "_teste-rotinas/" + b.ChainName, ftpRel);

                    Thread.Sleep(1000);
                    StatusFile.Set("backup", "Teste 3/3: nova copia completa...", 70);
                    var d = RunBackupCore(test, 0, false);
                    if (ftp != null)
                        SendFiles(ftp, test, d.FilesToUpload, "_teste-rotinas/" + d.ChainName, ftpRel);

                    var novoOk = d.Level == 0 && File.Exists(d.FilePath) && d.ChainName != a.ChainName;
                    var exclusaoOk = !Directory.Exists(a.ChainFolder);
                    var linhas = new[]
                    {
                        "TESTE DAS ROTINAS",
                        "Modo: " + (c.EnviarFtp ? "neste PC e servidor FTP" : "somente neste computador"),
                        "Completo: " + (completoOk ? "OK " + Path.GetFileName(a.FilePath) : "FALHOU"),
                        "Incremental: " + (incrementalOk ? "OK " + Path.GetFileName(b.FilePath) : "FALHOU"),
                        "Novo completo: " + (novoOk ? "OK " + Path.GetFileName(d.FilePath) : "FALHOU"),
                        "Exclusao local: " + (exclusaoOk ? "OK pasta antiga apagada" : "FALHOU"),
                        "Pasta isolada: " + pasta
                    };
                    var rel = Path.Combine(pasta, "resultado-teste.txt");
                    File.WriteAllLines(rel, linhas, Encoding.UTF8);
                    foreach (var l in linhas) Log.Write(l);
                    if (completoOk && incrementalOk && novoOk && exclusaoOk)
                        StatusFile.Set("ok", "Teste concluido. O programa esta funcionando.", 100);
                    else
                    {
                        StatusFile.Set("erro", "O teste encontrou um problema.", -1);
                        throw new Exception("O teste encontrou falha. Abra _teste-rotinas\\resultado-teste.txt");
                    }
                    return rel;
                }
                finally
                {
                    if (ftp != null) ftp.Dispose();
                }
            }
            finally
            {
                try
                {
                    State.Clear(c);
                    Log.Write("Cadeia oficial reiniciada apos o teste. O proximo backup sera COMPLETO.", "WARN");
                }
                catch { }
                Monitor.Exit(JobLock);
            }
        }

        static AppConfig Clone(AppConfig c)
        {
            return new AppConfig
            {
                ConfigPath = c.ConfigPath,
                Cnpj = c.Cnpj,
                Nome = c.Nome,
                GbakPath = c.GbakPath,
                Database = c.Database,
                DbUser = c.DbUser,
                DbPassword = c.DbPassword,
                LocalFolder = c.LocalFolder,
                DiasEntreFull = c.DiasEntreFull,
                ManterCadeias = c.ManterCadeias,
                FtpHost = c.FtpHost,
                FtpPort = c.FtpPort,
                FtpUser = c.FtpUser,
                FtpPassword = c.FtpPassword,
                FtpPastaRemota = c.FtpPastaRemota,
                FtpTimeoutSegundos = c.FtpTimeoutSegundos,
                FtpTentativas = c.FtpTentativas,
                EnviarFtp = c.EnviarFtp,
                Hora = c.Hora,
                NomeTarefa = c.NomeTarefa
            };
        }

        static BackupResult RunBackupCore(AppConfig c, int forceLevel, bool ftp)
        {
            EnsureFolders(c);
            if (string.IsNullOrWhiteSpace(c.Database) || !File.Exists(c.Database))
                throw new Exception("Escolha um arquivo .fdb valido.");
            FindNbackup(c.GbakPath);

            StatusFile.Set("backup", "Copiando o banco de dados...", 5);
            Log.Write("Backup iniciado. Banco: " + c.Database);
            Log.Write(ftp ? "Destino: neste computador e no servidor FTP." : "Destino: somente neste computador.");

            var state = State.Load(c);
            var level = 1;
            if (state == null || NeedFull(c, state)) level = 0;
            if (forceLevel == 0 || forceLevel == 1) level = forceLevel;

            var stamp = Naming.Stamp();
            string chainName, chainFolder;
            if (level == 0)
            {
                chainName = Naming.CycleFolder(stamp);
                chainFolder = Path.Combine(c.LocalFolder, chainName);
            }
            else
            {
                chainName = state.ChainName;
                chainFolder = state.ChainFolder;
                if (string.IsNullOrEmpty(chainFolder) || !Directory.Exists(chainFolder))
                {
                    level = 0;
                    chainName = Naming.CycleFolder(stamp);
                    chainFolder = Path.Combine(c.LocalFolder, chainName);
                }
            }
            Directory.CreateDirectory(chainFolder);

            var backupName = Naming.FileName(level, stamp);
            var backupFile = Path.Combine(chainFolder, backupName);
            try
            {
                RunNbackup(c, level, backupFile);
            }
            catch (Exception ex)
            {
                if (level != 1) throw;
                Log.Write("Incremental falhou: " + ex.Message, "WARN");
                level = 0;
                chainName = Naming.CycleFolder(stamp);
                chainFolder = Path.Combine(c.LocalFolder, chainName);
                Directory.CreateDirectory(chainFolder);
                backupName = Naming.FileName(0, stamp);
                backupFile = Path.Combine(chainFolder, backupName);
                RunNbackup(c, 0, backupFile);
            }

            var level0File = backupFile;
            var level0Name = backupName;
            if (level == 1 && state != null && File.Exists(state.Level0File))
            {
                level0File = state.Level0File;
                level0Name = Path.GetFileName(level0File);
            }

            WriteGuide(c, chainFolder, level, backupName, level0Name);
            State.Save(c, new State
            {
                Level0Time = level == 0 ? DateTime.Now.ToString("o") : (state != null ? state.Level0Time : DateTime.Now.ToString("o")),
                Level0File = level0File,
                ChainName = chainName,
                ChainFolder = chainFolder,
                LastLevel = level,
                LastFile = backupFile
            });

            var files = new List<string> { backupFile };
            var guide = Path.Combine(chainFolder, "Como-restaurar.txt");
            if (File.Exists(guide)) files.Add(guide);
            if (level == 1 && File.Exists(level0File) && !files.Contains(level0File)) files.Add(level0File);

            ClearOldLocal(c);
            Log.Write("Copia local pronta: " + backupFile, "OK");

            var result = new BackupResult
            {
                Level = level,
                FilePath = backupFile,
                ChainName = chainName,
                ChainFolder = chainFolder,
                FilesToUpload = files
            };

            if (ftp)
            {
                StatusFile.Set("ftp", "Enviando a copia para o servidor...", 0);
                try
                {
                    Upload(c, files, chainName, null);
                    StatusFile.Set("ok", "Copia concluida neste computador e no servidor.", 100);
                }
                catch (Exception ex)
                {
                    var friendly = FriendlyFtp(ex);
                    Log.Write("Copia local ok. FTP falhou: " + friendly, "WARN");
                    StatusFile.Set("aviso", "Copia neste PC ok. O envio ao servidor nao concluiu.", 100);
                    throw new LocalOkFtpFailedException(
                        "A copia neste computador ficou pronta.\n\n" +
                        "O envio ao servidor nao concluiu.\n" +
                        friendly + "\n\n" +
                        "No computador do servidor, feche a janela do FTP e abra iniciar-ftp.bat. Depois clique de novo em Copiar agora.");
                }
            }
            else
            {
                StatusFile.Set("ok", "Copia neste computador concluida.", 100);
            }
            return result;
        }

        static bool NeedFull(AppConfig c, State s)
        {
            if (s == null || string.IsNullOrEmpty(s.Level0File) || !File.Exists(s.Level0File)) return true;
            DateTime when;
            if (!DateTime.TryParse(s.Level0Time, out when)) return true;
            if (DateTime.Now >= when.AddDays(Math.Max(1, c.DiasEntreFull))) return true;
            if (OtherLevel0Since(c, s.Level0File))
            {
                Log.Write("Outro backup completo foi feito depois desta cadeia. Novo ciclo COMPLETO.", "WARN");
                return true;
            }
            return false;
        }

        static bool OtherLevel0Since(AppConfig c, string level0File)
        {
            DateTime t0;
            try { t0 = File.GetLastWriteTimeUtc(level0File); }
            catch { return true; }
            var roots = LocalSearchRoots(c);
            AddRoot(roots, Path.Combine(c.LocalFolder, "_teste-rotinas"));
            foreach (var root in roots)
            {
                if (!Directory.Exists(root)) continue;
                string[] files;
                try { files = Directory.GetFiles(root, "*.nbk", SearchOption.AllDirectories); }
                catch { continue; }
                foreach (var f in files)
                {
                    if (string.Equals(f, level0File, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!IsLevel0Name(Path.GetFileName(f))) continue;
                    DateTime t;
                    try { t = File.GetLastWriteTimeUtc(f); } catch { continue; }
                    if (t > t0.AddSeconds(2)) return true;
                }
            }
            return false;
        }

        static void RunNbackup(AppConfig c, int level, string dest)
        {
            var exe = FindNbackup(c.GbakPath);
            var pwd = Path.Combine(Path.GetTempPath(), "fbk-pwd-" + Guid.NewGuid().ToString("N") + ".tmp");
            File.WriteAllText(pwd, c.DbPassword ?? "", new UTF8Encoding(false));
            try
            {
                Log.Write("nbackup nivel " + level + " -> " + dest);
                var p = new Process();
                p.StartInfo.FileName = exe;
                p.StartInfo.Arguments = string.Format("-USER {0} -FETCH_PASSWORD \"{1}\" -BACKUP {2} \"{3}\" \"{4}\"",
                    c.DbUser, pwd, level, c.Database, dest);
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.RedirectStandardError = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.Start();
                var err = p.StandardError.ReadToEnd();
                p.WaitForExit();
                if (p.ExitCode != 0)
                    throw new Exception("nbackup retornou " + p.ExitCode + (string.IsNullOrWhiteSpace(err) ? "" : ": " + err.Trim()));
                if (!File.Exists(dest) || new FileInfo(dest).Length <= 0)
                    throw new Exception("nbackup nao gerou o arquivo.");
                Log.Write("Backup nivel " + level + " gerado (" + Naming.Size(new FileInfo(dest).Length) + ")", "OK");
            }
            catch
            {
                try
                {
                    var u = new Process();
                    u.StartInfo.FileName = exe;
                    u.StartInfo.Arguments = string.Format("-USER {0} -FETCH_PASSWORD \"{1}\" -UNLOCK \"{2}\"", c.DbUser, pwd, c.Database);
                    u.StartInfo.UseShellExecute = false;
                    u.StartInfo.CreateNoWindow = true;
                    u.Start();
                    u.WaitForExit(15000);
                }
                catch { }
                throw;
            }
            finally
            {
                try { File.WriteAllText(pwd, new string('x', 64)); File.Delete(pwd); } catch { }
            }
        }

        static void WriteGuide(AppConfig c, string folder, int level, string backupName, string l0)
        {
            if (level == 0) l0 = backupName;
            var incs = new List<string>();
            foreach (var f in Directory.GetFiles(folder, "Incremental_*.nbk"))
                incs.Add(Path.GetFileName(f));
            incs.Sort(StringComparer.OrdinalIgnoreCase);
            var txt = new StringBuilder();
            txt.AppendLine("BACKUP FIREBIRD");
            txt.AppendLine("Cliente: " + c.Nome);
            txt.AppendLine("Pasta: " + c.ClientFolderName);
            txt.AppendLine();
            txt.AppendLine("Completo_*.nbk = backup completo (base)");
            txt.AppendLine("Incremental_*.nbk = o que mudou desde o completo");
            txt.AppendLine("Restaurar: primeiro o completo, depois o incremental (se houver varios do mesmo tipo, use o mais recente).");
            txt.AppendLine();
            txt.AppendLine("Completo desta pasta: " + l0);
            txt.AppendLine("Restaurar so o completo:");
            txt.AppendLine("  nbackup -USER SYSDBA -R C:\\restaurado\\banco.fdb \"" + l0 + "\"");
            if (incs.Count > 0)
            {
                txt.AppendLine("Restaurar com incremental:");
                txt.AppendLine("  nbackup -USER SYSDBA -R C:\\restaurado\\banco.fdb \"" + l0 + "\" \"" + incs[incs.Count - 1] + "\"");
            }
            File.WriteAllText(Path.Combine(folder, "Como-restaurar.txt"), txt.ToString(), new UTF8Encoding(true));
        }

        static void ClearOldLocal(AppConfig c)
        {
            var dirs = new List<DirectoryInfo>();
            if (!Directory.Exists(c.LocalFolder)) return;
            foreach (var d in new DirectoryInfo(c.LocalFolder).GetDirectories())
            {
                if (Naming.IsCycleFolder(d.Name)) dirs.Add(d);
            }
            dirs.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            var keep = Math.Max(1, c.ManterCadeias);
            while (dirs.Count > keep)
            {
                try
                {
                    Log.Write("Pasta antiga removida: " + dirs[0].Name);
                    dirs[0].Delete(true);
                }
                catch (Exception ex) { Log.Write(ex.Message, "WARN"); }
                dirs.RemoveAt(0);
            }
        }

        public static void TestFtp(AppConfig c)
        {
            using (var ftp = new FtpSession(c))
            {
                ftp.Connect();
                ftp.EnsurePath(c.ClientFolderName);
            }
        }

        public sealed class CopyItem
        {
            public string Type;
            public DateTime Time;
            public string Path;
            public long Size;
            public string Cycle;
            public string Origin;
        }

        public static List<string> LocalSearchRoots(AppConfig c)
        {
            var list = new List<string>();
            AddRoot(list, c != null ? c.LocalFolder : null);
            AddRoot(list, @"C:\BackupFirebird");
            AddRoot(list, @"C:\GDI\Dados\Backup Local");
            if (c != null && !string.IsNullOrWhiteSpace(c.Database))
            {
                try
                {
                    var parent = Path.GetDirectoryName(c.Database);
                    AddRoot(list, Path.Combine(parent, "Backup Local"));
                    AddRoot(list, Path.Combine(parent, "Backup"));
                    AddRoot(list, parent);
                }
                catch { }
            }
            try { AddRoot(list, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Backup Firebird")); } catch { }
            return list;
        }

        static void AddRoot(List<string> list, string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            try { path = Path.GetFullPath(path); } catch { return; }
            foreach (var x in list)
                if (string.Equals(x, path, StringComparison.OrdinalIgnoreCase)) return;
            list.Add(path);
        }

        public static List<CopyItem> ListCopies(AppConfig c)
        {
            return ListCopiesInRoots(LocalSearchRoots(c));
        }

        public static List<CopyItem> ListCopiesInRoots(IList<string> roots)
        {
            var list = new List<CopyItem>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (roots == null) return list;
            foreach (var root in roots)
            {
                if (!Directory.Exists(root)) continue;
                ScanBackupRoot(root, root, list, seen);
            }
            list.Sort((a, b) => b.Time.CompareTo(a.Time));
            return list;
        }

        static void ScanBackupRoot(string root, string origin, List<CopyItem> list, HashSet<string> seen)
        {
            DirectoryInfo di;
            try { di = new DirectoryInfo(root); } catch { return; }
            foreach (var f in di.GetFiles("*.nbk"))
                AddCopyItem(list, seen, f, di.Name, origin);
            foreach (var dir in di.GetDirectories())
            {
                if (dir.Name.StartsWith("_teste", StringComparison.OrdinalIgnoreCase)
                    || dir.Name.StartsWith("_montar", StringComparison.OrdinalIgnoreCase))
                {
                    ScanBackupRoot(dir.FullName, origin + " (teste)", list, seen);
                    continue;
                }
                if (dir.Name.StartsWith("_", StringComparison.Ordinal)) continue;
                if (!Naming.IsCycleFolder(dir.Name)) continue;
                foreach (var f in dir.GetFiles("*.nbk"))
                    AddCopyItem(list, seen, f, dir.Name, origin);
            }
        }

        static void AddCopyItem(List<CopyItem> list, HashSet<string> seen, FileInfo f, string cycle, string origin)
        {
            if (!seen.Add(f.FullName)) return;
            list.Add(new CopyItem
            {
                Type = f.Name.StartsWith("Incremental_", StringComparison.OrdinalIgnoreCase) || f.Name.EndsWith("_L1.nbk", StringComparison.OrdinalIgnoreCase) ? "Do dia" : "Completa",
                Time = f.LastWriteTime,
                Path = f.FullName,
                Size = f.Length,
                Cycle = cycle,
                Origin = origin
            });
        }

        public static List<RestoreChoice> ListLocalRestoreChoices(AppConfig c, string extraFolder)
        {
            var roots = LocalSearchRoots(c);
            AddRoot(roots, extraFolder);
            var copies = ListCopiesInRoots(roots);
            var byDir = new Dictionary<string, RestoreChoice>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in copies)
            {
                var dir = Path.GetDirectoryName(item.Path);
                RestoreChoice ch;
                if (!byDir.TryGetValue(dir, out ch))
                {
                    ch = new RestoreChoice
                    {
                        Origin = "Neste PC",
                        Cycle = item.Cycle,
                        Time = item.Time
                    };
                    byDir[dir] = ch;
                }
                var name = Path.GetFileName(item.Path);
                if (IsLevel0Name(name))
                {
                    ch.Level0 = item.Path;
                    if (item.Time > ch.Time) ch.Time = item.Time;
                }
                else if (IsLevel1Name(name))
                {
                    if (ch.Incrementals == null) ch.Incrementals = new List<string>();
                    ch.Incrementals.Add(item.Path);
                    if (item.Time > ch.Time) ch.Time = item.Time;
                }
            }
            var list = new List<RestoreChoice>();
            foreach (var ch in byDir.Values)
            {
                if (string.IsNullOrEmpty(ch.Level0)) continue;
                ch.Incrementals = FilterAndSortIncrementals(ch.Level0, ch.Incrementals);
                try { ch.Time = File.GetLastWriteTime(ch.Level0); } catch { }
                list.Add(ch);
            }
            list.Sort((a, b) => b.Time.CompareTo(a.Time));
            return list;
        }

        public static bool IsScheduled(AppConfig c)
        {
            return Sch("/Query /TN \"" + c.NomeTarefa + "\"") == 0;
        }

        public static string NormalizeHora(string hora)
        {
            DateTime t;
            if (DateTime.TryParseExact((hora ?? "").Trim(), new[] { "HH:mm", "H:mm" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out t))
                return t.ToString("HH:mm");
            return "23:00";
        }

        static void Upload(AppConfig c, List<string> files, string chainName, string cleanupDir)
        {
            var last = (Exception)null;
            var tentativas = Math.Max(1, Math.Min(3, c.FtpTentativas));
            for (var attempt = 1; attempt <= tentativas; attempt++)
            {
                try
                {
                    Log.Write("Conectando FTP " + c.FtpHost + ":" + c.FtpPort + " (tentativa " + attempt + ")");
                    using (var ftp = new FtpSession(c))
                    {
                        ftp.Connect();
                        SendFiles(ftp, c, files, chainName, cleanupDir);
                    }
                    Log.Write("Envio FTP concluido.", "OK");
                    return;
                }
                catch (Exception ex)
                {
                    last = ex;
                    Log.Write("Tentativa FTP " + attempt + ": " + FriendlyFtp(ex), "WARN");
                    if (attempt >= tentativas) break;
                    Thread.Sleep(2000);
                }
            }
            throw last ?? new Exception("Falha no envio FTP.");
        }

        static void SendFiles(FtpSession ftp, AppConfig c, List<string> files, string chainName, string cleanupDir)
        {
            ftp.EnsurePath(c.ClientFolderName + "/" + chainName.Replace('\\', '/').Trim('/'));
            foreach (var f in files)
            {
                if (!File.Exists(f)) continue;
                StatusFile.Set("ftp", "Enviando " + Path.GetFileName(f), 0);
                ftp.Store(f, Path.GetFileName(f));
            }
            Log.Write("Envio FTP concluido.", "OK");
        }

        public static string FriendlyFtp(Exception ex)
        {
            var m = ex == null || ex.Message == null ? "" : ex.Message;
            if (m.IndexOf("transporte", StringComparison.OrdinalIgnoreCase) >= 0
                || m.IndexOf("did not properly respond", StringComparison.OrdinalIgnoreCase) >= 0
                || m.IndexOf("host conectado nao respondeu", StringComparison.OrdinalIgnoreCase) >= 0
                || m.IndexOf("host conectado não respondeu", StringComparison.OrdinalIgnoreCase) >= 0
                || m.IndexOf("Timeout ao conectar", StringComparison.OrdinalIgnoreCase) >= 0)
                return "O servidor das copias nao respondeu. Em geral o FTP ficou preso; feche e abra iniciar-ftp.bat no servidor.";
            if (m.IndexOf("XSTOR", StringComparison.OrdinalIgnoreCase) >= 0
                || m.IndexOf("VERSAO 2", StringComparison.OrdinalIgnoreCase) >= 0)
                return "O servidor das copias precisa ser religado. No servidor, feche e abra iniciar-ftp.bat.";
            return m.Trim();
        }

        public static void Install(AppConfig c)
        {
            EnsureFolders(c);
            c.Hora = NormalizeHora(c.Hora);
            Ini.Save(c);
            var exe = Process.GetCurrentProcess().MainModule.FileName;
            var root = Path.GetDirectoryName(exe);
            if (string.IsNullOrEmpty(root)) root = AppPaths.Root;

            var cmdBackup = Path.Combine(root, "_agendar-backup.cmd");
            var cmdTray = Path.Combine(root, "_agendar-bandeja.cmd");
            File.WriteAllText(cmdBackup, "@echo off\r\ncd /d \"%~dp0\"\r\n\"" + Path.GetFileName(exe) + "\" --backup\r\n", Encoding.ASCII);
            File.WriteAllText(cmdTray, "@echo off\r\ncd /d \"%~dp0\"\r\nstart \"\" \"" + Path.GetFileName(exe) + "\" --tray\r\n", Encoding.ASCII);

            Sch("/Delete /TN \"" + c.NomeTarefa + "\" /F");
            Sch("/Delete /TN \"Backup Firebird Bandeja\" /F");

            var create1 = "/Create /TN \"" + c.NomeTarefa + "\" /TR " + SchTr(cmdBackup) + " /SC DAILY /ST " + c.Hora + " /F /RL LIMITED";
            var create2 = "/Create /TN \"Backup Firebird Bandeja\" /TR " + SchTr(cmdTray) + " /SC ONLOGON /F /RL LIMITED";
            if (Sch(create1) != 0) throw new Exception("Nao foi possivel criar o agendamento diario. Execute como administrador se o Windows pedir.");
            Sch(create2);

            try
            {
                Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Backup Firebird.lnk"), exe, "--tray", root);
            }
            catch (Exception ex) { Log.Write("Atalho inicializar: " + ex.Message, "WARN"); }

            try
            {
                Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Backup Firebird.lnk"), exe, "", root);
            }
            catch (Exception ex) { Log.Write("Atalho area de trabalho: " + ex.Message, "WARN"); }

            Log.Write("Servico instalado. Backup diario as " + c.Hora + ".", "OK");
        }

        public static void Uninstall(AppConfig c)
        {
            var name = (c != null && !string.IsNullOrWhiteSpace(c.NomeTarefa)) ? c.NomeTarefa : "Backup Firebird Cliente";
            Sch("/Delete /TN \"" + name + "\" /F");
            Sch("/Delete /TN \"Backup Firebird Bandeja\" /F");

            try
            {
                var lnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Backup Firebird.lnk");
                if (File.Exists(lnk)) File.Delete(lnk);
            }
            catch (Exception ex) { Log.Write("Atalho inicializar: " + ex.Message, "WARN"); }

            try
            {
                var root = AppPaths.Root;
                TryDeleteFile(Path.Combine(root, "_agendar-backup.cmd"));
                TryDeleteFile(Path.Combine(root, "_agendar-bandeja.cmd"));
            }
            catch { }

            Log.Write("Servico removido neste PC.", "OK");
        }

        public static void ClearLocalBackups(AppConfig c)
        {
            if (c == null || string.IsNullOrWhiteSpace(c.LocalFolder))
                throw new Exception("Pasta de copias nao configurada.");
            var root = Path.GetFullPath(c.LocalFolder);
            if (root.Length < 8)
                throw new Exception("Pasta de copias invalida.");
            var rootNorm = root.TrimEnd('\\');
            if (string.Equals(rootNorm, Path.GetPathRoot(rootNorm), StringComparison.OrdinalIgnoreCase))
                throw new Exception("Nao posso limpar a unidade inteira.");
            if (!string.IsNullOrWhiteSpace(c.Database))
            {
                string dbDir = null;
                try { dbDir = Path.GetDirectoryName(Path.GetFullPath(c.Database)); } catch { }
                if (!string.IsNullOrEmpty(dbDir) && string.Equals(dbDir, rootNorm, StringComparison.OrdinalIgnoreCase))
                    throw new Exception("A pasta das copias e a mesma do banco. Nao apago para nao arriscar o GDI.");
            }
            if (!Directory.Exists(root)) return;

            State.Clear(c);

            foreach (var d in new DirectoryInfo(root).GetDirectories())
            {
                if (!Naming.IsCycleFolder(d.Name) && !string.Equals(d.Name, "_teste-rotinas", StringComparison.OrdinalIgnoreCase))
                    continue;
                try
                {
                    d.Delete(true);
                    Log.Write("Pasta apagada: " + d.Name);
                }
                catch (Exception ex) { Log.Write(ex.Message, "WARN"); }
            }
            foreach (var f in Directory.GetFiles(root))
            {
                var name = Path.GetFileName(f);
                if (name == null) continue;
                if (name.EndsWith(".nbk", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("Como-restaurar.txt", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("nbackup-state.json", StringComparison.OrdinalIgnoreCase))
                {
                    TryDeleteFile(f);
                }
            }
            Log.Write("Copias locais apagadas em " + root, "OK");
        }

        static void TryDeleteFile(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception ex) { Log.Write(ex.Message, "WARN"); }
        }

        static string SchTr(string path)
        {
            return "\"\\\"" + path + "\\\"\"";
        }

        static void Shortcut(string lnkPath, string target, string args, string workDir)
        {
            if (string.IsNullOrWhiteSpace(lnkPath)) return;
            var dir = Path.GetDirectoryName(lnkPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var t = Type.GetTypeFromProgID("WScript.Shell");
            if (t == null) throw new Exception("WScript.Shell indisponivel.");
            object shell = Activator.CreateInstance(t);
            object lnk = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod | BindingFlags.Public, null, shell, new object[] { lnkPath });
            ComSet(lnk, "TargetPath", target);
            ComSet(lnk, "Arguments", args ?? "");
            ComSet(lnk, "WorkingDirectory", workDir ?? "");
            ComSet(lnk, "WindowStyle", 1);
            try { ComSet(lnk, "IconLocation", target + ",0"); } catch { }
            lnk.GetType().InvokeMember("Save", BindingFlags.InvokeMethod, null, lnk, null);
            try { Marshal.FinalReleaseComObject(lnk); } catch { }
            try { Marshal.FinalReleaseComObject(shell); } catch { }
        }

        static void ComSet(object obj, string prop, object value)
        {
            obj.GetType().InvokeMember(prop, BindingFlags.SetProperty | BindingFlags.Public, null, obj, new object[] { value });
        }

        static int Sch(string args)
        {
            var p = new Process();
            p.StartInfo.FileName = "schtasks.exe";
            p.StartInfo.Arguments = args;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.Start();
            p.WaitForExit();
            return p.ExitCode;
        }
    }

    sealed class State
    {
        public string Level0Time, Level0File, ChainName, ChainFolder, LastFile;
        public int LastLevel;
        static string FileOf(AppConfig c) { return Path.Combine(c.LocalFolder, "nbackup-state.json"); }

        public static State Load(AppConfig c)
        {
            var p = FileOf(c);
            if (!File.Exists(p)) return null;
            try
            {
                var j = File.ReadAllText(p, Encoding.UTF8);
                var s = new State();
                s.Level0Time = Get(j, "Level0Time");
                s.Level0File = Get(j, "Level0File");
                s.ChainName = Get(j, "ChainName");
                s.ChainFolder = Get(j, "ChainFolder");
                s.LastFile = Get(j, "LastFile");
                int n;
                int.TryParse(Get(j, "LastLevel"), out n);
                s.LastLevel = n;
                return s;
            }
            catch { return null; }
        }

        public static void Save(AppConfig c, State s)
        {
            var j = "{\"Level0Time\":\"" + Esc(s.Level0Time) + "\",\"Level0File\":\"" + Esc(s.Level0File) + "\",\"ChainName\":\"" + Esc(s.ChainName) + "\",\"ChainFolder\":\"" + Esc(s.ChainFolder) + "\",\"LastFile\":\"" + Esc(s.LastFile) + "\",\"LastLevel\":" + s.LastLevel + "}";
            File.WriteAllText(FileOf(c), j, new UTF8Encoding(true));
        }

        public static void Clear(AppConfig c)
        {
            var p = FileOf(c);
            if (File.Exists(p)) File.Delete(p);
        }

        static string Get(string json, string key)
        {
            var m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*\"([^\"]*)\"");
            if (m.Success) return m.Groups[1].Value.Replace("\\\\", "\\");
            m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*(-?\\d+)");
            return m.Success ? m.Groups[1].Value : "";
        }

        static string Esc(string s) { return (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\""); }
    }

    sealed class FtpSession : IDisposable
    {
        readonly AppConfig _c;
        TcpClient _tcp;
        NetworkStream _ns;
        StreamReader _reader;
        StreamWriter _writer;
        bool _xstor;

        public FtpSession(AppConfig c) { _c = c; }

        int CommandTimeoutMs { get { return 30000; } }
        int TransferTimeoutMs { get { return Math.Max(120, _c.FtpTimeoutSegundos) * 1000; } }

        void SetIoTimeout(int ms)
        {
            try
            {
                if (_ns != null) { _ns.ReadTimeout = ms; _ns.WriteTimeout = ms; }
                if (_tcp != null) { _tcp.ReceiveTimeout = ms; _tcp.SendTimeout = ms; }
            }
            catch { }
        }

        public void Connect()
        {
            _tcp = new TcpClient();
            _tcp.NoDelay = true;
            _tcp.LingerState = new LingerOption(true, 3);
            var ar = _tcp.BeginConnect(_c.FtpHost, _c.FtpPort, null, null);
            if (!ar.AsyncWaitHandle.WaitOne(20000)) throw new Exception("Timeout ao conectar no FTP.");
            _tcp.EndConnect(ar);
            _ns = _tcp.GetStream();
            SetIoTimeout(CommandTimeoutMs);
            _reader = new StreamReader(_ns, Encoding.ASCII, false, 1024, true);
            _writer = new StreamWriter(_ns, Encoding.ASCII, 1024, true) { NewLine = "\r\n", AutoFlush = true };
            string banner;
            try
            {
                banner = ReadReply();
            }
            catch (IOException)
            {
                throw new Exception("O servidor FTP aceitou a conexao mas nao enviou resposta. No servidor, feche e abra iniciar-ftp.bat (VERSAO 2).");
            }
            Assert(banner, 220);
            _xstor = banner.IndexOf("XSTOR=1", StringComparison.OrdinalIgnoreCase) >= 0;
            Assert(Send("USER " + _c.FtpUser), 230, 331);
            if (Code(Last) == 331) Assert(Send("PASS " + _c.FtpPassword, true), 230, 202);
            Send("TYPE I");
            if (!_xstor) Log.Write("Servidor sem XSTOR=1. Use o FTP VERSAO 2.", "WARN");
        }

        string Last;

        int Code(string r)
        {
            int n;
            if (r != null && r.Length >= 3 && int.TryParse(r.Substring(0, 3), out n)) return n;
            return 0;
        }

        List<string> ReadReplyLines()
        {
            var lines = new List<string>();
            while (true)
            {
                var line = _reader.ReadLine();
                if (line == null) throw new Exception("FTP desconectou.");
                Log.Write("FTP < " + line);
                lines.Add(line);
                if (line.Length >= 4 && line[3] == ' ') break;
                if (line.Length == 3) break;
            }
            Last = lines[lines.Count - 1];
            return lines;
        }

        string ReadReply()
        {
            var lines = ReadReplyLines();
            return lines[lines.Count - 1];
        }

        string Send(string cmd, bool hide = false)
        {
            Log.Write(hide ? "FTP > (oculto)" : "FTP > " + cmd);
            _writer.WriteLine(cmd);
            return ReadReply();
        }

        void Assert(string reply, params int[] ok)
        {
            var n = Code(reply);
            foreach (var x in ok) if (n == x) return;
            throw new Exception("FTP: " + reply);
        }

        public void EnsurePath(string relative)
        {
            Send("CWD /");
            var parts = (relative ?? "").Replace('\\', '/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                if (part == "Backup" || part == "Backup2" || Regex.IsMatch(part, @"^[A-Za-z]:$")) continue;
                var cwd = Send("CWD " + part);
                var code = Code(cwd);
                if (code == 250 || code == 200) continue;
                var mk = Send("MKD " + part);
                var mc = Code(mk);
                if (mc != 257 && mc != 250 && mc != 200 && mc != 550)
                    throw new Exception("Nao criou pasta " + part + ": " + mk);
                Assert(Send("CWD " + part), 250, 200);
            }
        }

        public void Store(string localFile, string remoteName)
        {
            var fi = new FileInfo(localFile);
            if (_xstor)
            {
                _writer.WriteLine("XSTOR " + remoteName + " " + fi.Length);
                Log.Write("FTP > XSTOR " + remoteName + " " + fi.Length);
                SetIoTimeout(TransferTimeoutMs);
                try
                {
                    var r = ReadReply();
                    if (Code(r) == 502) throw new Exception("Servidor FTP antigo. Abra iniciar-ftp.bat VERSAO 2.");
                    Assert(r, 150, 125);
                    using (var fs = File.OpenRead(localFile))
                    {
                        var buf = new byte[65536];
                        int n; long sent = 0; int last = -1;
                        while ((n = fs.Read(buf, 0, buf.Length)) > 0)
                        {
                            _ns.Write(buf, 0, n);
                            sent += n;
                            var pct = fi.Length > 0 ? (int)(sent * 100 / fi.Length) : 100;
                            if (pct >= last + 10 || pct == 100)
                            {
                                last = pct;
                                StatusFile.Set("ftp", "Enviando " + remoteName, pct);
                            }
                        }
                        _ns.Flush();
                    }
                    Assert(ReadReply(), 226, 250);
                }
                finally { SetIoTimeout(CommandTimeoutMs); }
            }
            else throw new Exception("Este cliente envia pela porta 9099 (XSTOR). Ligue o FTP VERSAO 2 no servidor (iniciar-ftp.bat).");
        }

        public void CleanupCycles(string remoteDir, int keep)
        {
            Cleanup(remoteDir, keep);
        }

        public void Cleanup(string remoteDir, int keep)
        {
            keep = Math.Max(1, keep);
            try
            {
                Send("CWD /");
                EnsurePath(remoteDir.Replace("E:/Backup2", "").Replace("E:\\Backup2", "").Replace("E:/Backup", "").Replace("E:\\Backup", "").Trim('/').Trim('\\'));
                var names = ListNames();
                var cycles = new List<string>();
                foreach (var n in names) if (Naming.IsCycleFolder(n)) cycles.Add(n);
                cycles.Sort(StringComparer.Ordinal);
                while (cycles.Count > keep)
                {
                    var old = cycles[0];
                    cycles.RemoveAt(0);
                    Send("CWD " + old);
                    foreach (var f in ListNames())
                    {
                        if (f == "." || f == "..") continue;
                        Send("DELE " + f);
                    }
                    Send("CDUP");
                    Send("RMD " + old);
                    Log.Write("Pasta FTP antiga removida: " + old);
                }
            }
            catch (Exception ex) { Log.Write("Limpeza FTP: " + ex.Message, "WARN"); }
        }

        List<string> ListNames()
        {
            Log.Write("FTP > XDIR");
            _writer.WriteLine("XDIR");
            var lines = ReadReplyLines();
            var list = new List<string>();
            foreach (var line in lines)
            {
                if (line.Length >= 4 && line.StartsWith("211-"))
                {
                    var name = line.Substring(4).Trim();
                    if (name.Length > 0 && name != "Lista") list.Add(name);
                }
            }
            return list;
        }

        public void Dispose()
        {
            try
            {
                SetIoTimeout(3000);
                if (_writer != null)
                {
                    _writer.WriteLine("QUIT");
                    try { ReadReply(); } catch { }
                }
            }
            catch { }
            try { if (_writer != null) _writer.Dispose(); } catch { }
            try { if (_reader != null) _reader.Dispose(); } catch { }
            try { if (_ns != null) _ns.Dispose(); } catch { }
            try { if (_tcp != null) _tcp.Close(); } catch { }
        }
    }
}
