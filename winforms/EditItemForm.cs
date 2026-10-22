using System;
using System.Drawing;
using System.Windows.Forms;

namespace TodoTool
{
    /// <summary>新建 / 编辑待办项弹窗（对应原版编辑 Modal，含日期实时预览）。</summary>
    public class EditItemForm : Form
    {
        private readonly TodoItem working;
        private readonly TextBox txtText;
        private readonly TextBox txtNote;
        private readonly TextBox txtOwner;
        private readonly TextBox txtDeadline;
        private readonly Label lblPreview;
        private readonly Panel dlHost;
        private readonly Button btnPick;
        private readonly Button btnClearDate;
        private CalendarPopup popup;

        public TodoItem Result;

        public EditItemForm(TodoItem source)
        {
            working = source.Clone();
            bool isNew = source.Text.Length == 0;

            Text = isNew ? "新建待办项" : "编辑待办项";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(470, 346);
            BackColor = Color.White;
            Font = new Font("微软雅黑", 9F);

            Label lblTitle = new Label
            {
                Text = Text,
                Dock = DockStyle.Top,
                Height = 42,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(20, 6, 0, 0),
                ForeColor = Color.FromArgb(0x1A, 0x1A, 0x1A),
                Font = new Font("微软雅黑", 12F, FontStyle.Bold)
            };

            TableLayoutPanel grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 6,
                Padding = new Padding(20, 6, 20, 12)
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 74));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int r = 0; r < 6; r++) grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            const AnchorStyles LR = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;

            // 行0：内容（多行）
            grid.Controls.Add(MakeCaption("内容"), 0, 0);
            txtText = new TextBox
            {
                Multiline = true,
                Height = 58,
                Width = 300,
                Anchor = LR,
                ScrollBars = ScrollBars.Vertical,
                Text = working.Text
            };
            grid.Controls.Add(txtText, 1, 0);

            // 行1：负责人
            grid.Controls.Add(MakeCaption("负责人"), 0, 1);
            txtOwner = new TextBox { Width = 300, Anchor = LR, Text = working.Owner };
            grid.Controls.Add(txtOwner, 1, 1);

            // 行2：截止日期
            grid.Controls.Add(MakeCaption("截止日期"), 0, 2);
            txtDeadline = new TextBox { Width = 300, Anchor = LR, Text = working.Deadline.Length > 0 ? working.Deadline : working.DeadlineText };
            txtDeadline.TextChanged += delegate { UpdatePreview(); };
            btnPick = new Button
            {
                Text = "📅",
                Width = 32,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0xF1, 0xF3, 0xF5),
                Font = new Font("微软雅黑", 9F),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnPick.FlatAppearance.BorderSize = 0;
            btnPick.Click += delegate { OpenPopup(); };
            btnClearDate = new Button
            {
                Text = "清除",
                Width = 38,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.FromArgb(0xC6, 0x28, 0x28),
                Font = new Font("微软雅黑", 8.25F),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnClearDate.FlatAppearance.BorderSize = 0;
            btnClearDate.Click += delegate { txtDeadline.Text = ""; };
            dlHost = new Panel { Dock = DockStyle.Fill, Height = 30, Margin = new Padding(0) };
            dlHost.Controls.Add(txtDeadline);
            dlHost.Controls.Add(btnPick);
            dlHost.Controls.Add(btnClearDate);
            dlHost.Resize += LayoutDeadlineRow;
            grid.Controls.Add(dlHost, 1, 2);

            // 行3：解析预览
            lblPreview = new Label
            {
                Dock = DockStyle.Fill,
                Height = 26,
                Padding = new Padding(1, 6, 0, 0),
                ForeColor = Color.FromArgb(0x88, 0x88, 0x88),
                Font = new Font("微软雅黑", 8.25F)
            };
            grid.Controls.Add(new Label(), 0, 3);
            grid.Controls.Add(lblPreview, 1, 3);

            // 行4：备注
            grid.Controls.Add(MakeCaption("备注"), 0, 4);
            txtNote = new TextBox { Width = 300, Anchor = LR, Text = working.Note };
            grid.Controls.Add(txtNote, 1, 4);

            // 行5：按钮
            FlowLayoutPanel btns = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                Anchor = LR,
                Margin = new Padding(0, 12, 0, 0)
            };
            Button btnSave = new Button
            {
                Text = "保存",
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0x2B, 0x57, 0x9A),
                ForeColor = Color.White,
                Padding = new Padding(10, 2, 10, 2)
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += delegate { SaveAndClose(); };
            Button btnCancel = new Button
            {
                Text = "取消",
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 10, 0),
                Padding = new Padding(10, 2, 10, 2)
            };
            btnCancel.Click += delegate { Close(); };
            btns.Controls.Add(btnSave);
            btns.Controls.Add(btnCancel);
            grid.Controls.Add(new Label(), 0, 5);
            grid.Controls.Add(btns, 1, 5);

            Controls.Add(grid);
            Controls.Add(lblTitle);

            AcceptButton = btnSave;
            CancelButton = btnCancel;

            UpdatePreview();
            ActiveControl = txtText;
        }

        private static Label MakeCaption(string text)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(0x88, 0x88, 0x88),
                TextAlign = ContentAlignment.MiddleRight
            };
        }

        private void LayoutDeadlineRow(object sender, EventArgs e)
        {
            int w = dlHost.ClientSize.Width;
            if (w <= 0) return;
            txtDeadline.Width = Math.Max(60, w - 74);
            btnPick.SetBounds(w - 72, 2, 32, 26);
            btnClearDate.SetBounds(w - 38, 2, 38, 26);
        }

        private void OpenPopup()
        {
            if (popup != null && !popup.IsDisposed) { popup.Close(); popup = null; return; }
            string current = txtDeadline.Text.Trim();
            string iso = DeadlineParser.IsIso(current) ? current : DeadlineParser.Parse(current);
            popup = CalendarPopup.ShowBelow(btnPick, iso, delegate (string picked)
            {
                txtDeadline.Text = picked ?? "";
                txtDeadline.Focus();
            });
        }

        private void UpdatePreview()
        {
            MainForm.PreviewDeadlineText(txtDeadline.Text, lblPreview, "支持：10-20 / 明天 / 下周三 / 月底，或点 📅 选择");
        }

        private void SaveAndClose()
        {
            string text = txtText.Text.Trim();
            if (text.Length == 0)
            {
                lblPreview.Text = "⚠ 请先输入待办内容";
                lblPreview.ForeColor = Color.FromArgb(0xC6, 0x28, 0x28);
                ActiveControl = txtText;
                return;
            }
            working.Text = text;
            working.Owner = txtOwner.Text.Trim();
            working.Note = txtNote.Text.Trim();
            DeadlineParser.ApplyDeadline(working, txtDeadline.Text);
            Result = working;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
