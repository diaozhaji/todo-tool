using System;
using System.Drawing;
using System.Windows.Forms;

namespace TodoTool
{
    /// <summary>
    /// 日历弹层（对应原版 HTML 的自建日历面板）：
    /// 快捷选项 + MonthCalendar + 清除日期/关闭；失焦自动关闭。
    /// </summary>
    public class CalendarPopup : Form
    {
        private readonly MonthCalendar cal;
        private bool _picked;

        /// <summary>选择结果回调：iso 为 null 表示清除日期。</summary>
        public event Action<string> Picked;

        private CalendarPopup(string currentIso)
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.White;
            Padding = new Padding(12);

            // 快捷选项行
            string[] quick = { "今天", "明天", "后天", "本周五", "下周三", "月底" };
            FlowLayoutPanel quickPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                Padding = new Padding(0, 0, 0, 8)
            };
            foreach (string q in quick)
            {
                Button b = new Button
                {
                    Text = q,
                    AutoSize = true,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(0xF1, 0xF3, 0xF5),
                    ForeColor = Color.FromArgb(0x55, 0x60, 0x70),
                    Margin = new Padding(0, 0, 6, 0),
                    Font = new Font("微软雅黑", 8.25F)
                };
                b.FlatAppearance.BorderSize = 0;
                b.Click += delegate
                {
                    string iso = DeadlineParser.Parse(q);
                    if (iso != null) FirePicked(iso);
                };
                quickPanel.Controls.Add(b);
            }

            // 日历
            cal = new MonthCalendar
            {
                MaxSelectionCount = 1,
                Dock = DockStyle.Top,
                CalendarDimensions = new Size(1, 1)
            };
            if (DeadlineParser.IsIso(currentIso))
            {
                try { cal.SetDate(DateTime.ParseExact(currentIso, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)); }
                catch { cal.SetDate(DateTime.Now.Date); }
            }
            else
            {
                cal.SetDate(DateTime.Now.Date);
            }
            cal.DateSelected += delegate (object s, DateRangeEventArgs e)
            {
                DateTime d = e.Start;
                FirePicked(d.Year.ToString("0000") + "-" + d.Month.ToString("00") + "-" + d.Day.ToString("00"));
            };

            // 底部按钮
            FlowLayoutPanel foot = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                Padding = new Padding(0, 8, 0, 0)
            };
            Button btnClear = new Button
            {
                Text = "清除日期",
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.FromArgb(0xC6, 0x28, 0x28),
                BackColor = Color.White
            };
            btnClear.FlatAppearance.BorderSize = 1;
            btnClear.FlatAppearance.BorderColor = Color.FromArgb(0xE3, 0xE8, 0xEF);
            btnClear.Click += delegate { FirePicked(null); };

            Button btnClose = new Button
            {
                Text = "关闭",
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.FromArgb(0x66, 0x66, 0x66),
                BackColor = Color.White,
                Margin = new Padding(6, 0, 0, 0)
            };
            btnClose.FlatAppearance.BorderSize = 1;
            btnClose.FlatAppearance.BorderColor = Color.FromArgb(0xE3, 0xE8, 0xEF);
            btnClose.Click += delegate { Close(); };

            foot.Controls.Add(btnClear);
            foot.Controls.Add(btnClose);

            // Dock 处理顺序 = Controls 逆序：先加的在最下，后加的在最上
            Controls.Add(foot);
            Controls.Add(cal);
            Controls.Add(quickPanel);

            AutoScaleMode = AutoScaleMode.None;
            Size = new Size(
                Math.Max(cal.Width, quickPanel.PreferredSize.Width) + Padding.Horizontal + 4,
                quickPanel.PreferredSize.Height + cal.Height + foot.PreferredSize.Height + Padding.Vertical + 2);

            // 失焦自动关闭
            Deactivate += delegate
            {
                if (!_picked) Close();
            };
            Paint += delegate (object s, PaintEventArgs e)
            {
                e.Graphics.DrawRectangle(Pens.Gainsboro, 0, 0, Width - 1, Height - 1);
            };
        }

        private void FirePicked(string iso)
        {
            _picked = true;
            if (Picked != null) Picked(iso);
            Close();
        }

        /// <summary>在锚点控件下方弹出。点击空白/失焦自动关闭。</summary>
        public static CalendarPopup ShowBelow(Control anchor, string currentIso, Action<string> onPicked)
        {
            CalendarPopup p = new CalendarPopup(currentIso);
            p.Picked += onPicked;
            Form host = anchor.FindForm();
            Point pt = anchor.Parent.PointToScreen(new Point(anchor.Left, anchor.Bottom + 4));
            Screen sc = Screen.FromPoint(pt);
            if (pt.X + p.Width > sc.WorkingArea.Right) pt.X = sc.WorkingArea.Right - p.Width;
            if (pt.X < sc.WorkingArea.Left) pt.X = sc.WorkingArea.Left;
            if (pt.Y + p.Height > sc.WorkingArea.Bottom)
                pt.Y = anchor.Parent.PointToScreen(new Point(anchor.Left, anchor.Top)).Y - p.Height - 4;
            if (pt.Y < sc.WorkingArea.Top) pt.Y = sc.WorkingArea.Top;
            p.Location = pt;
            p.Show(host);
            return p;
        }
    }
}
