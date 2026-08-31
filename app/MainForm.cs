using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace BackupFdbCliente
{
    sealed class MainForm : Form
    {
        static readonly Color Navy = Color.FromArgb(15, 76, 129);
        static readonly Color Accent = Color.FromArgb(37, 99, 235);
        static readonly Color Ok = Color.FromArgb(21, 128, 61);
        static readonly Color Warn = Color.FromArgb(180, 83, 9);
        static readonly Color Danger = Color.FromArgb(185, 28, 28);
        static readonly Color Bg = Color.FromArgb(241, 245, 249);
        static readonly Color Mute = Color.FromArgb(71, 85, 105);

        readonly Font _ui = new Font("Segoe UI", 9.5f);
        readonly Font _title = new Font("Segoe UI", 16f, FontStyle.Bold);
        readonly Font _section = new Font("Segoe UI", 10.5f, FontStyle.Bold);
        readonly Font _small = new Font("Segoe UI", 8.5f);

        readonly EventWaitHandle _showEvent;
        readonly bool _startHidden;
        bool _busy;
        bool _exitForReal;
        bool _balloonShown;
        NotifyIcon _tray;
        Icon _trayIcon;
        System.Windows.Forms.Timer _poll;
        System.Windows.Forms.Timer _showWatch;

        TextBox _cnpj, _nome, _database, _local, _dbUser, _dbPass, _log, _nbackup;
        DateTimePicker _hora;
        NumericUpDown _diasFull, _manter;
        RadioButton _destLocal, _destFtp;
        Label _destino, _dbStatus, _localStatus, _taskStatus, _status, _nbkStatus;
        ProgressBar _bar;
        ListView _copies;
        Button _btnSave, _btnInstall, _btnRestart, _btnUninstall, _btnClear, _btnNow, _btnLocal, _btnFtp, _btnTest, _btnMontar, _btnFtpDlg, _btnRestore;
        string _montarLevel0;
        AppConfig _ftpHold;

        public MainForm(bool startHidden, EventWaitHandle showEvent)
        {
            _startHidden = startHidden;
            _showEvent = showEvent;
            _ftpHold = Ini.Load(AppPaths.ConfigFile);

            Text = "Backup do sistema";
            Font = _ui;
            BackColor = Bg;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1000, 720);
            Size = new Size(1060, 780);
            AllowDrop = true;
            DoubleBuffered = true;

            BuildUi();
            LoadToForm(_ftpHold);
            RefreshCopies();
            RefreshHints();
            SetupTray();
            SetupWatchers();

            DragEnter += (s, e) =>
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
            };
            DragDrop += OnDrop;
            FormClosing += OnClosing;
            Shown += (s, e) =>
            {
                if (_startHidden)
                {
                    Hide();
                    ShowInTaskbar = false;
                }
            };
        }

        void BuildUi()
        {
            var header = new Panel { Dock = DockStyle.Top, Height = 72, BackColor = Navy };
            header.Controls.Add(new Label
            {
                Text = "Backup do sistema",
                Font = _title,
                ForeColor = Color.White,
                Location = new Point(22, 8),
                AutoSize = true
            });
            header.Controls.Add(new Label
            {
                Text = "Copia de seguranca do banco de dados da sua empresa.",
                Font = _ui,
                ForeColor = Color.FromArgb(191, 219, 254),
                Location = new Point(24, 42),
                AutoSize = true
            });
            Controls.Add(header);

            var footer = new Panel { Dock = DockStyle.Bottom, Height = 132 };
            _status = new Label { Text = "Pronto.", Location = new Point(16, 8), Size = new Size(700, 20), ForeColor = Mute, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            _bar = new ProgressBar { Location = new Point(16, 30), Size = new Size(1010, 10), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            _log = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.White,
                Location = new Point(16, 46),
                Size = new Size(1010, 76),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                Font = new Font("Segoe UI", 8.5f)
            };
            footer.Controls.Add(_status);
            footer.Controls.Add(_bar);
            footer.Controls.Add(_log);
            Controls.Add(footer);

            var content = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(12, 10, 12, 6)
            };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 534));
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            Controls.Add(content);
            content.BringToFront();

            var left = new Panel { Dock = DockStyle.Fill };
            var right = new Panel { Dock = DockStyle.Fill };
            content.Controls.Add(left, 0, 0);
            content.Controls.Add(right, 1, 0);

            var actions = new Panel { Dock = DockStyle.Bottom, Height = 76 };
            var stack = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true
            };
            left.Controls.Add(stack);
            left.Controls.Add(actions);

            var g1 = Card(stack, "1. Sua empresa", 96);
            g1.Controls.Add(L("CNPJ", 14, 28, 50));
            _cnpj = T(g1, 68, 24, 160, "");
            _cnpj.MaxLength = 18;
            g1.Controls.Add(L("Empresa", 240, 28, 62));
            _nome = T(g1, 304, 24, 196, "");
            _destino = new Label { Location = new Point(14, 58), Size = new Size(490, 30), Font = _small, ForeColor = Warn };
            g1.Controls.Add(_destino);
            _cnpj.TextChanged += (s, e) => RefreshHints();
            _nome.TextChanged += (s, e) => RefreshHints();

            var g2 = Card(stack, "2. Banco de dados", 168);
            g2.Controls.Add(L("Arquivo do banco", 14, 26, 108));
            _database = T(g2, 124, 22, 262, "");
            var btnDb = Btn("Procurar...", 394, 20, 106, true);
            g2.Controls.Add(btnDb);
            btnDb.Click += (s, e) => ChooseDb();
            _dbStatus = new Label { Location = new Point(124, 50), Size = new Size(374, 16), Font = _small, ForeColor = Warn };
            g2.Controls.Add(_dbStatus);

            g2.Controls.Add(L("Pasta do Firebird", 14, 72, 108));
            _nbackup = T(g2, 124, 68, 262, "");
            var btnNbk = Btn("Procurar...", 394, 66, 106, true);
            g2.Controls.Add(btnNbk);
            btnNbk.Click += (s, e) => ChooseFirebird();
            _nbkStatus = new Label { Location = new Point(124, 96), Size = new Size(374, 16), Font = _small, ForeColor = Warn };
            g2.Controls.Add(_nbkStatus);

            g2.Controls.Add(L("Usuario", 14, 122, 60));
            _dbUser = T(g2, 74, 118, 140, "SYSDBA");
            g2.Controls.Add(L("Senha", 230, 122, 46));
            _dbPass = T(g2, 278, 118, 140, "");
            _dbPass.UseSystemPasswordChar = true;
            _database.TextChanged += (s, e) => RefreshHints();
            _nbackup.TextChanged += (s, e) => RefreshHints();

            var g3 = Card(stack, "3. Onde guardar neste computador", 96);
            g3.Controls.Add(L("Pasta", 14, 28, 46));
            _local = T(g3, 62, 24, 272, "");
            var btnFolder = Btn("Procurar...", 342, 22, 96, true);
            var btnOpen = Btn("Abrir", 444, 22, 56, false);
            g3.Controls.Add(btnFolder);
            g3.Controls.Add(btnOpen);
            btnFolder.Click += (s, e) => ChooseFolder();
            btnOpen.Click += (s, e) => OpenFolder();
            _localStatus = new Label { Location = new Point(62, 56), Size = new Size(438, 32), Font = _small, ForeColor = Mute };
            g3.Controls.Add(_localStatus);
            _local.TextChanged += (s, e) => RefreshHints();

            var g4 = Card(stack, "4. Quando copiar", 92);
            g4.Controls.Add(L("Todo dia as", 14, 28, 90));
            _hora = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "HH:mm",
                ShowUpDown = true,
                Location = new Point(108, 24),
                Size = new Size(80, 26),
                Font = _ui
            };
            g4.Controls.Add(_hora);
            g4.Controls.Add(L("Copia completa a cada", 204, 28, 140));
            _diasFull = new NumericUpDown { Location = new Point(348, 24), Size = new Size(48, 26), Minimum = 1, Maximum = 30, Value = 7 };
            g4.Controls.Add(_diasFull);
            g4.Controls.Add(L("dias", 400, 28, 36));
            g4.Controls.Add(L("Guardar", 14, 60, 58));
            _manter = new NumericUpDown { Location = new Point(74, 56), Size = new Size(48, 26), Minimum = 1, Maximum = 30, Value = 4 };
            g4.Controls.Add(_manter);
            g4.Controls.Add(L("copias completas (a mais antiga sai)", 128, 60, 280));

            var gDest = Card(stack, "5. Para onde vai a copia", 88);
            _destLocal = new RadioButton
            {
                Text = "Somente neste computador (sem servidor FTP)",
                Location = new Point(14, 24),
                Size = new Size(490, 22),
                Font = _ui,
                AutoSize = false
            };
            _destFtp = new RadioButton
            {
                Text = "Neste computador e no servidor FTP",
                Location = new Point(14, 50),
                Size = new Size(490, 22),
                Font = _ui,
                AutoSize = false,
                Checked = true
            };
            gDest.Controls.Add(_destLocal);
            gDest.Controls.Add(_destFtp);
            _destLocal.CheckedChanged += (s, e) => RefreshHints();
            _destFtp.CheckedChanged += (s, e) => RefreshHints();

            _btnSave = Btn("Salvar", 0, 4, 72, false);
            _btnInstall = Btn("Instalar servico agora", 76, 4, 176, true);
            _btnRestart = Btn("Reiniciar", 256, 4, 86, false);
            _btnFtpDlg = Btn("Servidor...", 346, 4, 96, false);
            _btnUninstall = Btn("Remover servico", 0, 40, 132, false);
            actions.Controls.Add(_btnSave);
            actions.Controls.Add(_btnInstall);
            actions.Controls.Add(_btnRestart);
            actions.Controls.Add(_btnFtpDlg);
            actions.Controls.Add(_btnUninstall);
            _btnSave.Click += (s, e) => SaveClicked();
            _btnInstall.Click += (s, e) => InstallClicked();
            _btnRestart.Click += (s, e) => RestartClicked();
            _btnFtpDlg.Click += (s, e) => OpenFtp();
            _btnUninstall.Click += (s, e) => UninstallClicked();

            var run = new Panel { Dock = DockStyle.Bottom, Height = 118 };
            var g5 = new GroupBox
            {
                Text = "Copias salvas neste computador",
                Font = _section,
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };
            right.Controls.Add(g5);
            right.Controls.Add(run);

            _copies = new ListView
            {
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                Location = new Point(12, 26),
                Size = new Size(430, 240),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Font = _small
            };
            _copies.Columns.Add("Tipo", 90);
            _copies.Columns.Add("Quando", 120);
            _copies.Columns.Add("Arquivo", 150);
            _copies.Columns.Add("Tamanho", 70);
            _copies.DoubleClick += (s, e) => OpenSelectedCopy();
            g5.Controls.Add(_copies);
            var btnRefresh = Btn("Atualizar", 12, 272, 80, false);
            var btnOpenSel = Btn("Abrir pasta", 96, 272, 90, false);
            _btnRestore = Btn("Recuperar...", 190, 272, 100, true);
            _btnClear = Btn("Limpar copias", 294, 272, 108, false);
            btnRefresh.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            btnOpenSel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            _btnRestore.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            _btnClear.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            g5.Controls.Add(btnRefresh);
            g5.Controls.Add(btnOpenSel);
            g5.Controls.Add(_btnRestore);
            g5.Controls.Add(_btnClear);
            btnRefresh.Click += (s, e) => RefreshCopies();
            btnOpenSel.Click += (s, e) => OpenFolder();
            _btnRestore.Click += (s, e) => OpenRestore();
            _btnClear.Click += (s, e) => ClearCopiesClicked();
            g5.Resize += (s, e) =>
            {
                btnRefresh.Top = g5.ClientSize.Height - 40;
                btnOpenSel.Top = btnRefresh.Top;
                _btnRestore.Top = btnRefresh.Top;
                _btnClear.Top = btnRefresh.Top;
                _copies.Height = Math.Max(80, btnRefresh.Top - 32);
            };

            _taskStatus = new Label { Location = new Point(4, 2), Size = new Size(460, 32), Font = _small, ForeColor = Warn, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            run.Controls.Add(_taskStatus);
            _btnNow = Btn("Copiar agora", 0, 38, 140, true);
            _btnLocal = Btn("So neste PC", 148, 38, 108, false);
            _btnFtp = Btn("Testar envio", 264, 38, 108, false);
            _btnTest = Btn("Testar o programa", 0, 74, 140, false);
            _btnMontar = Btn("Gerar para recuperar", 148, 74, 160, false);
            run.Controls.Add(_btnNow);
            run.Controls.Add(_btnLocal);
            run.Controls.Add(_btnFtp);
            run.Controls.Add(_btnTest);
            run.Controls.Add(_btnMontar);
            _btnNow.Click += (s, e) => RunJob("Copia", cfg => Engine.RunBackup(cfg, cfg.EnviarFtp), true);
            _btnLocal.Click += (s, e) => RunJob("Copia neste PC", cfg => Engine.RunBackup(cfg, false), true);
            _btnFtp.Click += (s, e) => RunJob("Teste de envio", Engine.TestFtp, true);
            _btnTest.Click += (s, e) =>
            {
                if (MessageBox.Show(this,
                    "Vamos conferir se o backup esta funcionando.\n\nIsso nao apaga as copias de verdade da empresa.",
                    "Testar o programa", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK)
                    return;
                RunJob("Teste do programa", cfg => Engine.RunTests(cfg), true);
            };
            _btnMontar.Click += (s, e) =>
            {
                if (MessageBox.Show(this,
                    "Vamos gerar 1 copia completa e 2 copias do dia, so para teste de recuperacao.\n\nIsso nao apaga as copias da empresa.\nO proximo backup automatico sera completo.\n\nDepois use Recuperar... e grave em outra pasta, por exemplo C:\\restaurado.",
                    "Gerar para recuperar", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK)
                    return;
                _montarLevel0 = null;
                RunJob("Gerar para recuperar", cfg =>
                {
                    _montarLevel0 = Engine.RunMontarTeste(cfg);
                }, true);
            };

            Log.LineWritten += line =>
            {
                try
                {
                    if (!IsHandleCreated || IsDisposed) return;
                    if (string.IsNullOrEmpty(line)) return;
                    if (line.IndexOf("FTP >", StringComparison.OrdinalIgnoreCase) >= 0) return;
                    if (line.IndexOf("FTP <", StringComparison.OrdinalIgnoreCase) >= 0) return;
                    if (line.IndexOf("FTP PWD", StringComparison.OrdinalIgnoreCase) >= 0) return;
                    BeginInvoke(new Action(() => AppendLog(line)));
                }
                catch { }
            };
        }

        GroupBox Card(Control parent, string title, int h)
        {
            var g = new GroupBox
            {
                Text = title,
                Font = _section,
                Size = new Size(518, h),
                BackColor = Color.White,
                Margin = new Padding(0, 0, 0, 8)
            };
            parent.Controls.Add(g);
            return g;
        }

        Label L(string text, int x, int y, int w)
        {
            return new Label { Text = text, Location = new Point(x, y), Size = new Size(w, 22), Font = _ui, TextAlign = ContentAlignment.MiddleLeft };
        }

        TextBox T(Control parent, int x, int y, int w, string value)
        {
            var c = new TextBox { Location = new Point(x, y), Size = new Size(w, 26), Text = value, Font = _ui };
            parent.Controls.Add(c);
            return c;
        }

        Button Btn(string text, int x, int y, int w, bool primary)
        {
            var b = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(w, 32),
                Font = _ui,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                BackColor = primary ? Accent : Color.White,
                ForeColor = primary ? Color.White : Color.FromArgb(30, 41, 59)
            };
            b.FlatAppearance.BorderSize = primary ? 0 : 1;
            b.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            return b;
        }

        void SetupTray()
        {
            _trayIcon = MakeIcon();
            Icon = _trayIcon;
            var menu = new ContextMenuStrip();
            menu.Items.Add("Abrir", null, (s, e) => ShowMe());
            menu.Items.Add("Copiar agora", null, (s, e) => { ShowMe(); RunJob("Copia", cfg => Engine.RunBackup(cfg, cfg.EnviarFtp), true); });
            menu.Items.Add("Copiar so neste PC", null, (s, e) => { ShowMe(); RunJob("Copia neste PC", cfg => Engine.RunBackup(cfg, false), true); });
            menu.Items.Add("Recuperar o banco...", null, (s, e) => { ShowMe(); OpenRestore(); });
            menu.Items.Add("Gerar para recuperar", null, (s, e) => { ShowMe(); if (_btnMontar != null) _btnMontar.PerformClick(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Abrir pasta das copias", null, (s, e) => OpenFolder());
            menu.Items.Add("Reiniciar", null, (s, e) => RestartClicked());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Sair", null, (s, e) => TryExitFromTray());
            _tray = new NotifyIcon
            {
                Icon = _trayIcon,
                Visible = true,
                ContextMenuStrip = menu,
                Text = Tip("Backup do sistema")
            };
            _tray.DoubleClick += (s, e) => ShowMe();
        }

        static Icon MakeIcon()
        {
            try
            {
                var exe = Application.ExecutablePath;
                if (!string.IsNullOrEmpty(exe) && File.Exists(exe))
                {
                    var ico = Icon.ExtractAssociatedIcon(exe);
                    if (ico != null) return ico;
                }
            }
            catch { }
            var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Navy);
                using (var brush = new SolidBrush(Color.White))
                    g.FillEllipse(brush, 2, 2, 12, 12);
            }
            return Icon.FromHandle(bmp.GetHicon());
        }

        void SetupWatchers()
        {
            _poll = new System.Windows.Forms.Timer { Interval = 400 };
            _poll.Tick += (s, e) =>
            {
                if (!_busy) return;
                var st = StatusFileEx.Read();
                if (st == null) return;
                _status.Text = st.Message;
                if (st.Percent >= 0 && st.Percent <= 100)
                {
                    _bar.Style = ProgressBarStyle.Continuous;
                    _bar.Value = Math.Max(_bar.Minimum, Math.Min(100, st.Percent));
                }
                else _bar.Style = ProgressBarStyle.Marquee;
            };

            _showWatch = new System.Windows.Forms.Timer { Interval = 400, Enabled = true };
            _showWatch.Tick += (s, e) =>
            {
                if (_showEvent != null && _showEvent.WaitOne(0)) ShowMe();
            };
        }

        void ShowMe()
        {
            ShowInTaskbar = true;
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
            BringToFront();
        }

        void OnClosing(object sender, FormClosingEventArgs e)
        {
            if (_exitForReal) return;
            if (e.CloseReason == CloseReason.WindowsShutDown) return;

            if (e.CloseReason == CloseReason.UserClosing || e.CloseReason == CloseReason.TaskManagerClosing)
            {
                e.Cancel = true;
                if (e.CloseReason != CloseReason.UserClosing) return;
                Hide();
                ShowInTaskbar = false;
                if (!_balloonShown)
                {
                    _balloonShown = true;
                    Balloon("Backup do sistema", "O backup continua ligado. Clique no icone ao lado do relogio para abrir de novo.", ToolTipIcon.Info);
                }
            }
        }

        void TryExitFromTray()
        {
            if (IsServiceInstalled() && !AskPassword("Sair", "Informe a senha para fechar o backup."))
                return;
            _exitForReal = true;
            Close();
        }

        bool IsServiceInstalled()
        {
            try
            {
                var c = Ini.Load(AppPaths.ConfigFile);
                return Engine.IsScheduled(c);
            }
            catch { return false; }
        }

        bool AskPassword(string title, string prompt)
        {
            using (var dlg = new PasswordForm(title, prompt))
            {
                var owner = Visible ? this : (IWin32Window)null;
                if (dlg.ShowDialog(owner) != DialogResult.OK)
                    return false;
                if (dlg.Password == (_ftpHold.SenhaOperacao ?? ""))
                    return true;
                MessageBox.Show(owner ?? this, "Senha incorreta.", title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
        }

        void RestartClicked()
        {
            try
            {
                var exe = Application.ExecutablePath;
                var args = (Visible && ShowInTaskbar) ? "" : "--tray";
                var start = new ProcessStartInfo();
                start.FileName = "cmd.exe";
                start.Arguments = "/c ping 127.0.0.1 -n 3 >nul & start \"\" \"" + exe + "\" " + args;
                start.CreateNoWindow = true;
                start.UseShellExecute = false;
                Process.Start(start);
                _exitForReal = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_tray != null) _tray.Visible = false;
                if (_tray != null) _tray.Dispose();
                if (_poll != null) _poll.Dispose();
                if (_showWatch != null) _showWatch.Dispose();
            }
            base.Dispose(disposing);
        }

        void OnDrop(object sender, DragEventArgs e)
        {
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null || files.Length == 0) return;
            var f = files[0];
            if (f.EndsWith(".fdb", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase))
            {
                _database.Text = f;
                RefreshHints();
                UiLog("Banco selecionado: " + f);
            }
        }

        void LoadToForm(AppConfig c)
        {
            _cnpj.Text = c.Cnpj;
            _nome.Text = c.Nome;
            _database.Text = c.Database;
            _local.Text = c.LocalFolder;
            _dbUser.Text = c.DbUser;
            _dbPass.Text = c.DbPassword;
            var fb = c.GbakPath;
            string found;
            if (string.IsNullOrWhiteSpace(fb) && Engine.TryFindNbackup("", out found))
                fb = Engine.FirebirdFolder(found);
            _nbackup.Text = fb ?? "";
            _diasFull.Value = Math.Max(1, Math.Min(30, c.DiasEntreFull));
            _manter.Value = Math.Max(1, Math.Min(30, c.ManterCadeias));
            DateTime t;
            if (!DateTime.TryParseExact(Engine.NormalizeHora(c.Hora), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out t))
                t = DateTime.Today.AddHours(23);
            _hora.Value = DateTime.Today.AddHours(t.Hour).AddMinutes(t.Minute);
            if (_destFtp != null) _destFtp.Checked = c.EnviarFtp;
            if (_destLocal != null) _destLocal.Checked = !c.EnviarFtp;
        }

        AppConfig Collect()
        {
            var c = Ini.Load(AppPaths.ConfigFile);
            c.ConfigPath = AppPaths.ConfigFile;
            c.Cnpj = _cnpj.Text.Trim();
            c.Nome = _nome.Text.Trim();
            c.Database = _database.Text.Trim();
            c.GbakPath = _nbackup.Text.Trim();
            c.LocalFolder = _local.Text.Trim();
            c.DbUser = string.IsNullOrWhiteSpace(_dbUser.Text) ? "SYSDBA" : _dbUser.Text.Trim();
            c.DbPassword = _dbPass.Text;
            c.DiasEntreFull = (int)_diasFull.Value;
            c.ManterCadeias = (int)_manter.Value;
            c.Hora = _hora.Value.ToString("HH:mm");
            c.EnviarFtp = _destFtp == null || _destFtp.Checked;
            c.FtpHost = _ftpHold.FtpHost;
            c.FtpPort = _ftpHold.FtpPort;
            c.FtpUser = _ftpHold.FtpUser;
            c.FtpPassword = _ftpHold.FtpPassword;
            c.FtpPastaRemota = _ftpHold.FtpPastaRemota;
            c.FtpTimeoutSegundos = _ftpHold.FtpTimeoutSegundos;
            c.FtpTentativas = _ftpHold.FtpTentativas;
            if (string.IsNullOrWhiteSpace(c.LocalFolder) || Ini.IsCloud(c.LocalFolder))
                throw new Exception("A pasta das copias nao pode ficar no OneDrive. Escolha uma pasta deste computador, por exemplo C:\\BackupFirebird.");
            return c;
        }

        void RefreshHints()
        {
            var ftpOn = _destFtp == null || _destFtp.Checked;
            var digits = Naming.Digits(_cnpj.Text);
            var dest = @"E:\Backup2\" + Naming.ClientFolder(_cnpj.Text, _nome.Text);
            if (!ftpOn)
            {
                _destino.Text = "Modo: somente neste computador. Sem envio ao servidor FTP.";
                _destino.ForeColor = Ok;
            }
            else if (digits.Length < 14)
            {
                _destino.Text = "Pasta no servidor: " + dest + Environment.NewLine + "Informe o CNPJ com 14 numeros.";
                _destino.ForeColor = Warn;
            }
            else
            {
                _destino.Text = "Pasta no servidor: " + dest;
                _destino.ForeColor = Ok;
            }

            var db = _database.Text.Trim();
            if (string.IsNullOrWhiteSpace(db))
            {
                _dbStatus.Text = "Escolha o arquivo do banco (geralmente GDI.FDB).";
                _dbStatus.ForeColor = Warn;
            }
            else if (File.Exists(db))
            {
                var fi = new FileInfo(db);
                _dbStatus.Text = "Banco encontrado: " + fi.Name + "  (" + Naming.Size(fi.Length) + ")";
                _dbStatus.ForeColor = Ok;
            }
            else
            {
                _dbStatus.Text = "Nao achei esse arquivo. Clique em Procurar...";
                _dbStatus.ForeColor = Danger;
            }

            string nbk;
            if (Engine.TryFindNbackup(_nbackup.Text.Trim(), out nbk))
            {
                _nbkStatus.Text = "Firebird encontrado. Pronto para copiar.";
                _nbkStatus.ForeColor = Ok;
            }
            else
            {
                _nbkStatus.Text = "Nao achei o Firebird. Clique em Procurar e abra a pasta Firebird_3_0.";
                _nbkStatus.ForeColor = Danger;
            }

            var local = _local.Text.Trim();
            if (Ini.IsCloud(local))
            {
                _localStatus.Text = "Essa pasta esta na nuvem. Escolha uma pasta deste computador, por exemplo C:\\BackupFirebird.";
                _localStatus.ForeColor = Danger;
            }
            else
            {
                _localStatus.Text = ftpOn
                    ? "As copias ficam neste computador e nao sao apagadas depois do envio."
                    : "As copias ficam so neste computador. Nao ha envio ao servidor.";
                _localStatus.ForeColor = Ok;
            }

            try
            {
                var cfg = Ini.Load(AppPaths.ConfigFile);
                if (Engine.IsScheduled(cfg))
                {
                    var modo = ftpOn ? "e envia ao servidor" : "somente neste PC";
                    _taskStatus.Text = "Servico instalado. Todo dia as " + Engine.NormalizeHora(cfg.Hora) + " (" + modo + ").";
                    _taskStatus.ForeColor = Ok;
                }
                else
                {
                    _taskStatus.Text = "Preencha os dados e clique em Instalar servico agora.";
                    _taskStatus.ForeColor = Warn;
                }
            }
            catch
            {
                _taskStatus.Text = "Preencha os dados e clique em Instalar servico agora.";
                _taskStatus.ForeColor = Warn;
            }

            if (_btnFtp != null && !_busy)
                _btnFtp.Enabled = ftpOn;
        }

        void RefreshCopies()
        {
            _copies.Items.Clear();
            AppConfig c;
            try { c = Ini.Load(AppPaths.ConfigFile); }
            catch { return; }
            if (!string.IsNullOrWhiteSpace(_local.Text)) c.LocalFolder = _local.Text.Trim();
            foreach (var item in Engine.ListCopies(c))
            {
                var row = new ListViewItem(item.Type);
                row.SubItems.Add(item.Time.ToString("dd/MM/yyyy HH:mm"));
                row.SubItems.Add(Path.GetFileName(item.Path));
                row.SubItems.Add(Naming.Size(item.Size));
                row.Tag = item.Path;
                if (item.Type == "Do dia") row.ForeColor = Mute;
                _copies.Items.Add(row);
            }
        }

        void ChooseFirebird()
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "nbackup (nbackup.exe)|nbackup.exe|Programas (*.exe)|*.exe";
                ofd.Title = "Selecione nbackup.exe (fica na pasta do Firebird)";
                ofd.CheckFileExists = true;
                ofd.FileName = "nbackup.exe";
                var cur = _nbackup.Text.Trim();
                string found;
                if (Directory.Exists(cur)) ofd.InitialDirectory = cur;
                else if (File.Exists(cur)) ofd.InitialDirectory = Path.GetDirectoryName(cur);
                else if (Engine.TryFindNbackup("", out found)) ofd.InitialDirectory = Engine.FirebirdFolder(found);
                else ofd.InitialDirectory = @"C:\Program Files\Firebird";
                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    _nbackup.Text = Path.GetDirectoryName(ofd.FileName);
                    RefreshHints();
                    UiLog("Firebird: " + ofd.FileName);
                }
            }
        }

        void OpenRestore()
        {
            OpenRestore("");
        }

        void OpenRestore(string preselect)
        {
            AppConfig c;
            try { c = Collect(); }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var pre = preselect ?? "";
            if (string.IsNullOrEmpty(pre) && _copies.SelectedItems.Count > 0)
                pre = _copies.SelectedItems[0].Tag as string ?? "";
            using (var dlg = new RestoreForm(c, pre))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                var dest = dlg.DestFdb;
                var choice = dlg.Choice;
                if (choice == null)
                {
                    MessageBox.Show(this, "Escolha uma copia na lista.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                RunJob("Recuperar", cfg => Engine.Restore(cfg, choice, dest), true);
            }
        }

        void ChooseDb()
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "Banco Firebird (*.fdb;*.gdb)|*.fdb;*.gdb|Todos (*.*)|*.*";
                ofd.Title = "Selecione o banco que deve ser copiado (GDI.FDB)";
                ofd.CheckFileExists = true;
                var cur = _database.Text.Trim();
                if (File.Exists(cur))
                {
                    ofd.InitialDirectory = Path.GetDirectoryName(cur);
                    ofd.FileName = Path.GetFileName(cur);
                }
                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    _database.Text = ofd.FileName;
                    RefreshHints();
                    UiLog("Banco selecionado: " + ofd.FileName);
                }
            }
        }

        void ChooseFolder()
        {
            using (var d = new FolderBrowserDialog())
            {
                d.Description = "Pasta neste computador para guardar as copias";
                var cur = _local.Text.Trim();
                if (Directory.Exists(cur)) d.SelectedPath = cur;
                if (d.ShowDialog(this) == DialogResult.OK)
                {
                    _local.Text = d.SelectedPath;
                    RefreshHints();
                }
            }
        }

        void OpenFolder()
        {
            try
            {
                var c = Collect();
                Engine.EnsureFolders(c);
                System.Diagnostics.Process.Start("explorer.exe", c.LocalFolder);
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        void OpenSelectedCopy()
        {
            if (_copies.SelectedItems.Count == 0) return;
            var path = _copies.SelectedItems[0].Tag as string;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + path + "\"");
        }

        void SaveClicked()
        {
            try
            {
                var c = Collect();
                Ini.Save(c);
                _ftpHold = c;
                Engine.EnsureFolders(c);
                UiLog("Dados salvos.");
                RefreshHints();
                MessageBox.Show(this, "Dados salvos.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void InstallClicked()
        {
            try
            {
                var c = Collect();
                Engine.Install(c);
                _ftpHold = c;
                RefreshHints();
                UiLog("Servico instalado. Todo dia as " + c.Hora + (c.EnviarFtp ? " (com envio ao servidor)." : " (somente neste PC)."));
                Balloon("Pronto", "Servico instalado. Todo dia as " + c.Hora + (c.EnviarFtp ? " o banco sera copiado e enviado." : " o banco sera copiado so neste PC."), ToolTipIcon.Info);
                MessageBox.Show(this,
                    c.EnviarFtp
                        ? "Servico instalado. Todo dia as " + c.Hora + " o Windows copia o banco e envia ao servidor."
                        : "Servico instalado. Todo dia as " + c.Hora + " o Windows copia o banco so neste computador (sem FTP).",
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void UninstallClicked()
        {
            if (!AskPassword("Remover servico", "Informe a senha para remover o servico."))
                return;
            if (MessageBox.Show(this,
                "Isso desliga o backup automatico neste PC.\nAs copias ja feitas nao sao apagadas.",
                "Remover servico", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;
            try
            {
                var c = Ini.Load(AppPaths.ConfigFile);
                Engine.Uninstall(c);
                RefreshHints();
                UiLog("Servico removido neste PC.");
                MessageBox.Show(this, "Servico removido. O backup automatico foi desligado neste PC.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void ClearCopiesClicked()
        {
            if (!AskPassword("Limpar copias", "Informe a senha para apagar as copias deste computador."))
                return;
            if (MessageBox.Show(this,
                "Isso apaga as copias neste computador.\nO servidor nao e afetado.\n\nTem certeza?",
                "Limpar copias", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;
            try
            {
                var c = Ini.Load(AppPaths.ConfigFile);
                if (!string.IsNullOrWhiteSpace(_local.Text)) c.LocalFolder = _local.Text.Trim();
                Engine.ClearLocalBackups(c);
                RefreshCopies();
                RefreshHints();
                UiLog("Copias locais apagadas.");
                MessageBox.Show(this, "As copias deste computador foram apagadas.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void OpenFtp()
        {
            using (var dlg = new FtpForm(_ftpHold))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    _ftpHold = dlg.Result;
            }
        }

        void RunJob(string title, Action<AppConfig> work, bool saveFirst)
        {
            if (_busy)
            {
                MessageBox.Show(this, "Ja existe uma copia em andamento. Aguarde terminar.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            AppConfig c;
            try
            {
                c = Collect();
                if (saveFirst)
                {
                    Ini.Save(c);
                    _ftpHold = c;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _busy = true;
            SetBusy(true);
            _bar.Style = ProgressBarStyle.Marquee;
            _status.Text = title + "...";
            _tray.Text = Tip(title + "...");
            UiLog(title + " iniciado.");

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    work(c);
                    BeginInvoke(new Action(() => FinishJob(title + " concluido.", true, false)));
                }
                catch (LocalOkFtpFailedException ex)
                {
                    Log.Write(ex.Message, "WARN");
                    BeginInvoke(new Action(() => FinishJob(ex.Message, false, true)));
                }
                catch (Exception ex)
                {
                    Log.Write(ex.Message, "ERROR");
                    StatusFile.Set("erro", ex.Message, -1);
                    BeginInvoke(new Action(() => FinishJob(ex.Message, false, false)));
                }
            });
        }

        void FinishJob(string message, bool ok, bool warning)
        {
            _busy = false;
            SetBusy(false);
            _bar.Style = ProgressBarStyle.Continuous;
            _bar.Value = (ok || warning) ? 100 : 0;
            _status.Text = warning ? "Copia neste PC ok. Envio ao servidor ainda nao concluiu." : message.Replace("\n", " ");
            _tray.Text = Tip(ok ? "Backup do sistema" : (warning ? "Copia neste PC ok" : "Falha na copia"));
            UiLog(message.Replace("\r\n", " | "));
            RefreshCopies();
            RefreshHints();
            Balloon(ok ? "Concluido" : (warning ? "Copia neste PC ok" : "Nao concluiu"), message, ok ? ToolTipIcon.Info : ToolTipIcon.Warning);
            if (ok && message.IndexOf("Gerar para recuperar", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                RefreshCopies();
                MessageBox.Show(this,
                    "Pronto. Foram geradas 1 copia completa e 2 copias do dia.\n\nAgora escolha essas copias e grave o banco em outra pasta, por exemplo C:\\restaurado.\nNao use o arquivo que o GDI esta usando.",
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                if (!string.IsNullOrEmpty(_montarLevel0))
                    OpenRestore(_montarLevel0);
            }
            else if (ok && message.IndexOf("Recuperar concluido", StringComparison.OrdinalIgnoreCase) >= 0)
                MessageBox.Show(this, "Pronto. O banco foi gravado no destino escolhido.\n\nDeixe o GDI fechado se for substituir o arquivo original.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            else if (!ok) MessageBox.Show(this, message, Text, MessageBoxButtons.OK, warning ? MessageBoxIcon.Warning : MessageBoxIcon.Warning);
        }

        void SetBusy(bool busy)
        {
            _btnSave.Enabled = !busy;
            _btnInstall.Enabled = !busy;
            if (_btnRestart != null) _btnRestart.Enabled = !busy;
            if (_btnUninstall != null) _btnUninstall.Enabled = !busy;
            if (_btnClear != null) _btnClear.Enabled = !busy;
            _btnNow.Enabled = !busy;
            _btnLocal.Enabled = !busy;
            _btnFtp.Enabled = !busy && (_destFtp == null || _destFtp.Checked);
            _btnTest.Enabled = !busy;
            if (_btnMontar != null) _btnMontar.Enabled = !busy;
            _btnFtpDlg.Enabled = !busy;
            if (_btnRestore != null) _btnRestore.Enabled = !busy;
            UseWaitCursor = busy;
            if (busy) _poll.Start(); else _poll.Stop();
        }

        void AppendLog(string line)
        {
            if (_log.TextLength > 80000) _log.Clear();
            _log.AppendText(line + Environment.NewLine);
        }

        void UiLog(string msg)
        {
            AppendLog(string.Format("[{0:HH:mm:ss}] {1}", DateTime.Now, msg));
        }

        void Balloon(string title, string text, ToolTipIcon icon)
        {
            try
            {
                _tray.BalloonTipTitle = Tip(title);
                var body = text ?? "";
                if (body.Length > 250) body = body.Substring(0, 247) + "...";
                _tray.BalloonTipText = body;
                _tray.BalloonTipIcon = icon;
                _tray.ShowBalloonTip(5000);
            }
            catch { }
        }

        static string Tip(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "Backup do sistema";
            return text.Length > 63 ? text.Substring(0, 60) + "..." : text;
        }
    }

    sealed class PasswordForm : Form
    {
        readonly TextBox _pass;
        public string Password { get { return _pass.Text; } }

        public PasswordForm(string title, string prompt)
        {
            Text = string.IsNullOrWhiteSpace(title) ? "Confirmar" : title;
            Font = new Font("Segoe UI", 9.5f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            TopMost = true;
            ClientSize = new Size(360, 140);
            BackColor = Color.White;

            Controls.Add(new Label
            {
                Text = string.IsNullOrWhiteSpace(prompt) ? "Informe a senha." : prompt,
                Location = new Point(16, 12),
                Size = new Size(328, 36)
            });
            _pass = new TextBox
            {
                Location = new Point(16, 52),
                Size = new Size(328, 26),
                UseSystemPasswordChar = true
            };
            Controls.Add(_pass);
            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(170, 96), Size = new Size(82, 30) };
            var cancel = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Location = new Point(262, 96), Size = new Size(82, 30) };
            Controls.Add(ok);
            Controls.Add(cancel);
            AcceptButton = ok;
            CancelButton = cancel;
            Shown += (s, e) => _pass.Focus();
        }
    }

    sealed class FtpForm : Form
    {
        readonly TextBox _host, _user, _pass, _pasta;
        readonly NumericUpDown _port, _timeout, _tries;
        public AppConfig Result { get; private set; }

        public FtpForm(AppConfig c)
        {
            Result = c;
            Text = "Servidor das copias";
            Font = new Font("Segoe UI", 9.5f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(460, 300);
            BackColor = Color.White;

            Controls.Add(new Label { Text = "Endereco", Location = new Point(20, 24), AutoSize = true });
            _host = new TextBox { Location = new Point(120, 20), Size = new Size(220, 26), Text = c.FtpHost };
            Controls.Add(_host);
            Controls.Add(new Label { Text = "Porta", Location = new Point(350, 24), AutoSize = true });
            _port = new NumericUpDown { Location = new Point(390, 20), Size = new Size(50, 26), Minimum = 1, Maximum = 65535, Value = Math.Max(1, c.FtpPort) };
            Controls.Add(_port);

            Controls.Add(new Label { Text = "Usuario", Location = new Point(20, 62), AutoSize = true });
            _user = new TextBox { Location = new Point(120, 58), Size = new Size(320, 26), Text = c.FtpUser };
            Controls.Add(_user);
            Controls.Add(new Label { Text = "Senha", Location = new Point(20, 100), AutoSize = true });
            _pass = new TextBox { Location = new Point(120, 96), Size = new Size(320, 26), Text = c.FtpPassword, UseSystemPasswordChar = true };
            Controls.Add(_pass);
            Controls.Add(new Label { Text = "Pasta no servidor", Location = new Point(20, 138), AutoSize = true });
            _pasta = new TextBox { Location = new Point(120, 134), Size = new Size(320, 26), Text = c.FtpPastaRemota };
            Controls.Add(_pasta);

            Controls.Add(new Label { Text = "Tempo limite (s)", Location = new Point(20, 176), AutoSize = true });
            _timeout = new NumericUpDown { Location = new Point(120, 172), Size = new Size(80, 26), Minimum = 30, Maximum = 86400, Value = Math.Max(30, c.FtpTimeoutSegundos) };
            Controls.Add(_timeout);
            Controls.Add(new Label { Text = "Tentativas", Location = new Point(230, 176), AutoSize = true });
            _tries = new NumericUpDown { Location = new Point(310, 172), Size = new Size(60, 26), Minimum = 1, Maximum = 10, Value = Math.Max(1, c.FtpTentativas) };
            Controls.Add(_tries);

            var hint = new Label
            {
                Text = "Em geral estes dados ja vem prontos. Clique em Testar conexao para conferir.",
                Location = new Point(20, 214),
                Size = new Size(420, 32),
                ForeColor = Color.FromArgb(71, 85, 105)
            };
            Controls.Add(hint);

            var ok = new Button { Text = "Salvar", DialogResult = DialogResult.OK, Location = new Point(250, 254), Size = new Size(90, 32) };
            var cancel = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Location = new Point(350, 254), Size = new Size(90, 32) };
            var test = new Button { Text = "Testar conexao", Location = new Point(20, 254), Size = new Size(120, 32) };
            Controls.Add(ok);
            Controls.Add(cancel);
            Controls.Add(test);
            AcceptButton = ok;
            CancelButton = cancel;
            ok.Click += (s, e) => Apply();
            test.Click += (s, e) =>
            {
                Apply();
                try
                {
                    Engine.TestFtp(Result);
                    MessageBox.Show(this, "Conectou. A pasta da empresa no servidor esta pronta.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };
        }

        void Apply()
        {
            Result.FtpHost = _host.Text.Trim();
            Result.FtpPort = (int)_port.Value;
            Result.FtpUser = _user.Text.Trim();
            Result.FtpPassword = _pass.Text;
            Result.FtpPastaRemota = string.IsNullOrWhiteSpace(_pasta.Text) ? "E:/Backup2" : _pasta.Text.Trim();
            Result.FtpTimeoutSegundos = (int)_timeout.Value;
            Result.FtpTentativas = (int)_tries.Value;
        }
    }

    sealed class RestoreForm : Form
    {
        readonly AppConfig _c;
        readonly ListView _completes;
        readonly ListView _incs;
        readonly TextBox _dest;
        readonly Label _detail;
        readonly List<string> _extra = new List<string>();
        bool _filling;
        public RestoreChoice Choice { get; private set; }
        public string DestFdb { get { return _dest.Text.Trim(); } }

        public RestoreForm(AppConfig c, string preselect)
        {
            _c = c;
            Text = "Recuperar o banco";
            Font = new Font("Segoe UI", 9.5f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(760, 640);
            BackColor = Color.White;

            var destName = "GDI.FDB";
            if (!string.IsNullOrWhiteSpace(c.Database)) destName = Path.GetFileName(c.Database);
            var destDefault = Path.Combine(@"C:\restaurado", destName);

            Controls.Add(new Label
            {
                Text = "Escolha a copia completa (mais nova em cima). As copias do dia aparecem abaixo. Se trouxe a pasta de pen drive ou outro HD, clique em Procurar pasta.",
                Location = new Point(16, 12),
                Size = new Size(728, 36),
                ForeColor = Color.FromArgb(71, 85, 105)
            });

            Controls.Add(new Label { Text = "1. Copias completas", Location = new Point(16, 52), AutoSize = true });
            _completes = new ListView
            {
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                HideSelection = false,
                MultiSelect = false,
                Location = new Point(16, 74),
                Size = new Size(728, 168),
                Font = new Font("Segoe UI", 8.5f)
            };
            _completes.Columns.Add("Onde", 90);
            _completes.Columns.Add("Data", 130);
            _completes.Columns.Add("Ciclo", 210);
            _completes.Columns.Add("Arquivo", 200);
            _completes.Columns.Add("Copias do dia", 90);
            _completes.SelectedIndexChanged += (s, e) => ShowIncrementals();
            Controls.Add(_completes);

            Controls.Add(new Label { Text = "2. Copias do dia. Desmarque as posteriores se quiser voltar a uma data anterior.", Location = new Point(16, 250), Size = new Size(728, 18), ForeColor = Color.FromArgb(71, 85, 105) });
            _incs = new ListView
            {
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                HideSelection = false,
                CheckBoxes = true,
                Location = new Point(16, 272),
                Size = new Size(728, 140),
                Font = new Font("Segoe UI", 8.5f)
            };
            _incs.Columns.Add("Data", 140);
            _incs.Columns.Add("Arquivo", 430);
            _incs.Columns.Add("Tamanho", 120);
            _incs.ItemChecked += (s, e) => { if (!_filling) SyncChoice(); };
            Controls.Add(_incs);

            _detail = new Label
            {
                Location = new Point(16, 418),
                Size = new Size(728, 36),
                ForeColor = Color.FromArgb(21, 128, 61)
            };
            Controls.Add(_detail);

            var bFolder = new Button { Text = "Procurar pasta...", Location = new Point(16, 456), Size = new Size(130, 30) };
            var bFile = new Button { Text = "Procurar arquivo...", Location = new Point(152, 456), Size = new Size(140, 30) };
            var bRef = new Button { Text = "Atualizar", Location = new Point(298, 456), Size = new Size(90, 30) };
            bFolder.Click += (s, e) => AddFolder();
            bFile.Click += (s, e) => AddFile();
            bRef.Click += (s, e) => LoadLocal(preselect);
            Controls.Add(bFolder);
            Controls.Add(bFile);
            Controls.Add(bRef);

            Controls.Add(new Label { Text = "Onde gravar o banco recuperado", Location = new Point(16, 494), AutoSize = true });
            _dest = new TextBox { Location = new Point(16, 514), Size = new Size(614, 26), Text = destDefault };
            Controls.Add(_dest);
            var bd = new Button { Text = "Procurar...", Location = new Point(638, 512), Size = new Size(106, 30) };
            bd.Click += (s, e) =>
            {
                using (var sfd = new SaveFileDialog())
                {
                    sfd.Filter = "Banco Firebird (*.fdb)|*.fdb";
                    sfd.Title = "Onde gravar o banco recuperado";
                    sfd.FileName = destName;
                    sfd.InitialDirectory = @"C:\restaurado";
                    if (sfd.ShowDialog(this) == DialogResult.OK) _dest.Text = sfd.FileName;
                }
            };
            Controls.Add(bd);

            Controls.Add(new Label
            {
                Text = "Nao use o arquivo que o GDI esta aberto. Grave em outra pasta, por exemplo C:\\restaurado.",
                Location = new Point(16, 546),
                Size = new Size(500, 20),
                ForeColor = Color.FromArgb(180, 83, 9)
            });

            var ok = new Button { Text = "Recuperar", DialogResult = DialogResult.OK, Location = new Point(544, 592), Size = new Size(100, 32) };
            var cancel = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Location = new Point(650, 592), Size = new Size(90, 32) };
            Controls.Add(ok);
            Controls.Add(cancel);
            AcceptButton = ok;
            CancelButton = cancel;
            ok.Click += (s, e) =>
            {
                SyncChoice();
                if (Choice == null || string.IsNullOrEmpty(Choice.Level0))
                {
                    MessageBox.Show(this, "Escolha uma copia completa na lista de cima ou clique em Procurar pasta.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    DialogResult = DialogResult.None;
                    return;
                }
                if (string.IsNullOrWhiteSpace(DestFdb))
                {
                    MessageBox.Show(this, "Informe onde gravar o banco recuperado.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    DialogResult = DialogResult.None;
                }
            };

            LoadLocal(preselect);
        }

        void LoadLocal(string preselect)
        {
            _completes.Items.Clear();
            _incs.Items.Clear();
            foreach (var ch in Engine.ListLocalRestoreChoices(_c, null))
                AddRow(ch);
            foreach (var extra in _extra)
                foreach (var ch in Engine.ListLocalRestoreChoices(_c, extra))
                    AddRow(ch);
            if (!string.IsNullOrWhiteSpace(preselect) && File.Exists(preselect))
            {
                string l0, l1;
                Engine.SuggestRestoreFiles(preselect, out l0, out l1);
                SelectMatching(l0);
            }
            if (_completes.Items.Count == 0)
                _detail.Text = "Nenhuma copia neste computador. Copie a pasta do servidor para um pen drive ou outro HD e clique em Procurar pasta.";
            else if (_completes.SelectedItems.Count == 0)
                _completes.Items[0].Selected = true;
        }

        void AddFolder()
        {
            using (var d = new FolderBrowserDialog())
            {
                d.Description = "Pasta com as copias (pen drive, HD ou pasta copiada do servidor)";
                if (d.ShowDialog(this) != DialogResult.OK) return;
                _extra.Add(d.SelectedPath);
                foreach (var ch in Engine.ListLocalRestoreChoices(_c, d.SelectedPath))
                    AddRow(ch);
                if (_completes.Items.Count == 0)
                    MessageBox.Show(this, "Nao achei uma copia completa nessa pasta.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                else if (_completes.SelectedItems.Count == 0)
                    _completes.Items[0].Selected = true;
            }
        }

        void AddFile()
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "Backup Firebird (*.nbk)|*.nbk|Todos (*.*)|*.*";
                ofd.Title = "Selecione uma copia completa ou uma copia do dia";
                if (ofd.ShowDialog(this) != DialogResult.OK) return;
                var ch = Engine.ChoiceFromPath(ofd.FileName);
                if (ch == null || string.IsNullOrEmpty(ch.Level0))
                {
                    MessageBox.Show(this, "Nao achei a copia completa na mesma pasta deste arquivo.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                AddRow(ch);
                SelectMatching(ch.Level0);
            }
        }

        void AddRow(RestoreChoice ch)
        {
            foreach (ListViewItem existing in _completes.Items)
            {
                var old = existing.Tag as RestoreChoice;
                if (old != null && string.Equals(old.Level0, ch.Level0, StringComparison.OrdinalIgnoreCase))
                    return;
            }
            var nInc = ch.Incrementals == null ? 0 : ch.Incrementals.Count;
            var row = new ListViewItem(ch.Origin ?? "Neste PC");
            row.SubItems.Add(ch.Time.ToString("dd/MM/yyyy HH:mm"));
            row.SubItems.Add(ch.Cycle ?? "");
            row.SubItems.Add(Path.GetFileName(ch.Level0) ?? "");
            row.SubItems.Add(nInc == 0 ? "nenhum" : nInc.ToString());
            row.Tag = ch;
            _completes.Items.Add(row);
        }

        void SelectMatching(string l0)
        {
            if (string.IsNullOrEmpty(l0)) return;
            foreach (ListViewItem row in _completes.Items)
            {
                var ch = row.Tag as RestoreChoice;
                if (ch != null && string.Equals(ch.Level0, l0, StringComparison.OrdinalIgnoreCase))
                {
                    row.Selected = true;
                    row.EnsureVisible();
                    return;
                }
            }
        }

        void ShowIncrementals()
        {
            _filling = true;
            try
            {
                _incs.Items.Clear();
                if (_completes.SelectedItems.Count == 0) { SyncChoice(); return; }
                var src = _completes.SelectedItems[0].Tag as RestoreChoice;
                if (src == null || src.Incrementals == null) { SyncChoice(); return; }
                foreach (var inc in src.Incrementals)
                {
                    DateTime t;
                    long size = 0;
                    try { t = File.GetLastWriteTime(inc); size = new FileInfo(inc).Length; }
                    catch { t = DateTime.MinValue; }
                    var row = new ListViewItem(t == DateTime.MinValue ? "" : t.ToString("dd/MM/yyyy HH:mm:ss"));
                    row.SubItems.Add(Path.GetFileName(inc));
                    row.SubItems.Add(Naming.Size(size));
                    row.Tag = inc;
                    row.Checked = true;
                    _incs.Items.Add(row);
                }
            }
            finally
            {
                _filling = false;
                SyncChoice();
            }
        }

        List<string> CheckedIncrementals()
        {
            var list = new List<string>();
            foreach (ListViewItem row in _incs.Items)
            {
                if (!row.Checked) continue;
                var p = row.Tag as string;
                if (!string.IsNullOrEmpty(p)) list.Add(p);
            }
            return list;
        }

        void SyncChoice()
        {
            Choice = null;
            if (_completes.SelectedItems.Count == 0)
            {
                if (_completes.Items.Count > 0) _detail.Text = "Escolha uma copia completa na lista de cima.";
                return;
            }
            var src = _completes.SelectedItems[0].Tag as RestoreChoice;
            if (src == null || string.IsNullOrEmpty(src.Level0)) return;
            Choice = new RestoreChoice
            {
                Origin = src.Origin,
                Cycle = src.Cycle,
                Level0 = src.Level0,
                Time = src.Time,
                Incrementals = CheckedIncrementals()
            };
            var files = Engine.BuildRestoreFiles(Choice.Level0, Choice.Incrementals);
            if (files.Count == 0)
                _detail.Text = "";
            else if (files.Count == 1)
                _detail.Text = "Vai recuperar so a copia completa: " + Path.GetFileName(files[0]);
            else
                _detail.Text = "Vai juntar nesta ordem: " + Engine.RestoreOrderText(files);
        }
    }

    static class StatusFileEx
    {
        public sealed class Info
        {
            public string Message;
            public int Percent;
        }

        public static Info Read()
        {
            try
            {
                var p = Path.Combine(AppPaths.LogDir, "status.json");
                if (!File.Exists(p)) return null;
                var j = File.ReadAllText(p);
                var msg = Regex.Match(j, "\"message\"\\s*:\\s*\"([^\"]*)\"");
                var pct = Regex.Match(j, "\"percent\"\\s*:\\s*(-?\\d+)");
                var i = new Info { Message = msg.Success ? msg.Groups[1].Value : "", Percent = 0 };
                if (pct.Success) int.TryParse(pct.Groups[1].Value, out i.Percent);
                return i;
            }
            catch { return null; }
        }
    }
}

