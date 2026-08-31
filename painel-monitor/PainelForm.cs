using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace BackupFdbPainel
{
    sealed class PainelForm : Form
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

        PainelConfig _cfg;
        ListView _clients, _files;
        Label _status, _resumo;
        TextBox _filtro, _log;
        ProgressBar _bar;
        Button _btnRefresh, _btnDownFile, _btnDownClient, _btnOpen, _btnSrv, _btnPasta, _btnMontar;
        bool _busy;

        public PainelForm(PainelConfig cfg)
        {
            _cfg = cfg ?? PainelConfig.Load();
            Text = "Painel de backups";
            Font = _ui;
            BackColor = Bg;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1080, 720);
            Size = new Size(1180, 800);
            DoubleBuffered = true;
            BuildUi();
            Shown += (s, e) => BeginRefresh();
        }

        void BuildUi()
        {
            var header = new Panel { Dock = DockStyle.Top, Height = 72, BackColor = Navy };
            header.Controls.Add(new Label { Text = "Painel de backups", Font = _title, ForeColor = Color.White, Location = new Point(22, 8), AutoSize = true });
            header.Controls.Add(new Label { Text = "Acompanhe as copias de cada CNPJ no servidor. Baixe a pasta do cliente quando precisar.", Font = _ui, ForeColor = Color.FromArgb(191, 219, 254), Location = new Point(24, 42), AutoSize = true });
            Controls.Add(header);

            var footer = new Panel { Dock = DockStyle.Bottom, Height = 132 };
            _status = new Label { Text = "Pronto.", Location = new Point(16, 8), Size = new Size(800, 20), ForeColor = Mute, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            _bar = new ProgressBar { Location = new Point(16, 30), Size = new Size(1140, 10), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            _log = new TextBox
            {
                Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = Color.White,
                Location = new Point(16, 46), Size = new Size(1140, 76),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                Font = _small
            };
            footer.Controls.Add(_status);
            footer.Controls.Add(_bar);
            footer.Controls.Add(_log);
            Controls.Add(footer);

            var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(12, 10, 12, 6) };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42f));
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58f));
            Controls.Add(content);
            content.BringToFront();

            var left = new GroupBox { Text = "Empresas (CNPJ)", Font = _section, Dock = DockStyle.Fill, BackColor = Color.White };
            var right = new GroupBox { Text = "Arquivos do cliente selecionado", Font = _section, Dock = DockStyle.Fill, BackColor = Color.White };
            content.Controls.Add(left, 0, 0);
            content.Controls.Add(right, 1, 0);

            _filtro = new TextBox { Location = new Point(12, 26), Size = new Size(280, 26), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, Font = _ui };
            var hint = new Label { Text = "Buscar CNPJ ou nome", Location = new Point(300, 28), AutoSize = true, ForeColor = Mute, Font = _small };
            _filtro.TextChanged += (s, e) => ApplyFilter();
            left.Controls.Add(_filtro);
            left.Controls.Add(hint);

            _clients = new ListView
            {
                View = View.Details, FullRowSelect = true, GridLines = true, HideSelection = false,
                Location = new Point(12, 58), Size = new Size(430, 360),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Font = _small
            };
            _clients.Columns.Add("CNPJ", 110);
            _clients.Columns.Add("Empresa", 120);
            _clients.Columns.Add("Ultima copia", 110);
            _clients.Columns.Add("Situacao", 80);
            _clients.SelectedIndexChanged += (s, e) => ShowFiles();
            left.Controls.Add(_clients);

            _resumo = new Label { Location = new Point(12, 424), Size = new Size(430, 36), Font = _small, ForeColor = Mute, Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
            left.Controls.Add(_resumo);

            _btnRefresh = Btn("Atualizar", 12, 466, 100, true);
            _btnSrv = Btn("Conta...", 118, 466, 100, false);
            _btnPasta = Btn("Pasta...", 224, 466, 100, false);
            _btnRefresh.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            _btnSrv.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            _btnPasta.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            left.Controls.Add(_btnRefresh);
            left.Controls.Add(_btnSrv);
            left.Controls.Add(_btnPasta);
            _btnRefresh.Click += (s, e) => BeginRefresh();
            _btnSrv.Click += (s, e) => OpenSrv();
            _btnPasta.Click += (s, e) => EscolherPasta();
            left.Resize += (s, e) =>
            {
                _btnRefresh.Top = left.ClientSize.Height - 40;
                _btnSrv.Top = _btnRefresh.Top;
                _btnPasta.Top = _btnRefresh.Top;
                _resumo.Top = _btnRefresh.Top - 40;
                _clients.Height = Math.Max(80, _resumo.Top - 64);
            };

            _files = new ListView
            {
                View = View.Details, FullRowSelect = true, GridLines = true, HideSelection = false,
                Location = new Point(12, 26), Size = new Size(600, 400),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Font = _small
            };
            _files.Columns.Add("Tipo", 80);
            _files.Columns.Add("Quando", 120);
            _files.Columns.Add("Arquivo", 220);
            _files.Columns.Add("Tamanho", 80);
            _files.Columns.Add("Pasta", 140);
            _files.DoubleClick += (s, e) => DownloadFile();
            right.Controls.Add(_files);

            _btnDownFile = Btn("Baixar arquivo", 12, 436, 118, false);
            _btnDownClient = Btn("Baixar cliente", 136, 436, 118, true);
            _btnMontar = Btn("Montar .fdb", 260, 436, 110, false);
            _btnOpen = Btn("Abrir pasta", 376, 436, 110, false);
            _btnDownFile.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            _btnDownClient.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            _btnMontar.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            _btnOpen.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            right.Controls.Add(_btnDownFile);
            right.Controls.Add(_btnDownClient);
            right.Controls.Add(_btnMontar);
            right.Controls.Add(_btnOpen);
            _btnDownFile.Click += (s, e) => DownloadFile();
            _btnDownClient.Click += (s, e) => DownloadClient();
            _btnMontar.Click += (s, e) => MontarLocal();
            _btnOpen.Click += (s, e) => OpenLocal();
            right.Resize += (s, e) =>
            {
                _btnDownFile.Top = right.ClientSize.Height - 40;
                _btnDownClient.Top = _btnDownFile.Top;
                _btnMontar.Top = _btnDownFile.Top;
                _btnOpen.Top = _btnDownFile.Top;
                _files.Height = Math.Max(80, _btnDownFile.Top - 32);
            };
        }

        Button Btn(string text, int x, int y, int w, bool primary)
        {
            var b = new Button
            {
                Text = text, Location = new Point(x, y), Size = new Size(w, 32), Font = _ui,
                FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
                BackColor = primary ? Accent : Color.White,
                ForeColor = primary ? Color.White : Color.FromArgb(30, 41, 59)
            };
            b.FlatAppearance.BorderSize = primary ? 0 : 1;
            b.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            return b;
        }

        ClientRow SelectedClient()
        {
            if (_clients.SelectedItems.Count == 0) return null;
            return _clients.SelectedItems[0].Tag as ClientRow;
        }

        RemoteItem SelectedFile()
        {
            if (_files.SelectedItems.Count == 0) return null;
            return _files.SelectedItems[0].Tag as RemoteItem;
        }

        void ApplyFilter()
        {
            var q = (_filtro.Text ?? "").Trim();
            foreach (ListViewItem row in _clients.Items)
            {
                var c = row.Tag as ClientRow;
                if (c == null) { row.ForeColor = Color.Black; continue; }
                var ok = q.Length == 0
                    || (c.Cnpj != null && c.Cnpj.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                    || (c.Nome != null && c.Nome.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
                row.ForeColor = ok ? Color.Black : Color.Silver;
            }
        }

        void ShowFiles()
        {
            _files.Items.Clear();
            var c = SelectedClient();
            if (c == null)
            {
                _resumo.Text = "Selecione um CNPJ na lista.";
                return;
            }
            foreach (var f in c.Files)
            {
                var tipo = "Outro";
                if ((f.Name ?? "").StartsWith("Completo_", StringComparison.OrdinalIgnoreCase)) tipo = "Completa";
                else if ((f.Name ?? "").StartsWith("Incremental_", StringComparison.OrdinalIgnoreCase)) tipo = "Do dia";
                var row = new ListViewItem(tipo);
                row.SubItems.Add(f.Time == DateTime.MinValue ? "" : f.Time.ToString("dd/MM/yyyy HH:mm"));
                row.SubItems.Add(f.Name);
                row.SubItems.Add(FtpPainel.Size(f.Size));
                row.SubItems.Add(f.RelDir);
                row.Tag = f;
                if (tipo == "Do dia") row.ForeColor = Mute;
                _files.Items.Add(row);
            }
            _resumo.Text = c.Cnpj + "  " + c.Nome + Environment.NewLine +
                c.Completas + " completa(s), " + c.DoDia + " do dia, " + FtpPainel.Size(c.Bytes) + "  |  " + c.Situacao;
            _resumo.ForeColor = c.Situacao == "Em dia" ? Ok : (c.Situacao == "Atrasado" ? Warn : Danger);
        }

        void OpenSrv()
        {
            using (var dlg = new ContaPainelForm(_cfg))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                _cfg = dlg.Result;
                _cfg.Save();
                BeginRefresh();
            }
        }

        void EscolherPasta()
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Pasta neste PC onde os downloads do painel serao gravados.";
                dlg.ShowNewFolderButton = true;
                if (Directory.Exists(_cfg.PastaLocal)) dlg.SelectedPath = _cfg.PastaLocal;
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                _cfg.PastaLocal = dlg.SelectedPath;
                _cfg.Save();
                _status.Text = "Downloads em " + _cfg.PastaLocal;
                UiLog(_status.Text);
            }
        }

        void OpenLocal()
        {
            try
            {
                if (!Directory.Exists(_cfg.PastaLocal)) Directory.CreateDirectory(_cfg.PastaLocal);
                Process.Start("explorer.exe", _cfg.PastaLocal);
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        void SetBusy(bool busy)
        {
            _busy = busy;
            _btnRefresh.Enabled = !busy;
            _btnDownFile.Enabled = !busy;
            _btnDownClient.Enabled = !busy;
            if (_btnMontar != null) _btnMontar.Enabled = !busy;
            _btnSrv.Enabled = !busy;
            if (_btnPasta != null) _btnPasta.Enabled = !busy;
            UseWaitCursor = busy;
            _bar.Style = busy ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
            if (!busy) _bar.Value = 0;
        }

        void UiLog(string msg)
        {
            if (_log.TextLength > 80000) _log.Clear();
            _log.AppendText(string.Format("[{0:HH:mm:ss}] {1}{2}", DateTime.Now, msg, Environment.NewLine));
        }

        void BeginRefresh()
        {
            if (_busy) return;
            if (!_cfg.HasConta)
            {
                OpenSrv();
                return;
            }
            SetBusy(true);
            _status.Text = "Lendo o servidor...";
            UiLog("Atualizando lista de CNPJs...");
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    using (var ftp = new FtpPainel(_cfg))
                    {
                        ftp.LogLine += m => BeginInvoke(new Action(() => UiLog(m)));
                        ftp.Connect();
                        var tree = ftp.Tree();
                        var rows = FtpPainel.GroupClients(tree);
                        BeginInvoke(new Action(() => FillClients(rows)));
                    }
                }
                catch (Exception ex)
                {
                    BeginInvoke(new Action(() =>
                    {
                        SetBusy(false);
                        _status.Text = ex.Message;
                        UiLog(ex.Message);
                        MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }));
                }
            });
        }

        void FillClients(System.Collections.Generic.List<ClientRow> rows)
        {
            _clients.Items.Clear();
            _files.Items.Clear();
            foreach (var c in rows)
            {
                var row = new ListViewItem(c.Cnpj);
                row.SubItems.Add(c.Nome);
                row.SubItems.Add(c.Last == DateTime.MinValue ? "-" : c.Last.ToString("dd/MM/yyyy HH:mm"));
                row.SubItems.Add(c.Situacao);
                row.Tag = c;
                if (c.Situacao == "Em dia") row.ForeColor = Ok;
                else if (c.Situacao == "Atrasado") row.ForeColor = Warn;
                else row.ForeColor = Danger;
                _clients.Items.Add(row);
            }
            ApplyFilter();
            SetBusy(false);
            _status.Text = rows.Count + " empresa(s) no servidor.";
            UiLog(_status.Text);
            if (_clients.Items.Count > 0) _clients.Items[0].Selected = true;
        }

        void DownloadFile()
        {
            var f = SelectedFile();
            if (f == null)
            {
                MessageBox.Show(this, "Selecione um arquivo na lista da direita.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            RunDownload(new[] { f }, Path.Combine(_cfg.PastaLocal, SelectedClient().Folder), false, SelectedClient());
        }

        void DownloadClient()
        {
            var c = SelectedClient();
            if (c == null)
            {
                MessageBox.Show(this, "Selecione um CNPJ na lista da esquerda.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (c.Files.Count == 0)
            {
                MessageBox.Show(this, "Este CNPJ ainda nao tem arquivos no servidor.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var cadeia = FtpPainel.ResolverCadeia(c.Files, SelectedFile());
            if (cadeia == null || cadeia.Completo == null)
            {
                MessageBox.Show(this, "Este CNPJ ainda nao tem um backup completo.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (var dlg = new CadeiaForm(c, cadeia))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                if (dlg.Arquivos.Count == 0) return;
                RunDownload(dlg.Arquivos.ToArray(), Path.Combine(_cfg.PastaLocal, c.Folder), dlg.MontarDepois, c);
            }
        }

        void MontarLocal()
        {
            var c = SelectedClient();
            if (c == null)
            {
                MessageBox.Show(this, "Selecione um CNPJ na lista da esquerda.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var pasta = Path.Combine(_cfg.PastaLocal, c.Folder);
            var sel = SelectedFile();
            var cadeiaPasta = sel == null ? "" : (sel.RelDir ?? "");
            var arquivos = MontarBackup.CadeiaLocal(pasta, cadeiaPasta);
            if (arquivos.Count == 0)
            {
                MessageBox.Show(this, "Ainda nao ha copias deste cliente neste PC.\nBaixe o cliente primeiro.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            PedirEMontar(arquivos, pasta, c);
        }

        void PedirEMontar(List<string> arquivos, string pastaLocal, ClientRow cliente)
        {
            using (var dlg = new MontarForm(arquivos, pastaLocal, cliente))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                var dest = dlg.Destino;
                var user = dlg.DbUser;
                var pass = dlg.DbPassword;
                SetBusy(true);
                _status.Text = "Montando o .fdb...";
                UiLog("Juntando completo + incrementais com nbackup...");
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        MontarBackup.Restaurar(arquivos, dest, user, pass);
                        BeginInvoke(new Action(() =>
                        {
                            SetBusy(false);
                            _status.Text = "Banco montado: " + dest;
                            UiLog(_status.Text);
                            MessageBox.Show(this, "Banco montado:\n" + dest, Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                            try
                            {
                                var dir = Path.GetDirectoryName(dest);
                                if (!string.IsNullOrEmpty(dir)) Process.Start("explorer.exe", "/select,\"" + dest + "\"");
                            }
                            catch { }
                        }));
                    }
                    catch (Exception ex)
                    {
                        BeginInvoke(new Action(() =>
                        {
                            SetBusy(false);
                            _status.Text = ex.Message;
                            UiLog(ex.Message);
                            MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }));
                    }
                });
            }
        }

        void RunDownload(RemoteItem[] files, string destRoot, bool montarDepois, ClientRow cliente)
        {
            if (_busy) return;
            SetBusy(true);
            _bar.Style = ProgressBarStyle.Continuous;
            _status.Text = "Baixando...";
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    var locais = new List<string>();
                    using (var ftp = new FtpPainel(_cfg))
                    {
                        ftp.LogLine += m => BeginInvoke(new Action(() => UiLog(m)));
                        ftp.Progress += pct => BeginInvoke(new Action(() =>
                        {
                            _bar.Style = ProgressBarStyle.Continuous;
                            _bar.Value = Math.Max(0, Math.Min(100, pct));
                        }));
                        ftp.Connect();
                        for (var i = 0; i < files.Length; i++)
                        {
                            var f = files[i];
                            var local = MontarBackup.LocalPath(destRoot, f);
                            var n = i + 1;
                            BeginInvoke(new Action(() => _status.Text = "Baixando " + n + " de " + files.Length + ": " + f.Name));
                            ftp.Download(f.VirtualPath, local);
                            locais.Add(local);
                        }
                    }
                    BeginInvoke(new Action(() =>
                    {
                        SetBusy(false);
                        _bar.Value = 100;
                        _status.Text = "Download concluido em " + destRoot;
                        UiLog(_status.Text);
                        if (montarDepois)
                            PedirEMontar(locais, destRoot, cliente);
                        else
                            MessageBox.Show(this, "Arquivos gravados em:\n" + destRoot, Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }));
                }
                catch (Exception ex)
                {
                    BeginInvoke(new Action(() =>
                    {
                        SetBusy(false);
                        _status.Text = ex.Message;
                        UiLog(ex.Message);
                        MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }));
                }
            });
        }
    }

    sealed class CadeiaForm : Form
    {
        readonly TreeView _tree;
        bool _travando;
        public List<RemoteItem> Arquivos { get; private set; }
        public bool MontarDepois { get; private set; }

        public CadeiaForm(ClientRow cliente, CadeiaBackup cadeia)
        {
            Arquivos = new List<RemoteItem>();
            Text = "Baixar cliente";
            Font = new Font("Segoe UI", 9.5f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(560, 420);
            BackColor = Color.White;

            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 64,
                BackColor = Color.FromArgb(15, 76, 129)
            };
            header.Controls.Add(new Label
            {
                Text = "Cadeia deste completo",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(16, 8),
                AutoSize = true
            });
            var sub = (cliente.Cnpj ?? "") + "  " + (cliente.Nome ?? "");
            if (!string.IsNullOrEmpty(cadeia.Pasta)) sub += "  |  " + cadeia.Pasta;
            header.Controls.Add(new Label
            {
                Text = sub,
                ForeColor = Color.FromArgb(191, 219, 254),
                Location = new Point(18, 36),
                AutoSize = true
            });
            Controls.Add(header);

            var hint = new Label
            {
                Text = "O completo e a base. Embaixo, na ordem, os incrementais daquele dia. Desmarque o que nao quiser baixar.",
                Location = new Point(16, 76),
                Size = new Size(528, 36),
                ForeColor = Color.FromArgb(71, 85, 105)
            };
            Controls.Add(hint);

            _tree = new TreeView
            {
                Location = new Point(16, 116),
                Size = new Size(528, 236),
                CheckBoxes = true,
                HideSelection = false,
                FullRowSelect = true
            };
            Controls.Add(_tree);

            var raiz = NewNode(cadeia.Completo, "Completo");
            raiz.Checked = true;
            foreach (var inc in cadeia.Incrementais)
            {
                var n = NewNode(inc, "Incremental");
                n.Checked = true;
                raiz.Nodes.Add(n);
            }
            _tree.Nodes.Add(raiz);
            raiz.Expand();
            _tree.AfterCheck += (s, e) =>
            {
                if (_travando) return;
                if (e.Node == raiz && !e.Node.Checked)
                {
                    _travando = true;
                    e.Node.Checked = true;
                    _travando = false;
                }
            };

            var baixar = new Button { Text = "Baixar", Location = new Point(194, 368), Size = new Size(90, 32) };
            var montar = new Button { Text = "Baixar e montar", Location = new Point(290, 368), Size = new Size(128, 32) };
            var cancel = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Location = new Point(424, 368), Size = new Size(90, 32) };
            Controls.Add(baixar);
            Controls.Add(montar);
            Controls.Add(cancel);
            AcceptButton = baixar;
            CancelButton = cancel;
            baixar.Click += (s, e) => Confirmar(false);
            montar.Click += (s, e) => Confirmar(true);
        }

        void Confirmar(bool montar)
        {
            Arquivos = Coletar();
            if (Arquivos.Count == 0)
            {
                MessageBox.Show(this, "Marque pelo menos o completo.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            MontarDepois = montar;
            DialogResult = DialogResult.OK;
            Close();
        }

        static TreeNode NewNode(RemoteItem f, string tipo)
        {
            var quando = f.Time == DateTime.MinValue ? "" : f.Time.ToString("dd/MM/yyyy HH:mm");
            var text = tipo + "  |  " + f.Name + "  |  " + FtpPainel.Size(f.Size);
            if (quando.Length > 0) text += "  |  " + quando;
            return new TreeNode(text) { Tag = f };
        }

        List<RemoteItem> Coletar()
        {
            var list = new List<RemoteItem>();
            foreach (TreeNode raiz in _tree.Nodes)
            {
                var f = raiz.Tag as RemoteItem;
                if (raiz.Checked && f != null) list.Add(f);
                foreach (TreeNode filho in raiz.Nodes)
                {
                    var inc = filho.Tag as RemoteItem;
                    if (filho.Checked && inc != null) list.Add(inc);
                }
            }
            return list;
        }
    }

    sealed class MontarForm : Form
    {
        readonly TextBox _dest, _user, _pass;
        public string Destino { get; private set; }
        public string DbUser { get; private set; }
        public string DbPassword { get; private set; }

        public MontarForm(List<string> arquivos, string pastaLocal, ClientRow cliente)
        {
            Text = "Montar banco .fdb";
            Font = new Font("Segoe UI", 9.5f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(560, 360);
            BackColor = Color.White;

            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 58,
                BackColor = Color.FromArgb(15, 76, 129)
            };
            header.Controls.Add(new Label
            {
                Text = "Juntar completo + incrementais",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(16, 8),
                AutoSize = true
            });
            header.Controls.Add(new Label
            {
                Text = "O nbackup gera um .fdb novo. O arquivo de destino nao pode existir.",
                ForeColor = Color.FromArgb(191, 219, 254),
                Location = new Point(18, 32),
                AutoSize = true
            });
            Controls.Add(header);

            var ordem = new StringBuilder();
            for (var i = 0; i < arquivos.Count; i++)
            {
                if (i > 0) ordem.AppendLine();
                ordem.Append((i + 1)).Append(") ").Append(Path.GetFileName(arquivos[i]));
            }
            Controls.Add(new Label { Text = "Ordem", Location = new Point(16, 72), AutoSize = true });
            Controls.Add(new TextBox
            {
                Location = new Point(16, 92),
                Size = new Size(528, 72),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Text = ordem.ToString()
            });

            var padrao = Path.Combine(pastaLocal ?? "", (cliente != null && !string.IsNullOrEmpty(cliente.Cnpj) ? cliente.Cnpj : "banco") + "_restaurado.fdb");
            if (File.Exists(padrao))
                padrao = Path.Combine(Path.GetDirectoryName(padrao) ?? "", Path.GetFileNameWithoutExtension(padrao) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".fdb");

            Controls.Add(new Label { Text = "Destino .fdb", Location = new Point(16, 178), AutoSize = true });
            _dest = new TextBox { Location = new Point(110, 174), Size = new Size(350, 26), Text = padrao };
            Controls.Add(_dest);
            var browse = new Button { Text = "...", Location = new Point(468, 173), Size = new Size(40, 28) };
            Controls.Add(browse);
            browse.Click += (s, e) =>
            {
                using (var d = new SaveFileDialog())
                {
                    d.Title = "Onde gravar o banco montado";
                    d.Filter = "Firebird (*.fdb)|*.fdb";
                    d.FileName = Path.GetFileName(_dest.Text);
                    var dir = Path.GetDirectoryName(_dest.Text);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) d.InitialDirectory = dir;
                    d.OverwritePrompt = false;
                    if (d.ShowDialog(this) == DialogResult.OK) _dest.Text = d.FileName;
                }
            };

            Controls.Add(new Label { Text = "Usuario", Location = new Point(16, 218), AutoSize = true });
            _user = new TextBox { Location = new Point(110, 214), Size = new Size(140, 26), Text = "SYSDBA" };
            Controls.Add(_user);
            Controls.Add(new Label { Text = "Senha", Location = new Point(270, 218), AutoSize = true });
            _pass = new TextBox { Location = new Point(318, 214), Size = new Size(226, 26), Text = "masterkey", UseSystemPasswordChar = true };
            Controls.Add(_pass);

            Controls.Add(new Label
            {
                Text = "Precisa do Firebird 3.0 neste PC (nbackup.exe). Usuario e senha do banco original.",
                Location = new Point(16, 250),
                Size = new Size(528, 32),
                ForeColor = Color.FromArgb(71, 85, 105)
            });

            var ok = new Button { Text = "Montar", Location = new Point(354, 308), Size = new Size(90, 32) };
            var cancel = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Location = new Point(454, 308), Size = new Size(90, 32) };
            Controls.Add(ok);
            Controls.Add(cancel);
            AcceptButton = ok;
            CancelButton = cancel;
            ok.Click += (s, e) =>
            {
                var dest = (_dest.Text ?? "").Trim();
                if (dest.Length == 0)
                {
                    MessageBox.Show(this, "Informe o arquivo .fdb de destino.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                Destino = dest;
                DbUser = _user.Text.Trim();
                DbPassword = _pass.Text;
                DialogResult = DialogResult.OK;
                Close();
            };
        }
    }

    sealed class ContaPainelForm : Form
    {
        readonly TextBox _email, _pass;
        public PainelConfig Result { get; private set; }

        public ContaPainelForm(PainelConfig c)
        {
            Result = c;
            Text = "Entrar no painel";
            Font = new Font("Segoe UI", 9.5f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(440, 210);
            BackColor = Color.White;

            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 58,
                BackColor = Color.FromArgb(15, 76, 129)
            };
            header.Controls.Add(new Label
            {
                Text = "Conta do representante",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(16, 8),
                AutoSize = true
            });
            header.Controls.Add(new Label
            {
                Text = "Somente e-mail e senha cadastrados no servidor.",
                ForeColor = Color.FromArgb(191, 219, 254),
                Location = new Point(18, 32),
                AutoSize = true
            });
            Controls.Add(header);

            Controls.Add(new Label { Text = "E-mail", Location = new Point(20, 78), AutoSize = true });
            _email = new TextBox { Location = new Point(90, 74), Size = new Size(330, 26), Text = c.Email };
            Controls.Add(_email);
            Controls.Add(new Label { Text = "Senha", Location = new Point(20, 116), AutoSize = true });
            _pass = new TextBox { Location = new Point(90, 112), Size = new Size(330, 26), Text = c.Password, UseSystemPasswordChar = true };
            Controls.Add(_pass);

            var ok = new Button { Text = "Entrar", Location = new Point(230, 160), Size = new Size(90, 32) };
            var cancel = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Location = new Point(330, 160), Size = new Size(90, 32) };
            Controls.Add(ok);
            Controls.Add(cancel);
            AcceptButton = ok;
            CancelButton = cancel;
            ok.Click += (s, e) =>
            {
                var email = _email.Text.Trim();
                if (email.IndexOf('@') < 1)
                {
                    MessageBox.Show(this, "Informe o e-mail cadastrado no servidor.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (string.IsNullOrEmpty(_pass.Text))
                {
                    MessageBox.Show(this, "Informe a senha.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                Result.Email = email;
                Result.Password = _pass.Text;
                DialogResult = DialogResult.OK;
                Close();
            };
        }
    }
}
