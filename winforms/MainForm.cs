using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace TodoTool
{
    /// <summary>主窗体：统计、工具栏、清单、底部快速添加、托盘常驻。</summary>
    public class MainForm : Form
    {
        // ---- 配色（与原 HTML 版一致）----
        static readonly Color CBlue = Color.FromArgb(0x2B, 0x57, 0x9A);
        static readonly Color CGreen = Color.FromArgb(0x2E, 0x7D, 0x32);
        static readonly Color CRed = Color.FromArgb(0xC6, 0x28, 0x28);
        static readonly Color COrange = Color.FromArgb(0xB2, 0x6A, 0x00);
        static readonly Color CSoon = Color.FromArgb(0x1B, 0x7A, 0x4D);
        static readonly Color CText = Color.FromArgb(0x33, 0x33, 0x33);
        static readonly Color CGray = Color.FromArgb(0x99, 0x99, 0x99);
        static readonly Color CMeta = Color.FromArgb(0x88, 0x88, 0x88);
        static readonly Color CBg = Color.FromArgb(0xF0, 0xF2, 0xF5);
        // zh-CN 语言包缺失（英文/精简系统）时降级为 InvariantCulture，避免启动即崩
        static readonly CultureInfo Zh = CreateZhCulture();
        static readonly Color CBorder = Color.FromArgb(0xE3, 0xE8, 0xEF);

        static CultureInfo CreateZhCulture()
        {
            try { return CultureInfo.GetCultureInfo("zh-CN"); }
            catch { return CultureInfo.InvariantCulture; }
        }

        // ---- 字体缓存 ----
        static readonly Font FontBase = new Font("微软雅黑", 9F);
        static readonly Font FontBold = new Font("微软雅黑", 9F, FontStyle.Bold);

        // ---- 数据与状态 ----
        readonly TodoStore store = new TodoStore();
        readonly Timer saveTimer = new Timer { Interval = 500 };
        bool _reloading;
        bool _reallyExit;
        CalendarPopup addPopup;

        // ---- 控件 ----
        BufferedListView list;
        ColumnHeader colNo, colText, colOwner, colDeadline, colNote;
        Label lblMeta, lblEmpty, lblTotal, lblDone, lblLate;
        Button btnSort, btnNew, btnEdit, btnDel, btnReset, btnExport, btnClear;
        TextBox txtAdd, txtAddOwner, txtAddDate;
        Label lblAddPreview;
        ToolStripStatusLabel lblSaved;
        Panel cardTotal, cardDone, cardLate;
        NotifyIcon tray;
        ContextMenuStrip menuRow;
        readonly Font fontStrike = new Font("微软雅黑", 9F, FontStyle.Strikeout);

        const string AutostartName = "TodoTool";
        static readonly string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string AddHint = "截止日期支持：10-20 / 2026-10-20 / 明天 / 下周三，或点 📅 直接选日期";

        public MainForm()
        {
            Text = "待办清单";
            Font = FontBase;
            BackColor = CBg;
            ClientSize = new Size(940, 640);
            MinimumSize = new Size(780, 540);
            StartPosition = FormStartPosition.CenterScreen;
            try { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            BuildUi();
            BuildTray();

            saveTimer.Tick += delegate { SaveNow(); };

            store.Load();
            if (store.Items.Count == 0 && !File.Exists(store.DataFile))
            {
                store.Items.Add(new TodoItem
                {
                    Id = 1,
                    Text = "示例：整理接口文档，同步确认字段",
                    Owner = "张三",
                    DeadlineText = "后天"
                });
            }
            Render();
            UpdateAddPreview();
        }

        // ============================================================
        //  UI 构建
        // ============================================================
        private void BuildUi()
        {
            // ---- 顶部：标题 + 日期行 ----
            Panel pnlTop = new Panel { Dock = DockStyle.Top, Height = 72, BackColor = CBg, Padding = new Padding(16, 8, 16, 0) };
            Label lblTitle = new Label
            {
                Dock = DockStyle.Top,
                Height = 36,
                Text = "待办清单",
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.FromArgb(0x1A, 0x1A, 0x1A),
                Font = new Font("微软雅黑", 15F, FontStyle.Bold)
            };
            lblMeta = new Label
            {
                Dock = DockStyle.Top,
                Height = 20,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = CMeta,
                Font = new Font("微软雅黑", 8.5F)
            };
            pnlTop.Controls.Add(lblMeta);
            pnlTop.Controls.Add(lblTitle);

            // ---- 统计卡片 ----
            Panel pnlStats = new Panel { Dock = DockStyle.Top, Height = 68, BackColor = CBg };
            cardTotal = MakeStatCard(out lblTotal, "全部待办", CBlue);
            cardDone = MakeStatCard(out lblDone, "已完成", CGreen);
            cardLate = MakeStatCard(out lblLate, "已逾期", CRed);
            pnlStats.Controls.Add(cardTotal);
            pnlStats.Controls.Add(cardDone);
            pnlStats.Controls.Add(cardLate);
            pnlStats.Resize += delegate { LayoutStatCards(pnlStats); };
            LayoutStatCards(pnlStats);

            // ---- 工具栏 ----
            Panel pnlToolbar = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = CBg };
            FlowLayoutPanel flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(16, 4, 16, 0)
            };
            btnNew = MakeButton("＋ 新建", CBlue, Color.White, false);
            btnEdit = MakeButton("编辑", Color.White, Color.FromArgb(0x66, 0x66, 0x66), true);
            btnDel = MakeButton("删除", Color.White, CRed, true);
            btnSort = MakeButton("按截止日期排序", Color.White, Color.FromArgb(0x66, 0x66, 0x66), true);
            btnReset = MakeButton("重置勾选", Color.White, Color.FromArgb(0x66, 0x66, 0x66), true);
            btnExport = MakeButton("导出数据", Color.White, Color.FromArgb(0x66, 0x66, 0x66), true);
            btnClear = MakeButton("清空重建", Color.White, CRed, true);
            btnNew.Click += delegate { EditItemFormLoop(null); };
            btnEdit.Click += delegate { EditSelectedOrNew(); };
            btnDel.Click += delegate { DeleteSelected(); };
            btnSort.Click += delegate { ToggleSort(); };
            btnReset.Click += delegate { ResetChecks(); };
            btnExport.Click += delegate { ExportData(); };
            btnClear.Click += delegate { ClearAll(); };
            flow.Controls.Add(btnNew);
            flow.Controls.Add(btnEdit);
            flow.Controls.Add(btnDel);
            flow.Controls.Add(MakeGap(8));
            flow.Controls.Add(btnSort);
            flow.Controls.Add(btnReset);
            flow.Controls.Add(btnExport);
            flow.Controls.Add(MakeGap(8));
            flow.Controls.Add(btnClear);
            pnlToolbar.Controls.Add(flow);

            // ---- 清单区 ----
            Panel pnlListHost = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16, 2, 16, 6),
                BackColor = CBg
            };
            Panel cardList = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(1, 0, 1, 1)
            };
            list = new BufferedListView
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                BorderStyle = BorderStyle.None,
                View = View.Details,
                FullRowSelect = true,
                CheckBoxes = true,
                HideSelection = false,
                MultiSelect = false,
                HeaderStyle = ColumnHeaderStyle.Nonclickable
            };
            colNo = new ColumnHeader { Text = "#", Width = 42, TextAlign = HorizontalAlignment.Right };
            colText = new ColumnHeader { Text = "待办内容", Width = 360 };
            colOwner = new ColumnHeader { Text = "负责人", Width = 92 };
            colDeadline = new ColumnHeader { Text = "截止日期", Width = 190 };
            colNote = new ColumnHeader { Text = "备注", Width = 160 };
            list.Columns.AddRange(new ColumnHeader[] { colNo, colText, colOwner, colDeadline, colNote });
            list.Font = FontBase;
            list.ItemChecked += delegate (object s, ItemCheckedEventArgs e)
            {
                if (_reloading) return;
                TodoItem it = e.Item.Tag as TodoItem;
                if (it == null || it.Done == e.Item.Checked) return;
                it.Done = e.Item.Checked;
                Render();
                ScheduleSave();
            };
            list.MouseDoubleClick += delegate (object s, MouseEventArgs e)
            {
                TodoItem it = HitItem(e.Location);
                if (it != null) EditSelectedOrNew(it);
            };
            list.KeyDown += delegate (object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.F2) { e.SuppressKeyPress = true; EditSelectedOrNew(); }
                else if (e.KeyCode == Keys.Delete) { e.SuppressKeyPress = true; DeleteSelected(); }
                else if (e.KeyCode == Keys.Space && list.SelectedItems.Count > 0) { e.SuppressKeyPress = true; ToggleCheck(SelectedItem()); }
            };
            list.MouseUp += delegate (object s, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Right) return;
                TodoItem it = HitItem(e.Location);
                if (it == null) return;
                foreach (ListViewItem lvi in list.Items) if (ReferenceEquals(lvi.Tag, it)) { lvi.Selected = true; break; }
                menuRow.Show(list, e.Location);
            };
            list.Resize += delegate { FitColumns(); };
            lblEmpty = new Label
            {
                Dock = DockStyle.None,
                Text = "暂无待办项，在下方输入框添加一条试试 ✍",
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(0xAA, 0xAA, 0xAA),
                Visible = false
            };
            cardList.Controls.Add(list);
            // 空态提示不参与 Dock 布局，手动铺满并覆盖在列表上层
            cardList.Controls.Add(lblEmpty);
            cardList.Resize += delegate
            {
                lblEmpty.SetBounds(0, 0, cardList.ClientSize.Width, cardList.ClientSize.Height);
            };
            lblEmpty.SetBounds(0, 0, cardList.ClientSize.Width, cardList.ClientSize.Height);
            pnlListHost.Controls.Add(cardList);

            // ---- 底部快速添加行 ----
            Panel pnlAddRow = new Panel { Dock = DockStyle.Bottom, Height = 78, BackColor = CBg, Padding = new Padding(16, 8, 16, 0) };
            TableLayoutPanel tbl = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 30,
                ColumnCount = 5,
                RowCount = 1,
                BackColor = Color.Transparent,
                Margin = new Padding(0)
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 32));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
            txtAdd = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 3, 6, 3) };
            txtAddOwner = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 3, 6, 3) };
            txtAddDate = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 3, 6, 3) };
            Button btnAddPick = new Button
            {
                Text = "📅",
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0xF1, 0xF3, 0xF5),
                Margin = new Padding(0, 3, 6, 3)
            };
            btnAddPick.FlatAppearance.BorderSize = 0;
            btnAddPick.Click += delegate { ToggleAddPopup(btnAddPick); };
            Button btnAdd = new Button
            {
                Text = "添加",
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                BackColor = CGreen,
                ForeColor = Color.White,
                Margin = new Padding(0, 3, 0, 3)
            };
            btnAdd.FlatAppearance.BorderSize = 0;
            btnAdd.Click += delegate { AddFromQuickRow(); };
            txtAdd.KeyDown += delegate (object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; AddFromQuickRow(); } };
            txtAddOwner.KeyDown += delegate (object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; AddFromQuickRow(); } };
            txtAddDate.KeyDown += delegate (object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; AddFromQuickRow(); } };
            txtAddDate.TextChanged += delegate { UpdateAddPreview(); };
            tbl.Controls.Add(txtAdd, 0, 0);
            tbl.Controls.Add(txtAddOwner, 1, 0);
            tbl.Controls.Add(txtAddDate, 2, 0);
            tbl.Controls.Add(btnAddPick, 3, 0);
            tbl.Controls.Add(btnAdd, 4, 0);
            lblAddPreview = new Label
            {
                Dock = DockStyle.Fill,
                Text = AddHint,
                ForeColor = CMeta,
                Font = new Font("微软雅黑", 8.25F),
                Padding = new Padding(1, 6, 0, 0)
            };
            pnlAddRow.Controls.Add(lblAddPreview);
            pnlAddRow.Controls.Add(tbl);

            // ---- 状态栏 ----
            StatusStrip status = new StatusStrip { BackColor = Color.White, SizingGrip = false };
            ToolStripStatusLabel lblFilePos = new ToolStripStatusLabel
            {
                Spring = true,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = CMeta,
                Text = "数据文件：" + store.DataFile
            };
            lblSaved = new ToolStripStatusLabel { Text = "", ForeColor = CGreen };
            status.Items.Add(lblFilePos);
            status.Items.Add(lblSaved);

            // ---- 行右键菜单 ----
            menuRow = new ContextMenuStrip();
            ToolStripMenuItem miEdit = new ToolStripMenuItem("编辑");
            ToolStripMenuItem miToggle = new ToolStripMenuItem("勾选完成/取消完成");
            ToolStripMenuItem miCopy = new ToolStripMenuItem("复制内容");
            ToolStripMenuItem miDel = new ToolStripMenuItem("删除");
            miEdit.Click += delegate { EditSelectedOrNew(); };
            miToggle.Click += delegate { ToggleCheck(SelectedItem()); };
            miCopy.Click += delegate
            {
                TodoItem it = SelectedItem();
                if (it != null && it.Text.Length > 0) Clipboard.SetText(it.Text);
            };
            miDel.Click += delegate { DeleteSelected(); };
            menuRow.Items.Add(miEdit);
            menuRow.Items.Add(miToggle);
            menuRow.Items.Add(miCopy);
            menuRow.Items.Add(new ToolStripSeparator());
            menuRow.Items.Add(miDel);

            // Dock 布局：处理顺序 = add 的逆序
            Controls.Add(pnlListHost);
            Controls.Add(pnlToolbar);
            Controls.Add(pnlStats);
            Controls.Add(pnlTop);
            Controls.Add(pnlAddRow);
            Controls.Add(status);
            FitColumns();
        }

        private void BuildTray()
        {
            tray = new NotifyIcon
            {
                Icon = Icon != null ? Icon : SystemIcons.Application,
                Text = "待办清单",
                Visible = true
            };
            ContextMenuStrip trayMenu = new ContextMenuStrip();
            ToolStripMenuItem miShow = new ToolStripMenuItem("显示主窗口");
            ToolStripMenuItem miAuto = new ToolStripMenuItem("开机自启动") { CheckOnClick = true, Checked = GetAutoStart() };
            ToolStripMenuItem miExit = new ToolStripMenuItem("退出");
            miShow.Click += delegate { ShowMain(); };
            miAuto.Click += delegate { SetAutoStart(miAuto.Checked); };
            miExit.Click += delegate { _reallyExit = true; Close(); };
            trayMenu.Items.Add(miShow);
            trayMenu.Items.Add(miAuto);
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add(miExit);
            tray.ContextMenuStrip = trayMenu;
            tray.DoubleClick += delegate { ShowMain(); };

            Resize += delegate
            {
                if (WindowState == FormWindowState.Minimized) Hide();
            };
            FormClosing += MainForm_FormClosing;
            FormClosed += delegate
            {
                tray.Visible = false;
                tray.Dispose();
                saveTimer.Dispose();
            };
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!_reallyExit && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            SaveNow();
        }

        private void ShowMain()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        // ============================================================
        //  渲染
        // ============================================================
        private void Render()
        {
            _reloading = true;
            list.BeginUpdate();
            list.Items.Clear();

            List<TodoItem> rows = new List<TodoItem>(store.Items);
            if (store.SortByDeadline)
            {
                rows.Sort(CompareByDeadline);
            }

            int order = 0;
            foreach (TodoItem it in rows)
            {
                order++;
                ListViewItem lvi = new ListViewItem(order.ToString(CultureInfo.InvariantCulture));
                lvi.UseItemStyleForSubItems = false;
                lvi.Tag = it;
                lvi.Checked = it.Done;

                string iso = DeadlineParser.ResolveISO(it);
                DeadlineInfo dl = iso != null ? DeadlineParser.Info(iso) : null;

                ListViewItem.ListViewSubItem cText = lvi.SubItems.Add(it.Text);
                ListViewItem.ListViewSubItem cOwner = lvi.SubItems.Add(it.Owner);

                string dlDisplay = "";
                Color dlColor = Color.FromArgb(0x55, 0x55, 0x55);
                Font dlFont = null;
                if (dl != null)
                {
                    dlDisplay = dl.Full;
                    if (dl.Cls == "overdue") { dlColor = CRed; dlFont = FontBold; }
                    else if (dl.Cls == "today") { dlColor = COrange; dlFont = FontBold; }
                    else if (dl.Cls == "soon") { dlColor = CSoon; }
                }
                else if (it.DeadlineText.Length > 0)
                {
                    dlDisplay = it.DeadlineText;
                    dlColor = CGray;
                }
                ListViewItem.ListViewSubItem cDeadline = lvi.SubItems.Add(dlDisplay);
                ListViewItem.ListViewSubItem cNote = lvi.SubItems.Add(it.Note);

                Color rowBase = it.Done ? CGray : CText;
                lvi.ForeColor = CGray; // 序号列固定浅灰
                cText.ForeColor = rowBase;
                cText.Font = it.Done ? fontStrike : FontBase;
                cOwner.ForeColor = it.Done ? CGray : CBlue;
                cDeadline.ForeColor = it.Done ? CGray : dlColor;
                if (!it.Done && dlFont != null) cDeadline.Font = dlFont;
                cNote.ForeColor = it.Done ? CGray : NoteColor(it.Note);

                list.Items.Add(lvi);
            }
            list.EndUpdate();
            _reloading = false;

            UpdateStatsMeta();
            FitColumns();

            bool on = store.SortByDeadline;
            btnSort.Text = on ? "✓ 按截止日期排序" : "按截止日期排序";
            btnSort.BackColor = on ? CBlue : Color.White;
            btnSort.ForeColor = on ? Color.White : Color.FromArgb(0x66, 0x66, 0x66);
        }

        private int CompareByDeadline(TodoItem a, TodoItem b)
        {
            string av = DeadlineParser.ResolveISO(a) ?? "9999-12-31";
            string bv = DeadlineParser.ResolveISO(b) ?? "9999-12-31";
            int c = string.CompareOrdinal(av, bv);
            if (c != 0) return c;
            // 与原版一致：同截止日期时按原始顺序稳定排列
            return store.Items.IndexOf(a).CompareTo(store.Items.IndexOf(b));
        }

        private static Color NoteColor(string note)
        {
            if (note == null || note.Length == 0) return CMeta;
            if (note.IndexOf('✓') >= 0) return CGreen;
            if (note.IndexOf('×') >= 0) return CRed;
            return CMeta;
        }

        private void UpdateStatsMeta()
        {
            int total = store.Items.Count, done = 0, late = 0, soon = 0;
            foreach (TodoItem it in store.Items)
            {
                if (it.Done) { done++; continue; }
                string iso = DeadlineParser.ResolveISO(it);
                if (iso == null) continue;
                DeadlineInfo dl = DeadlineParser.Info(iso);
                if (dl == null) continue;
                if (dl.Diff < 0) late++;
                if (dl.Diff >= 0 && dl.Diff <= 3) soon++;
            }
            lblTotal.Text = total.ToString(CultureInfo.InvariantCulture);
            lblDone.Text = done.ToString(CultureInfo.InvariantCulture);
            lblLate.Text = late.ToString(CultureInfo.InvariantCulture);
            lblMeta.Text = "今天是 " + DeadlineParser.Today0().ToString("yyyy年M月d日 dddd", Zh) +
                (soon > 0 ? " · 未来 3 天内到期 " + soon + " 项" : " · 近 3 天无到期项");
            lblEmpty.Visible = total == 0;
        }

        private void FitColumns()
        {
            if (list == null) return;
            int fixedW = colNo.Width + colOwner.Width + colDeadline.Width + colNote.Width
                + SystemInformation.VerticalScrollBarWidth;
            colText.Width = Math.Max(240, list.ClientSize.Width - fixedW);
        }

        // ============================================================
        //  交互动作
        // ============================================================
        private void AddFromQuickRow()
        {
            string text = txtAdd.Text.Trim();
            if (text.Length == 0)
            {
                lblAddPreview.Text = "⚠ 请先输入待办内容";
                lblAddPreview.ForeColor = CRed;
                txtAdd.Focus();
                return;
            }
            TodoItem it = new TodoItem
            {
                Id = store.NextId(),
                Text = text,
                Owner = txtAddOwner.Text.Trim()
            };
            DeadlineParser.ApplyDeadline(it, txtAddDate.Text);
            store.Items.Add(it);
            txtAdd.Text = "";
            txtAddOwner.Text = "";
            txtAddDate.Text = "";
            UpdateAddPreview();
            Render();
            ScheduleSave();
            txtAdd.Focus();
        }

        private void UpdateAddPreview()
        {
            PreviewDeadlineText(txtAddDate.Text, lblAddPreview, AddHint);
        }

        private void ToggleAddPopup(Control anchor)
        {
            if (addPopup != null && !addPopup.IsDisposed) { addPopup.Close(); addPopup = null; return; }
            string current = txtAddDate.Text.Trim();
            string iso = DeadlineParser.IsIso(current) ? current : DeadlineParser.Parse(current);
            addPopup = CalendarPopup.ShowBelow(anchor, iso, delegate (string picked)
            {
                txtAddDate.Text = picked ?? "";
                txtAddDate.Focus();
            });
        }

        /// <summary>供底部快速行与编辑弹窗共用的截止日期实时预览。</summary>
        public static void PreviewDeadlineText(string raw, Label output, string emptyHint)
        {
            raw = (raw ?? "").Trim();
            if (raw.Length == 0)
            {
                output.Text = emptyHint;
                output.ForeColor = CMeta;
                return;
            }
            string iso = DeadlineParser.Parse(raw);
            if (iso != null)
            {
                DeadlineInfo dl = DeadlineParser.Info(iso);
                output.Text = "✓ 识别为 " + iso + "（" + dl.Label + "）";
                output.ForeColor = dl.Cls == "overdue" ? CRed : (dl.Cls == "today" ? COrange : CSoon);
            }
            else
            {
                output.Text = "⚠ 无法识别为日期，将按文字原样显示：" + raw;
                output.ForeColor = COrange;
            }
        }

        private void EditSelectedOrNew()
        {
            EditItemFormLoop(SelectedItem());
        }

        private void EditSelectedOrNew(TodoItem it)
        {
            EditItemFormLoop(it);
        }

        private void EditItemFormLoop(TodoItem existing)
        {
            bool isNew = existing == null;
            TodoItem src = isNew
                ? new TodoItem { Id = store.NextId() }
                : existing;
            using (EditItemForm f = new EditItemForm(src))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
                TodoItem result = f.Result;
                if (isNew)
                {
                    store.Items.Add(result);
                }
                else
                {
                    int idx = store.Items.IndexOf(existing);
                    if (idx >= 0) store.Items[idx] = result;
                    else store.Items.Add(result);
                }
            }
            Render();
            ScheduleSave();
        }

        private void DeleteSelected()
        {
            TodoItem it = SelectedItem();
            if (it == null)
            {
                MessageBox.Show(this, "请先选择一条待办项。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            DialogResult r = MessageBox.Show(this, "删除「" + it.Text + "」？", "确认删除",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
            if (r != DialogResult.Yes) return;
            store.Items.Remove(it);
            Render();
            ScheduleSave();
        }

        private void ToggleCheck(TodoItem it)
        {
            if (it == null) return;
            it.Done = !it.Done;
            Render();
            ScheduleSave();
        }

        private void ResetChecks()
        {
            if (MessageBox.Show(this, "重置所有勾选状态？", "重置勾选",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            foreach (TodoItem it in store.Items) it.Done = false;
            Render();
            ScheduleSave();
        }

        private void ClearAll()
        {
            DialogResult r = MessageBox.Show(this, "清空所有数据并恢复默认？此操作不可撤销！", "清空重建",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (r != DialogResult.Yes) return;
            store.Items.Clear();
            store.Items.Add(new TodoItem
            {
                Id = 1,
                Text = "示例：整理接口文档，同步确认字段",
                Owner = "张三",
                DeadlineText = "后天"
            });
            store.SortByDeadline = false;
            Render();
            ScheduleSave();
        }

        private void ToggleSort()
        {
            store.SortByDeadline = !store.SortByDeadline;
            Render();
            ScheduleSave();
        }

        private void ExportData()
        {
            using (SaveFileDialog dlg = new SaveFileDialog())
            {
                dlg.Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*";
                dlg.FileName = "制卡技术待办清单.txt";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                List<TodoItem> rows = new List<TodoItem>(store.Items);
                if (store.SortByDeadline) rows.Sort(CompareByDeadline);

                StringBuilder sb = new StringBuilder();
                sb.Append("制卡技术待办清单  导出时间：").AppendLine(DateTime.Now.ToString("yyyy/M/d HH:mm:ss"));
                sb.Append("共 ").Append(rows.Count).AppendLine(" 项");
                sb.AppendLine();
                int i = 0;
                foreach (TodoItem it in rows)
                {
                    i++;
                    sb.Append(i).Append(". [").Append(it.Done ? "✓" : " ").Append("] ").Append(it.Text);
                    if (it.Owner.Length > 0) sb.Append("  |  负责人: ").Append(it.Owner);
                    string iso = DeadlineParser.ResolveISO(it);
                    if (iso != null)
                    {
                        DeadlineInfo dl = DeadlineParser.Info(iso);
                        sb.Append("  |  截止: ").Append(iso);
                        if (dl != null && dl.Diff < 0) sb.Append("（已逾期）");
                    }
                    else if (it.DeadlineText.Length > 0)
                    {
                        sb.Append("  |  截止: ").Append(it.DeadlineText);
                    }
                    if (it.Note.Length > 0) sb.Append("  |  备注: ").Append(it.Note);
                    sb.AppendLine();
                }
                try
                {
                    File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "导出失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // ============================================================
        //  保存
        // ============================================================
        private void ScheduleSave()
        {
            saveTimer.Stop();
            saveTimer.Start();
        }

        private void SaveNow()
        {
            saveTimer.Stop();
            store.Save();
            lblSaved.Text = "✓ 已自动保存 " + DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        }

        // ============================================================
        //  小工具
        // ============================================================
        private TodoItem SelectedItem()
        {
            if (list == null || list.SelectedItems.Count == 0) return null;
            return list.SelectedItems[0].Tag as TodoItem;
        }

        private TodoItem HitItem(Point pt)
        {
            ListViewHitTestInfo h = list.HitTest(pt);
            if (h == null || h.Item == null) return null;
            return h.Item.Tag as TodoItem;
        }

        private static Panel MakeStatCard(out Label value, string caption, Color color)
        {
            Panel card = new Panel
            {
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Width = 150,
                Height = 52
            };
            value = new Label
            {
                Dock = DockStyle.Top,
                Height = 28,
                TextAlign = ContentAlignment.MiddleCenter,
                Text = "0",
                Font = new Font("微软雅黑", 14F, FontStyle.Bold),
                ForeColor = color
            };
            Label cap = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Text = caption,
                ForeColor = CMeta,
                Font = new Font("微软雅黑", 8.25F)
            };
            card.Controls.Add(cap);
            card.Controls.Add(value);
            return card;
        }

        private static void LayoutStatCards(Panel host)
        {
            const int cw = 150, ch = 50, gap = 16;
            int total = 3 * cw + 2 * gap;
            int x = Math.Max(12, (host.ClientSize.Width - total) / 2);
            if (host.Controls.Count >= 3)
            {
                host.Controls[0].SetBounds(x, 8, cw, ch);
                host.Controls[1].SetBounds(x + cw + gap, 8, cw, ch);
                host.Controls[2].SetBounds(x + 2 * (cw + gap), 8, cw, ch);
            }
        }

        private static Button MakeButton(string text, Color back, Color fore, bool bordered)
        {
            Button b = new Button
            {
                Text = text,
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                BackColor = back,
                ForeColor = fore,
                Padding = new Padding(8, 2, 8, 2),
                Margin = new Padding(0, 2, 8, 2)
            };
            b.FlatAppearance.BorderSize = bordered ? 1 : 0;
            b.FlatAppearance.BorderColor = Color.FromArgb(0xDD, 0xDD, 0xDD);
            return b;
        }

        private static Control MakeGap(int width)
        {
            Label gap = new Label { Width = width, Margin = new Padding(0), AutoSize = false };
            return gap;
        }

        private static bool GetAutoStart()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKeyPath))
                {
                    return k != null && k.GetValue(AutostartName) != null;
                }
            }
            catch { return false; }
        }

        private static void SetAutoStart(bool on)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunKeyPath))
                {
                    if (k == null) return;
                    if (on) k.SetValue(AutostartName, "\"" + Application.ExecutablePath + "\"");
                    else k.DeleteValue(AutostartName, false);
                }
            }
            catch { }
        }
    }

    /// <summary>开启双缓冲的 ListView，避免列表重绘闪烁。</summary>
    internal class BufferedListView : ListView
    {
        public BufferedListView()
        {
            DoubleBuffered = true;
        }
    }
}
