using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace BackupFdbPainel
{
    public sealed class PainelConfig
    {
        public static string FtpHost = "ftp.exemplo.com";
        public static int FtpPort = 9099;

        static PainelConfig()
        {
            LoadNativo();
        }

        static void LoadNativo()
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
            var names = new[] { "ftp-nativo.ini", "ftp-nativo.ini.example" };
            for (var i = 0; i < names.Length; i++)
            {
                var p = Path.Combine(dir, names[i]);
                if (!File.Exists(p)) continue;
                foreach (var raw in File.ReadAllLines(p, Encoding.UTF8))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#") || line.StartsWith("[")) continue;
                    var eq = line.IndexOf('=');
                    if (eq < 1) continue;
                    var key = line.Substring(0, eq).Trim();
                    var val = line.Substring(eq + 1).Trim();
                    if (key == "Host" && val.Length > 0) FtpHost = val;
                    else if (key == "Port")
                    {
                        int n;
                        if (int.TryParse(val, out n) && n > 0) FtpPort = n;
                    }
                }
                break;
            }
        }

        public string Email = "";
        public string Password = "";
        public string PastaLocal = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BackupsClientes");
        public int TimeoutSegundos = 3600;

        public string Host { get { return FtpHost; } }
        public int Port { get { return FtpPort; } }
        public string User { get { return Email; } }

        public bool HasConta
        {
            get
            {
                return !string.IsNullOrWhiteSpace(Email)
                    && Email.IndexOf('@') > 0
                    && !string.IsNullOrEmpty(Password);
            }
        }

        public static string FilePath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\'), "config-painel.ini"); }
        }

        public static PainelConfig Load()
        {
            var c = new PainelConfig();
            var p = FilePath;
            if (!File.Exists(p)) return c;
            foreach (var raw in File.ReadAllLines(p, Encoding.UTF8))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#") || line.StartsWith("[")) continue;
                var eq = line.IndexOf('=');
                if (eq < 1) continue;
                var key = line.Substring(0, eq).Trim();
                var val = line.Substring(eq + 1).Trim();
                if (key == "Email" || key == "User") c.Email = val;
                else if (key == "Password") c.Password = val;
                else if (key == "PastaLocal" && val.Length > 0) c.PastaLocal = val;
                else if (key == "TimeoutSegundos") { int n; if (int.TryParse(val, out n)) c.TimeoutSegundos = n; }
            }
            if (c.Email.IndexOf('@') < 0)
            {
                c.Email = "";
                c.Password = "";
            }
            return c;
        }

        public void Save()
        {
            var lines = new[]
            {
                "; Painel do representante. O FTP ja vem no programa.",
                "; So e-mail e senha cadastrados no sqlite.db do servidor.",
                "[Conta]",
                "Email=" + Email,
                "Password=" + Password,
                "TimeoutSegundos=" + TimeoutSegundos,
                "",
                "[Local]",
                "PastaLocal=" + PastaLocal,
                ""
            };
            File.WriteAllLines(FilePath, lines, new UTF8Encoding(true));
        }
    }

    public sealed class RemoteItem
    {
        public bool IsDir;
        public string VirtualPath = "";
        public long Size;
        public DateTime Time;
        public string Name
        {
            get
            {
                var p = (VirtualPath ?? "").Replace('\\', '/').TrimEnd('/');
                var i = p.LastIndexOf('/');
                return i >= 0 ? p.Substring(i + 1) : p;
            }
        }
        public string ClientFolder
        {
            get
            {
                var p = (VirtualPath ?? "").Replace('\\', '/').Trim('/');
                if (p.Length == 0) return "";
                var i = p.IndexOf('/');
                return i < 0 ? p : p.Substring(0, i);
            }
        }
        public string RelDir
        {
            get
            {
                var p = (VirtualPath ?? "").Replace('\\', '/').Trim('/');
                var i = p.LastIndexOf('/');
                if (i <= 0) return "";
                var rest = p.Substring(p.IndexOf('/') + 1);
                var j = rest.LastIndexOf('/');
                return j < 0 ? "" : rest.Substring(0, j);
            }
        }
    }

    public sealed class ClientRow
    {
        public string Folder, Cnpj, Nome, Situacao;
        public DateTime Last;
        public int Completas, DoDia, Arquivos;
        public long Bytes;
        public List<RemoteItem> Files = new List<RemoteItem>();
    }

    public sealed class CadeiaBackup
    {
        public string Pasta = "";
        public RemoteItem Completo;
        public List<RemoteItem> Incrementais = new List<RemoteItem>();
    }

    public sealed class FtpPainel : IDisposable
    {
        readonly PainelConfig _c;
        TcpClient _tcp;
        NetworkStream _ns;
        public event Action<string> LogLine;
        public event Action<int> Progress;

        public FtpPainel(PainelConfig c) { _c = c; }

        public void Connect()
        {
            if (!_c.HasConta)
                throw new Exception("Informe o e-mail e a senha do representante.");
            try
            {
                _tcp = new TcpClient();
                _tcp.NoDelay = true;
                var ar = _tcp.BeginConnect(PainelConfig.FtpHost, PainelConfig.FtpPort, null, null);
                if (!ar.AsyncWaitHandle.WaitOne(20000))
                    throw new Exception(MsgServidorOff());
                _tcp.EndConnect(ar);
            }
            catch (SocketException)
            {
                throw new Exception(MsgServidorOff());
            }
            catch (Exception ex)
            {
                if (ex is SocketException || (ex.InnerException is SocketException))
                    throw new Exception(MsgServidorOff());
                throw;
            }
            _ns = _tcp.GetStream();
            SetTimeout(30000);
            var banner = ReadLine();
            if (banner == null || !banner.StartsWith("220")) throw new Exception("Servidor FTP nao respondeu. Confira iniciar-ftp.bat no servidor.");
            if (banner.IndexOf("XRETR=1", StringComparison.OrdinalIgnoreCase) < 0)
                throw new Exception("Este servidor ainda nao envia arquivo para o painel. Feche e abra iniciar-ftp.bat no servidor.");
            WriteLine("USER " + _c.Email);
            var r = ReadLine();
            if (Code(r) == 331)
            {
                WriteLine("PASS " + _c.Password);
                r = ReadLine();
            }
            if (Code(r) != 230 && Code(r) != 202)
                throw new Exception("E-mail ou senha invalidos. Use a conta cadastrada no sqlite.db.");
            WriteLine("TYPE I");
            ReadLine();
            Emit("FTP ok em " + PainelConfig.FtpHost + ":" + PainelConfig.FtpPort + " | conta " + _c.Email);
        }

        static string MsgServidorOff()
        {
            return "O FTP em " + PainelConfig.FtpHost + ":" + PainelConfig.FtpPort +
                " recusou a conexao. No computador do servidor, deixe iniciar-ftp.bat aberto e a porta 9099 encaminhada.";
        }

        public List<RemoteItem> Tree()
        {
            WriteLine("CWD /");
            ReadLine();
            WriteLine("XTREE");
            var list = new List<RemoteItem>();
            while (true)
            {
                var line = ReadLine();
                if (line == null) throw new Exception("Servidor desconectou.");
                if (line.StartsWith("211 ")) break;
                if (!line.StartsWith("211-")) continue;
                var body = line.Substring(4);
                if (string.Equals(body, "Lista", StringComparison.OrdinalIgnoreCase)) continue;
                var p = body.Split('|');
                if (p.Length < 4) continue;
                var it = new RemoteItem();
                it.IsDir = p[0] == "D";
                it.VirtualPath = p[1];
                long sz;
                long.TryParse(p[2], out sz);
                it.Size = sz;
                DateTime t;
                DateTime.TryParse(p[3], out t);
                it.Time = t;
                list.Add(it);
            }
            return list;
        }

        public void Download(string virtualPath, string localFile)
        {
            var dir = Path.GetDirectoryName(localFile);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            WriteLine("XRETR " + virtualPath);
            var r = ReadLine();
            if (Code(r) != 150) throw new Exception(r ?? "Falha ao pedir o arquivo.");
            long size = 0;
            var parts = r.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0) long.TryParse(parts[parts.Length - 1], out size);
            WriteLine("XRGO");
            SetTimeout(Math.Max(120, _c.TimeoutSegundos) * 1000);
            try
            {
                using (var fs = File.Create(localFile))
                {
                    var buf = new byte[65536];
                    long left = size;
                    long got = 0;
                    int last = -1;
                    while (left > 0)
                    {
                        var want = (int)Math.Min(buf.Length, left);
                        var n = _ns.Read(buf, 0, want);
                        if (n <= 0) throw new Exception("Conexao fechada no meio do download.");
                        fs.Write(buf, 0, n);
                        left -= n;
                        got += n;
                        var pct = size > 0 ? (int)(got * 100 / size) : 100;
                        if (pct >= last + 5 || pct == 100)
                        {
                            last = pct;
                            var ev = Progress;
                            if (ev != null) ev(pct);
                        }
                    }
                }
            }
            finally { SetTimeout(30000); }
            r = ReadLine();
            if (Code(r) != 226 && Code(r) != 250) throw new Exception(r ?? "Download incompleto.");
            Emit("Baixado: " + Path.GetFileName(localFile));
        }

        public static List<ClientRow> GroupClients(IList<RemoteItem> tree)
        {
            var map = new Dictionary<string, ClientRow>(StringComparer.OrdinalIgnoreCase);
            if (tree == null) return new List<ClientRow>();
            foreach (var it in tree)
            {
                var folder = it.ClientFolder;
                if (string.IsNullOrEmpty(folder)) continue;
                if (folder.StartsWith("_", StringComparison.Ordinal)) continue;
                ClientRow row;
                if (!map.TryGetValue(folder, out row))
                {
                    row = new ClientRow { Folder = folder };
                    SplitFolder(folder, out row.Cnpj, out row.Nome);
                    map[folder] = row;
                }
                if (it.IsDir) continue;
                if (it.Time > row.Last) row.Last = it.Time;
                row.Bytes += it.Size;
                row.Arquivos++;
                var name = it.Name ?? "";
                if (name.StartsWith("Completo_", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".nbk", StringComparison.OrdinalIgnoreCase))
                    row.Completas++;
                else if (name.StartsWith("Incremental_", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".nbk", StringComparison.OrdinalIgnoreCase))
                    row.DoDia++;
                if (name.EndsWith(".nbk", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Como-restaurar.txt", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("como-montar.txt", StringComparison.OrdinalIgnoreCase))
                    row.Files.Add(it);
            }
            var list = new List<ClientRow>();
            foreach (var row in map.Values)
            {
                if (row.Last == DateTime.MinValue) row.Situacao = "Sem copias";
                else if (row.Completas == 0) row.Situacao = "Sem completo";
                else if (row.Last >= DateTime.Now.AddHours(-36)) row.Situacao = "Em dia";
                else row.Situacao = "Atrasado";
                row.Files.Sort((a, b) => b.Time.CompareTo(a.Time));
                list.Add(row);
            }
            list.Sort((a, b) => string.Compare(a.Cnpj, b.Cnpj, StringComparison.OrdinalIgnoreCase));
            return list;
        }

        public static bool IsCompleto(RemoteItem f)
        {
            var n = f == null ? "" : (f.Name ?? "");
            return n.StartsWith("Completo_", StringComparison.OrdinalIgnoreCase)
                && n.EndsWith(".nbk", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsIncremental(RemoteItem f)
        {
            var n = f == null ? "" : (f.Name ?? "");
            return n.StartsWith("Incremental_", StringComparison.OrdinalIgnoreCase)
                && n.EndsWith(".nbk", StringComparison.OrdinalIgnoreCase);
        }

        public static List<CadeiaBackup> BuildCadeias(IList<RemoteItem> files)
        {
            var list = new List<CadeiaBackup>();
            if (files == null) return list;
            var groups = new Dictionary<string, List<RemoteItem>>(StringComparer.OrdinalIgnoreCase);
            var semPasta = new List<RemoteItem>();
            foreach (var f in files)
            {
                if (f == null || f.IsDir) continue;
                if (!IsCompleto(f) && !IsIncremental(f)) continue;
                var dir = f.RelDir ?? "";
                if (dir.Length == 0) { semPasta.Add(f); continue; }
                List<RemoteItem> bucket;
                if (!groups.TryGetValue(dir, out bucket))
                {
                    bucket = new List<RemoteItem>();
                    groups[dir] = bucket;
                }
                bucket.Add(f);
            }
            foreach (var kv in groups)
            {
                var cadeia = FromGroup(kv.Key, kv.Value);
                if (cadeia.Completo != null) list.Add(cadeia);
            }
            if (semPasta.Count > 0)
            {
                semPasta.Sort((a, b) => a.Time.CompareTo(b.Time));
                CadeiaBackup cur = null;
                foreach (var f in semPasta)
                {
                    if (IsCompleto(f))
                    {
                        cur = new CadeiaBackup { Pasta = "", Completo = f };
                        list.Add(cur);
                    }
                    else if (cur != null)
                    {
                        cur.Incrementais.Add(f);
                    }
                }
            }
            list.Sort((a, b) =>
            {
                var ta = a.Completo == null ? DateTime.MinValue : a.Completo.Time;
                var tb = b.Completo == null ? DateTime.MinValue : b.Completo.Time;
                return tb.CompareTo(ta);
            });
            return list;
        }

        public static CadeiaBackup ResolverCadeia(IList<RemoteItem> files, RemoteItem selecionado)
        {
            var cadeias = BuildCadeias(files);
            if (cadeias.Count == 0) return null;
            if (selecionado != null)
            {
                var selPath = selecionado.VirtualPath ?? "";
                var selDir = selecionado.RelDir ?? "";
                foreach (var c in cadeias)
                {
                    if (c.Completo != null && string.Equals(c.Completo.VirtualPath, selPath, StringComparison.OrdinalIgnoreCase))
                        return c;
                    if (selDir.Length > 0 && string.Equals(c.Pasta, selDir, StringComparison.OrdinalIgnoreCase))
                        return c;
                    foreach (var inc in c.Incrementais)
                    {
                        if (string.Equals(inc.VirtualPath, selPath, StringComparison.OrdinalIgnoreCase))
                            return c;
                    }
                }
            }
            return cadeias[0];
        }

        static CadeiaBackup FromGroup(string pasta, List<RemoteItem> files)
        {
            var cadeia = new CadeiaBackup { Pasta = pasta ?? "" };
            foreach (var f in files)
            {
                if (IsCompleto(f))
                {
                    if (cadeia.Completo == null || f.Time < cadeia.Completo.Time)
                        cadeia.Completo = f;
                }
                else if (IsIncremental(f))
                    cadeia.Incrementais.Add(f);
            }
            cadeia.Incrementais.Sort((a, b) => a.Time.CompareTo(b.Time));
            return cadeia;
        }

        static void SplitFolder(string folder, out string cnpj, out string nome)
        {
            var i = folder.IndexOf('_');
            if (i > 0)
            {
                cnpj = folder.Substring(0, i);
                nome = folder.Substring(i + 1).Replace('_', ' ');
            }
            else
            {
                cnpj = folder;
                nome = "";
            }
        }

        public static string Size(long n)
        {
            if (n < 1024) return n + " B";
            if (n < 1024 * 1024) return (n / 1024.0).ToString("0") + " KB";
            if (n < 1024L * 1024 * 1024) return (n / (1024.0 * 1024)).ToString("0.00") + " MB";
            return (n / (1024.0 * 1024 * 1024)).ToString("0.00") + " GB";
        }

        void SetTimeout(int ms)
        {
            try
            {
                if (_ns != null) { _ns.ReadTimeout = ms; _ns.WriteTimeout = ms; }
                if (_tcp != null) { _tcp.ReceiveTimeout = ms; _tcp.SendTimeout = ms; }
            }
            catch { }
        }

        void WriteLine(string s)
        {
            var b = Encoding.ASCII.GetBytes(s + "\r\n");
            _ns.Write(b, 0, b.Length);
            _ns.Flush();
        }

        string ReadLine()
        {
            var sb = new StringBuilder();
            while (true)
            {
                var n = _ns.ReadByte();
                if (n < 0) return sb.Length == 0 ? null : sb.ToString();
                if (n == '\n') break;
                if (n != '\r') sb.Append((char)n);
            }
            return sb.ToString();
        }

        static int Code(string r)
        {
            int n;
            if (r != null && r.Length >= 3 && int.TryParse(r.Substring(0, 3), out n)) return n;
            return 0;
        }

        void Emit(string msg)
        {
            var ev = LogLine;
            if (ev != null) ev(msg);
        }

        public void Dispose()
        {
            try
            {
                if (_ns != null)
                {
                    WriteLine("QUIT");
                    try { ReadLine(); } catch { }
                }
            }
            catch { }
            try { if (_ns != null) _ns.Dispose(); } catch { }
            try { if (_tcp != null) _tcp.Close(); } catch { }
        }
    }
}
