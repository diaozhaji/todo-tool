using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace TodoTool
{
    /// <summary>截止日期的解析结果信息。</summary>
    public sealed class DeadlineInfo
    {
        public int Diff;
        /// <summary>""=普通 / "overdue" / "today" / "soon"</summary>
        public string Cls;
        /// <summary>短标签，如 "10/22" 或跨年 "2027/1/5"。</summary>
        public string Label;
        /// <summary>提示，如 "已逾期 3 天"。</summary>
        public string Tip;
        public string Full
        {
            get { return string.IsNullOrEmpty(Tip) ? Label : Label + " · " + Tip; }
        }
    }

    /// <summary>
    /// 自然语言截止日期解析（移植自原 HTML 版 parseDeadline，规则保持一致）。
    /// 支持：10-20 / 2026-10-20 / 10月20日 / 2026年10月20日 / 20261020 / 261020 /
    ///       今天 明天 后天 大后天 昨天 / 本周X 下周X / 月底 下月底 /
    ///       N天(周/星期/礼拜/个月)之后|以前 / +3d -2w 等。
    /// </summary>
    public static class DeadlineParser
    {
        private static readonly Dictionary<string, int> REL = new Dictionary<string, int>
        {
            { "今天", 0 }, { "明天", 1 }, { "后天", 2 }, { "大后天", 3 }, { "昨天", -1 }
        };

        private static readonly Dictionary<string, int> WEEK = new Dictionary<string, int>
        {
            { "一", 1 }, { "二", 2 }, { "三", 3 }, { "四", 4 }, { "五", 5 }, { "六", 6 },
            { "日", 0 }, { "天", 0 },
            { "1", 1 }, { "2", 2 }, { "3", 3 }, { "4", 4 }, { "5", 5 }, { "6", 6 }, { "7", 0 }
        };

        public static DateTime Today0()
        {
            return DateTime.Now.Date;
        }

        public static bool IsIso(string s)
        {
            if (string.IsNullOrEmpty(s) || !Regex.IsMatch(s, @"^\d{4}-\d{2}-\d{2}$")) return false;
            DateTime d;
            return DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out d);
        }

        private static string Pad2(int n)
        {
            return n.ToString("00", CultureInfo.InvariantCulture);
        }

        public static string MakeISO(int y, int m, int d)
        {
            if (!(y >= 1900 && y <= 2999) || !(m >= 1 && m <= 12) || !(d >= 1 && d <= 31)) return null;
            try
            {
                DateTime dt = new DateTime(y, m, d);
                if (dt.Year != y || dt.Month != m || dt.Day != d) return null;
            }
            catch
            {
                return null;
            }
            return y.ToString("0000", CultureInfo.InvariantCulture) + "-" + Pad2(m) + "-" + Pad2(d);
        }

        public static string IsoFromDay(int dayOffset)
        {
            DateTime t = Today0().AddDays(dayOffset);
            return t.Year.ToString("0000") + "-" + Pad2(t.Month) + "-" + Pad2(t.Day);
        }

        /// <summary>相对今天 ±N 个月（保留日，超出取月末，与原版 addMonths 一致）。</summary>
        private static string AddMonths(int months)
        {
            DateTime t = Today0();
            int total = t.Year * 12 + (t.Month - 1) + months;
            int y = (int)Math.Floor((double)total / 12);
            int mo = total - y * 12; // 0-based，可能为负，需规格化
            while (mo < 0) { mo += 12; y--; }
            while (mo > 11) { mo -= 12; y++; }
            DateTime lastDay = new DateTime(y, mo + 1, 1).AddMonths(1).AddDays(-1);
            int dd = Math.Min(t.Day, lastDay.Day);
            return lastDay.Year.ToString("0000") + "-" + Pad2(lastDay.Month) + "-" + Pad2(dd);
        }

        /// <summary>解析自然语言日期。返回 ISO yyyy-MM-dd，无法识别返回 null。</summary>
        public static string Parse(string raw)
        {
            string s = (raw ?? "").Trim();
            if (s.Length == 0) return null;

            // 1) +3d / -2w / 5天 / 2周 / 3星期
            Match off = Regex.Match(s, @"^([+-])\s*(\d+)\s*(d|w|天|周|个?星期|days?)?$", RegexOptions.IgnoreCase);
            if (off.Success)
            {
                int sign = off.Groups[1].Value == "-" ? -1 : 1;
                string unit = off.Groups[3].Value;
                if (unit.Length == 0) unit = "d";
                unit = unit.ToLowerInvariant();
                int n = sign * int.Parse(off.Groups[2].Value, CultureInfo.InvariantCulture) *
                        (Regex.IsMatch(unit, @"^(w|周|个?星期|week)") ? 7 : 1);
                return IsoFromDay(n);
            }

            // 2) 2026-10-20 / 2026年10月20日 / 2026.10.20 / 2026/10/20
            Match m = Regex.Match(s, @"^(\d{4})\s*[-/.年月]\s*(\d{1,2})\s*[-/.月]\s*(\d{1,2})\s*日?$");
            if (m.Success)
                return MakeISO(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value));

            // 3) 20261020 / 261020
            if (Regex.IsMatch(s, @"^\d{8}$"))
                return MakeISO(int.Parse(s.Substring(0, 4)), int.Parse(s.Substring(4, 2)), int.Parse(s.Substring(6, 2)));
            if (Regex.IsMatch(s, @"^\d{6}$"))
                return MakeISO(2000 + int.Parse(s.Substring(0, 2)), int.Parse(s.Substring(2, 2)), int.Parse(s.Substring(4, 2)));

            // 4) 月底 / 月末 / 本月底 / 这个月底 / 下个月底 / 下月底
            if (Regex.IsMatch(s, @"^(?:本月|这个月|本|下(?:个月|周)?|下个)?月[底末]$"))
            {
                bool add = s.IndexOf('下') >= 0;
                DateTime t = Today0();
                DateTime last = new DateTime(t.Year, t.Month, 1).AddMonths(add ? 2 : 1).AddDays(-1);
                return MakeISO(last.Year, last.Month, last.Day);
            }

            // 5) 3天之后 / 2周以后 / 5个月之前 / 10日以后 …
            Match rl = Regex.Match(s, @"^(\d{1,3})\s*(天|日|周|星期|礼拜|个?月)\s*(之?后|以?后|之?前|以?前)$");
            if (rl.Success)
            {
                int n = int.Parse(rl.Groups[1].Value, CultureInfo.InvariantCulture);
                int sign = rl.Groups[3].Value.IndexOf('后') >= 0 ? 1 : -1;
                string u = rl.Groups[2].Value;
                if (u == "天" || u == "日") return IsoFromDay(sign * n);
                if (u == "周" || u == "星期" || u == "礼拜") return IsoFromDay(sign * n * 7);
                return AddMonths(sign * n);
            }

            // 6) 今天 / 明天 / 后天 / 大后天 / 昨天
            int rel;
            if (REL.TryGetValue(s, out rel)) return IsoFromDay(rel);

            // 7) 下周三 / 下个星期五
            Match nextRel = Regex.Match(s, @"^下(?:个)?(周|星期|礼拜)([一二三四五六日天1-7])$");
            if (nextRel.Success)
            {
                int target = WEEK[nextRel.Groups[2].Value];
                int cur = (int)Today0().DayOfWeek;
                return IsoFromDay((7 - cur + target) + 7);
            }

            // 8) 本周三 / 周五 / 这周五
            Match thisRel = Regex.Match(s, @"^(?:这|本)?(周|星期|礼拜)([一二三四五六日天1-7])$");
            if (thisRel.Success)
            {
                int target = WEEK[thisRel.Groups[2].Value];
                int cur = (int)Today0().DayOfWeek;
                int diff = target - cur;
                return IsoFromDay(diff >= 0 ? diff : diff + 7);
            }

            // 9) 10-20 / 10/20 / 10.20 / 10月20日（当年，早于今天 60 天以上则视为明年）
            m = Regex.Match(s, @"^(\d{1,2})\s*[-/.月]\s*(\d{1,2})\s*日?$");
            if (m.Success)
            {
                int mm = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                int dd = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                int y = Today0().Year;
                string iso = MakeISO(y, mm, dd);
                if (iso == null) return null;
                DateTime dt = DateTime.ParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                if ((dt - Today0()).Days < -60) iso = MakeISO(y + 1, mm, dd);
                return iso;
            }
            return null;
        }

        /// <summary>取条目的有效 ISO 截止日期（deadline 优先，否则尝试解析原文）。</summary>
        public static string ResolveISO(TodoItem item)
        {
            if (item == null) return null;
            if (!string.IsNullOrEmpty(item.Deadline) && IsIso(item.Deadline)) return item.Deadline;
            string cand = item.Deadline ?? "";
            if (string.IsNullOrEmpty(cand)) cand = item.DeadlineText ?? "";
            if (string.IsNullOrEmpty(cand)) return null;
            return Parse(cand);
        }

        /// <summary>把用户输入写回条目：识别成功存 ISO，失败存原文。</summary>
        public static void ApplyDeadline(TodoItem item, string raw)
        {
            string text = (raw ?? "").Trim();
            string iso = Parse(text);
            item.Deadline = iso ?? "";
            item.DeadlineText = iso == null ? text : "";
        }

        /// <summary>计算截止日期展示信息（着色、提示）。</summary>
        public static DeadlineInfo Info(string iso)
        {
            if (string.IsNullOrEmpty(iso) || !IsIso(iso)) return null;
            DateTime dt = DateTime.ParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            int diff = (dt - Today0()).Days;
            string cls = "", tip = "";
            if (diff < 0) { cls = "overdue"; tip = "已逾期 " + (-diff) + " 天"; }
            else if (diff == 0) { cls = "today"; tip = "今天到期"; }
            else if (diff == 1) { cls = "soon"; tip = "明天到期"; }
            else if (diff <= 3) { cls = "soon"; tip = "还剩 " + diff + " 天"; }
            DateTime now = Today0();
            string label = dt.Year == now.Year
                ? (dt.Month + "/" + dt.Day)
                : (dt.Year + "/" + dt.Month + "/" + dt.Day);
            return new DeadlineInfo { Diff = diff, Cls = cls, Label = label, Tip = tip };
        }
    }
}
