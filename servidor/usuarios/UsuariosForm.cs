using System;
using System.Drawing;
using System.Windows.Forms;

namespace BackupFdbUsuarios
{
    sealed class LoginForm : Form
    {
        readonly TextBox _email;
        readonly TextBox _pass;
        public string Email { get; private set; }

        public LoginForm()
        {
            Text = "Usuarios de download";
            Font = new Font("Segoe UI", 9.5f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(440, 220);
            BackColor = Color.White;

            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 64,
                BackColor = Color.FromArgb(15, 76, 129)
            };
            header.Controls.Add(new Label
            {
                Text = "Superuser",
                Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(18, 8),
                AutoSize = true
            });
            header.Controls.Add(new Label
            {
                Text = UsersDb.CountUsers() == 0
                    ? "Crie o primeiro superuser (e-mail e senha)."
                    : "Entre para cadastrar quem pode baixar as copias.",
                ForeColor = Color.FromArgb(191, 219, 254),
                Location = new Point(20, 38),
                AutoSize = true
            });
            Controls.Add(header);

            Controls.Add(new Label { Text = "E-mail", Location = new Point(20, 84), AutoSize = true });
            _email = new TextBox
            {
                Location = new Point(100, 80),
                Size = new Size(320, 26),
                Text = UsersDb.SuperEmail
            };
            Controls.Add(_email);

            Controls.Add(new Label { Text = "Senha", Location = new Point(20, 122), AutoSize = true });
            _pass = new TextBox
            {
                Location = new Point(100, 118),
                Size = new Size(320, 26),
                UseSystemPasswordChar = true
            };
            Controls.Add(_pass);

            var ok = new Button
            {
                Text = UsersDb.CountUsers() == 0 ? "Criar" : "Entrar",
                Location = new Point(230, 168),
                Size = new Size(90, 32),
                BackColor = Color.FromArgb(37, 99, 235),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            ok.FlatAppearance.BorderSize = 0;
            var cancel = new Button
            {
                Text = "Cancelar",
                DialogResult = DialogResult.Cancel,
                Location = new Point(330, 168),
                Size = new Size(90, 32)
            };
            Controls.Add(ok);
            Controls.Add(cancel);
            AcceptButton = ok;
            CancelButton = cancel;
            ok.Click += OnOk;
        }

        void OnOk(object sender, EventArgs e)
        {
            try
            {
                if (UsersDb.CountUsers() == 0)
                {
                    UsersDb.CreateFirstSuperuser(_email.Text, _pass.Text, "Superuser");
                    Email = _email.Text.Trim();
                    DialogResult = DialogResult.OK;
                    Close();
                    return;
                }
                var u = UsersDb.Authenticate(_email.Text, _pass.Text);
                if (u == null || !u.IsSuperuser)
                    throw new Exception("E-mail ou senha invalidos.");
                Email = u.Email;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    sealed class UsuariosForm : Form
    {
        static readonly Color Navy = Color.FromArgb(15, 76, 129);
        static readonly Color Accent = Color.FromArgb(37, 99, 235);
        static readonly Color Mute = Color.FromArgb(71, 85, 105);
        static readonly Color Bg = Color.FromArgb(241, 245, 249);

        readonly ListView _list;
        readonly Label _status;

        public UsuariosForm(string superEmail)
        {
            Text = "Usuarios de download";
            Font = new Font("Segoe UI", 9.5f);
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(760, 520);
            Size = new Size(820, 560);
            BackColor = Bg;

            var header = new Panel { Dock = DockStyle.Top, Height = 72, BackColor = Navy };
            header.Controls.Add(new Label
            {
                Text = "Usuarios de download",
                Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(20, 8),
                AutoSize = true
            });
            header.Controls.Add(new Label
            {
                Text = "Quem entra no painel com e-mail e senha. Logado: " + superEmail,
                ForeColor = Color.FromArgb(191, 219, 254),
                Location = new Point(22, 42),
                AutoSize = true
            });
            Controls.Add(header);

            var footer = new Panel { Dock = DockStyle.Bottom, Height = 56, BackColor = Bg };
            _status = new Label
            {
                Text = UsersDb.DbPath,
                Location = new Point(16, 16),
                Size = new Size(420, 24),
                ForeColor = Mute
            };
            footer.Controls.Add(_status);
            var btnAdd = MkBtn("Novo usuario", 450, true);
            var btnPass = MkBtn("Trocar senha", 570, false);
            var btnClose = MkBtn("Fechar", 690, false);
            btnClose.Click += (s, e) => Close();
            btnAdd.Click += (s, e) => Novo();
            btnPass.Click += (s, e) => TrocarSenha();
            footer.Controls.Add(btnAdd);
            footer.Controls.Add(btnPass);
            footer.Controls.Add(btnClose);
            Controls.Add(footer);

            var mid = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 12, 16, 8) };
            Controls.Add(mid);
            mid.BringToFront();

            var actions = new Panel { Dock = DockStyle.Right, Width = 150 };
            var btnNome = MkSide("Alterar nome");
            var btnAtivo = MkSide("Ativar / pausar");
            var btnDel = MkSide("Apagar");
            btnNome.Location = new Point(8, 8);
            btnAtivo.Location = new Point(8, 48);
            btnDel.Location = new Point(8, 88);
            btnNome.Click += (s, e) => AlterarNome();
            btnAtivo.Click += (s, e) => ToggleAtivo();
            btnDel.Click += (s, e) => Apagar();
            actions.Controls.Add(btnNome);
            actions.Controls.Add(btnAtivo);
            actions.Controls.Add(btnDel);
            mid.Controls.Add(actions);

            _list = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                HideSelection = false,
                BackColor = Color.White
            };
            _list.Columns.Add("E-mail", 260);
            _list.Columns.Add("Nome", 160);
            _list.Columns.Add("Tipo", 90);
            _list.Columns.Add("Situacao", 80);
            _list.Columns.Add("Atualizado", 140);
            _list.DoubleClick += (s, e) => TrocarSenha();
            mid.Controls.Add(_list);
            _list.BringToFront();
            Reload();
        }

        static Button MkBtn(string text, int x, bool primary)
        {
            var b = new Button
            {
                Text = text,
                Location = new Point(x, 12),
                Size = new Size(110, 32),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            if (primary)
            {
                b.BackColor = Accent;
                b.ForeColor = Color.White;
                b.FlatAppearance.BorderSize = 0;
            }
            else
            {
                b.BackColor = Color.White;
                b.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            }
            return b;
        }

        static Button MkSide(string text)
        {
            var b = new Button
            {
                Text = text,
                Size = new Size(134, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            return b;
        }

        FtpUser Selected()
        {
            if (_list.SelectedItems.Count < 1) return null;
            return _list.SelectedItems[0].Tag as FtpUser;
        }

        void Reload()
        {
            _list.Items.Clear();
            foreach (var u in UsersDb.ListAll())
            {
                var it = new ListViewItem(u.Email);
                it.SubItems.Add(u.Nome);
                it.SubItems.Add(u.IsSuperuser ? "Superuser" : "Download");
                it.SubItems.Add(u.Ativo ? "Ativo" : "Pausado");
                it.SubItems.Add(u.AtualizadoEm);
                it.Tag = u;
                if (!u.Ativo) it.ForeColor = Mute;
                _list.Items.Add(it);
            }
            _status.Text = "Banco: " + UsersDb.DbPath;
        }

        void Novo()
        {
            using (var f = new UsuarioEditForm(null))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    UsersDb.Add(f.Email, f.Password, f.Nome);
                    Reload();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        void AlterarNome()
        {
            var u = Selected();
            if (u == null)
            {
                MessageBox.Show(this, "Selecione um usuario.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (u.IsSuperuser)
            {
                MessageBox.Show(this, "O superuser nao pode ser alterado aqui.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var nome = Prompt("Nome", "Nome de exibicao:", u.Nome);
            if (nome == null) return;
            try
            {
                UsersDb.UpdateNome(u.Id, nome);
                Reload();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void TrocarSenha()
        {
            var u = Selected();
            if (u == null)
            {
                MessageBox.Show(this, "Selecione um usuario.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var senha = PromptSenha(u.Email);
            if (senha == null) return;
            try
            {
                UsersDb.ChangePassword(u.Id, senha);
                MessageBox.Show(this, "Senha atualizada.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                Reload();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void ToggleAtivo()
        {
            var u = Selected();
            if (u == null)
            {
                MessageBox.Show(this, "Selecione um usuario.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                UsersDb.SetAtivo(u.Id, !u.Ativo);
                Reload();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void Apagar()
        {
            var u = Selected();
            if (u == null)
            {
                MessageBox.Show(this, "Selecione um usuario.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (MessageBox.Show(this, "Apagar " + u.Email + "?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            try
            {
                UsersDb.Delete(u.Id);
                Reload();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        string Prompt(string title, string label, string value)
        {
            using (var f = new Form())
            {
                f.Text = title;
                f.FormBorderStyle = FormBorderStyle.FixedDialog;
                f.StartPosition = FormStartPosition.CenterParent;
                f.ClientSize = new Size(400, 140);
                f.MinimizeBox = false;
                f.MaximizeBox = false;
                f.Font = Font;
                f.Controls.Add(new Label { Text = label, Location = new Point(16, 16), AutoSize = true });
                var tb = new TextBox { Location = new Point(16, 40), Size = new Size(368, 26), Text = value ?? "" };
                f.Controls.Add(tb);
                var ok = new Button { Text = "Salvar", DialogResult = DialogResult.OK, Location = new Point(204, 90), Size = new Size(90, 32) };
                var cancel = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Location = new Point(300, 90), Size = new Size(84, 32) };
                f.Controls.Add(ok);
                f.Controls.Add(cancel);
                f.AcceptButton = ok;
                f.CancelButton = cancel;
                return f.ShowDialog(this) == DialogResult.OK ? tb.Text : null;
            }
        }

        string PromptSenha(string email)
        {
            using (var f = new Form())
            {
                f.Text = "Trocar senha";
                f.FormBorderStyle = FormBorderStyle.FixedDialog;
                f.StartPosition = FormStartPosition.CenterParent;
                f.ClientSize = new Size(400, 170);
                f.MinimizeBox = false;
                f.MaximizeBox = false;
                f.Font = Font;
                f.Controls.Add(new Label { Text = email, Location = new Point(16, 16), AutoSize = true });
                f.Controls.Add(new Label { Text = "Nova senha", Location = new Point(16, 48), AutoSize = true });
                var tb = new TextBox { Location = new Point(110, 44), Size = new Size(274, 26), UseSystemPasswordChar = true };
                f.Controls.Add(tb);
                f.Controls.Add(new Label { Text = "Confirmar", Location = new Point(16, 84), AutoSize = true });
                var tb2 = new TextBox { Location = new Point(110, 80), Size = new Size(274, 26), UseSystemPasswordChar = true };
                f.Controls.Add(tb2);
                var ok = new Button { Text = "Salvar", Location = new Point(204, 122), Size = new Size(90, 32) };
                var cancel = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Location = new Point(300, 122), Size = new Size(84, 32) };
                f.Controls.Add(ok);
                f.Controls.Add(cancel);
                f.AcceptButton = ok;
                f.CancelButton = cancel;
                string result = null;
                ok.Click += (s, e) =>
                {
                    if (tb.Text != tb2.Text)
                    {
                        MessageBox.Show(f, "As senhas nao conferem.", f.Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    result = tb.Text;
                    f.DialogResult = DialogResult.OK;
                    f.Close();
                };
                return f.ShowDialog(this) == DialogResult.OK ? result : null;
            }
        }
    }

    sealed class UsuarioEditForm : Form
    {
        readonly TextBox _email, _nome, _pass, _pass2;
        public string Email { get { return _email.Text.Trim(); } }
        public string Nome { get { return _nome.Text.Trim(); } }
        public string Password { get { return _pass.Text; } }

        public UsuarioEditForm(FtpUser existing)
        {
            Text = existing == null ? "Novo usuario" : "Editar usuario";
            Font = new Font("Segoe UI", 9.5f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(440, 250);
            BackColor = Color.White;

            Controls.Add(new Label { Text = "E-mail", Location = new Point(20, 24), AutoSize = true });
            _email = new TextBox { Location = new Point(120, 20), Size = new Size(300, 26) };
            Controls.Add(_email);
            Controls.Add(new Label { Text = "Nome", Location = new Point(20, 62), AutoSize = true });
            _nome = new TextBox { Location = new Point(120, 58), Size = new Size(300, 26) };
            Controls.Add(_nome);
            Controls.Add(new Label { Text = "Senha", Location = new Point(20, 100), AutoSize = true });
            _pass = new TextBox { Location = new Point(120, 96), Size = new Size(300, 26), UseSystemPasswordChar = true };
            Controls.Add(_pass);
            Controls.Add(new Label { Text = "Confirmar", Location = new Point(20, 138), AutoSize = true });
            _pass2 = new TextBox { Location = new Point(120, 134), Size = new Size(300, 26), UseSystemPasswordChar = true };
            Controls.Add(_pass2);

            var ok = new Button { Text = "Salvar", Location = new Point(230, 196), Size = new Size(90, 32) };
            var cancel = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Location = new Point(330, 196), Size = new Size(90, 32) };
            Controls.Add(ok);
            Controls.Add(cancel);
            AcceptButton = ok;
            CancelButton = cancel;
            ok.Click += (s, e) =>
            {
                if (_pass.Text != _pass2.Text)
                {
                    MessageBox.Show(this, "As senhas nao conferem.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                DialogResult = DialogResult.OK;
                Close();
            };
        }
    }
}
