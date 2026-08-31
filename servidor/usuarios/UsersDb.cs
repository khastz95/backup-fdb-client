using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace BackupFdbUsuarios
{
    public sealed class FtpUser
    {
        public long Id;
        public string Email = "";
        public string Nome = "";
        public bool Ativo = true;
        public bool IsSuperuser;
        public string SenhaHash = "";
        public string CriadoEm = "";
        public string AtualizadoEm = "";
    }

    public static class UsersDb
    {
        public const string SuperEmailPadrao = "admin@exemplo.com";
        const int Pbkdf2Rounds = 12000;

        public static string SuperEmail
        {
            get
            {
                var e = ReadServidorIni("Superuser", "Email", "");
                return string.IsNullOrWhiteSpace(e) ? SuperEmailPadrao : e.Trim();
            }
        }

        public static string DbPath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/'), "sqlite.db"); }
        }

        public static void EnsureCreated()
        {
            var cs = new SQLiteConnectionStringBuilder();
            cs.DataSource = DbPath;
            cs.Version = 3;
            cs.FailIfMissing = false;
            using (var con = new SQLiteConnection(cs.ToString()))
            {
                con.Open();
                using (var cmd = con.CreateCommand())
                {
                    cmd.CommandText =
                        "CREATE TABLE IF NOT EXISTS usuarios (" +
                        "id INTEGER PRIMARY KEY AUTOINCREMENT," +
                        "email TEXT NOT NULL UNIQUE COLLATE NOCASE," +
                        "senha_hash TEXT NOT NULL," +
                        "nome TEXT NOT NULL DEFAULT ''," +
                        "ativo INTEGER NOT NULL DEFAULT 1," +
                        "is_superuser INTEGER NOT NULL DEFAULT 0," +
                        "criado_em TEXT NOT NULL," +
                        "atualizado_em TEXT NOT NULL" +
                        ");";
                    cmd.ExecuteNonQuery();
                }
                TrySeedFromIni(con);
            }
        }

        public static int CountUsers()
        {
            using (var con = Open())
            using (var cmd = con.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM usuarios";
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        public static void CreateFirstSuperuser(string email, string password, string nome)
        {
            email = (email ?? "").Trim().ToLowerInvariant();
            if (CountUsers() > 0) throw new Exception("O superuser ja existe.");
            AssertEmail(email);
            AssertPassword(password);
            using (var con = Open())
            {
                if (FindByEmail(con, email) != null)
                    throw new Exception("Ja existe um usuario com este e-mail.");
                Insert(con, email, password, string.IsNullOrWhiteSpace(nome) ? "Superuser" : nome.Trim(), true, true);
            }
        }

        public static SQLiteConnection Open()
        {
            EnsureCreated();
            var cs = new SQLiteConnectionStringBuilder();
            cs.DataSource = DbPath;
            cs.Version = 3;
            cs.FailIfMissing = true;
            var con = new SQLiteConnection(cs.ToString());
            con.Open();
            return con;
        }

        public static FtpUser Authenticate(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email) || password == null) return null;
            using (var con = Open())
            {
                var u = FindByEmail(con, email.Trim());
                if (u == null || !u.Ativo) return null;
                if (!VerifyPassword(password, u.SenhaHash)) return null;
                return u;
            }
        }

        public static List<FtpUser> ListAll()
        {
            var list = new List<FtpUser>();
            using (var con = Open())
            using (var cmd = con.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT id, email, senha_hash, nome, ativo, is_superuser, criado_em, atualizado_em " +
                    "FROM usuarios ORDER BY is_superuser DESC, email COLLATE NOCASE";
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read()) list.Add(ReadUser(r));
                }
            }
            return list;
        }

        public static FtpUser Find(string email)
        {
            using (var con = Open())
                return FindByEmail(con, email);
        }

        public static void Add(string email, string password, string nome)
        {
            email = NormalizeEmail(email);
            AssertEmail(email);
            AssertPassword(password);
            if (string.IsNullOrWhiteSpace(nome)) nome = "";
            using (var con = Open())
            {
                if (FindByEmail(con, email) != null)
                    throw new Exception("Ja existe um usuario com este e-mail.");
                Insert(con, email, password, nome.Trim(), true, false);
            }
        }

        public static void UpdateNome(long id, string nome)
        {
            using (var con = Open())
            using (var cmd = con.CreateCommand())
            {
                cmd.CommandText = "UPDATE usuarios SET nome=@n, atualizado_em=@t WHERE id=@id AND is_superuser=0";
                cmd.Parameters.AddWithValue("@n", nome == null ? "" : nome.Trim());
                cmd.Parameters.AddWithValue("@t", Now());
                cmd.Parameters.AddWithValue("@id", id);
                if (cmd.ExecuteNonQuery() < 1) throw new Exception("Usuario nao encontrado ou e o superuser.");
            }
        }

        public static void SetAtivo(long id, bool ativo)
        {
            using (var con = Open())
            {
                var u = FindById(con, id);
                if (u == null) throw new Exception("Usuario nao encontrado.");
                if (u.IsSuperuser) throw new Exception("O superuser nao pode ser desativado.");
                using (var cmd = con.CreateCommand())
                {
                    cmd.CommandText = "UPDATE usuarios SET ativo=@a, atualizado_em=@t WHERE id=@id";
                    cmd.Parameters.AddWithValue("@a", ativo ? 1 : 0);
                    cmd.Parameters.AddWithValue("@t", Now());
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static void ChangePassword(long id, string password)
        {
            AssertPassword(password);
            using (var con = Open())
            using (var cmd = con.CreateCommand())
            {
                cmd.CommandText = "UPDATE usuarios SET senha_hash=@h, atualizado_em=@t WHERE id=@id";
                cmd.Parameters.AddWithValue("@h", HashPassword(password));
                cmd.Parameters.AddWithValue("@t", Now());
                cmd.Parameters.AddWithValue("@id", id);
                if (cmd.ExecuteNonQuery() < 1) throw new Exception("Usuario nao encontrado.");
            }
        }

        public static void Delete(long id)
        {
            using (var con = Open())
            {
                var u = FindById(con, id);
                if (u == null) throw new Exception("Usuario nao encontrado.");
                if (u.IsSuperuser) throw new Exception("O superuser nao pode ser apagado.");
                using (var cmd = con.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM usuarios WHERE id=@id";
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        static FtpUser FindByEmail(SQLiteConnection con, string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return null;
            using (var cmd = con.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT id, email, senha_hash, nome, ativo, is_superuser, criado_em, atualizado_em " +
                    "FROM usuarios WHERE email=@e LIMIT 1";
                cmd.Parameters.AddWithValue("@e", email.Trim());
                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return null;
                    return ReadUser(r);
                }
            }
        }

        static FtpUser FindById(SQLiteConnection con, long id)
        {
            using (var cmd = con.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT id, email, senha_hash, nome, ativo, is_superuser, criado_em, atualizado_em " +
                    "FROM usuarios WHERE id=@id LIMIT 1";
                cmd.Parameters.AddWithValue("@id", id);
                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return null;
                    return ReadUser(r);
                }
            }
        }

        static void Insert(SQLiteConnection con, string email, string password, string nome, bool ativo, bool superuser)
        {
            var now = Now();
            using (var cmd = con.CreateCommand())
            {
                cmd.CommandText =
                    "INSERT INTO usuarios (email, senha_hash, nome, ativo, is_superuser, criado_em, atualizado_em) " +
                    "VALUES (@e, @h, @n, @a, @s, @c, @u)";
                cmd.Parameters.AddWithValue("@e", email);
                cmd.Parameters.AddWithValue("@h", HashPassword(password));
                cmd.Parameters.AddWithValue("@n", nome ?? "");
                cmd.Parameters.AddWithValue("@a", ativo ? 1 : 0);
                cmd.Parameters.AddWithValue("@s", superuser ? 1 : 0);
                cmd.Parameters.AddWithValue("@c", now);
                cmd.Parameters.AddWithValue("@u", now);
                cmd.ExecuteNonQuery();
            }
        }

        static void TrySeedFromIni(SQLiteConnection con)
        {
            using (var cmd = con.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM usuarios";
                if (Convert.ToInt32(cmd.ExecuteScalar()) > 0) return;
            }
            var email = ReadServidorIni("Superuser", "Email", "");
            var senha = ReadServidorIni("Superuser", "Senha", "");
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(senha)) return;
            if (senha.IndexOf("TROQUE", StringComparison.OrdinalIgnoreCase) >= 0) return;
            Insert(con, email.Trim().ToLowerInvariant(), senha, "Superuser", true, true);
        }

        static string ReadServidorIni(string section, string key, string fallback)
        {
            try
            {
                var dir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
                var path = Path.Combine(dir, "servidor-ftp.ini");
                if (!File.Exists(path)) return fallback;
                var current = "";
                foreach (var raw in File.ReadAllLines(path))
                {
                    var t = (raw ?? "").Trim();
                    if (t.Length == 0 || t.StartsWith(";") || t.StartsWith("#")) continue;
                    if (t.StartsWith("[") && t.EndsWith("]"))
                    {
                        current = t.Substring(1, t.Length - 2);
                        continue;
                    }
                    if (!string.Equals(current, section, StringComparison.OrdinalIgnoreCase)) continue;
                    var eq = t.IndexOf('=');
                    if (eq < 1) continue;
                    if (string.Equals(t.Substring(0, eq).Trim(), key, StringComparison.OrdinalIgnoreCase))
                        return t.Substring(eq + 1).Trim();
                }
            }
            catch { }
            return fallback;
        }

        static FtpUser ReadUser(IDataRecord r)
        {
            var u = new FtpUser();
            u.Id = r.GetInt64(0);
            u.Email = r.GetString(1);
            u.SenhaHash = r.GetString(2);
            u.Nome = r.IsDBNull(3) ? "" : r.GetString(3);
            u.Ativo = Convert.ToInt32(r.GetValue(4)) != 0;
            u.IsSuperuser = Convert.ToInt32(r.GetValue(5)) != 0;
            u.CriadoEm = r.IsDBNull(6) ? "" : r.GetString(6);
            u.AtualizadoEm = r.IsDBNull(7) ? "" : r.GetString(7);
            return u;
        }

        static string NormalizeEmail(string email)
        {
            return (email ?? "").Trim().ToLowerInvariant();
        }

        static void AssertEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email) || email.IndexOf('@') < 1 || email.IndexOf('.') < 0)
                throw new Exception("Informe um e-mail valido.");
        }

        static void AssertPassword(string password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < 8)
                throw new Exception("A senha precisa ter pelo menos 8 caracteres.");
        }

        static string HashPassword(string password)
        {
            var salt = new byte[16];
            using (var rng = new RNGCryptoServiceProvider())
                rng.GetBytes(salt);
            var hash = new Rfc2898DeriveBytes(password, salt, Pbkdf2Rounds).GetBytes(32);
            return Convert.ToBase64String(salt) + ":" + Convert.ToBase64String(hash);
        }

        static bool VerifyPassword(string password, string stored)
        {
            if (string.IsNullOrEmpty(stored) || password == null) return false;
            var i = stored.IndexOf(':');
            if (i < 1) return false;
            byte[] salt;
            byte[] expected;
            try
            {
                salt = Convert.FromBase64String(stored.Substring(0, i));
                expected = Convert.FromBase64String(stored.Substring(i + 1));
            }
            catch
            {
                return false;
            }
            var actual = new Rfc2898DeriveBytes(password, salt, Pbkdf2Rounds).GetBytes(expected.Length);
            return SlowEquals(expected, actual);
        }

        static bool SlowEquals(byte[] a, byte[] b)
        {
            var diff = a.Length ^ b.Length;
            var n = Math.Min(a.Length, b.Length);
            for (var i = 0; i < n; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }

        static string Now()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }
}
