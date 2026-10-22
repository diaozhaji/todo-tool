import { DEFAULT_ITEMS, type TodoData, type TodoItemData } from "../types";

export const pad2 = (n: number): string => String(n).padStart(2, "0");

export function isISO(s: string): boolean {
  if (typeof s !== "string" || !/^\d{4}-\d{2}-\d{2}$/.test(s)) return false;
  const d = new Date(s + "T00:00:00");
  return !isNaN(d.getTime());
}

export function makeISO(y: number, m: number, d: number): string | null {
  if (!(y >= 1900 && y <= 2999) || !(m >= 1 && m <= 12) || !(d >= 1 && d <= 31)) return null;
  const dt = new Date(y, m - 1, d);
  if (dt.getFullYear() !== y || dt.getMonth() + 1 !== m || dt.getDate() !== d) return null;
  return y + "-" + pad2(m) + "-" + pad2(d);
}

function today0(): Date {
  const t = new Date();
  t.setHours(0, 0, 0, 0);
  return t;
}

export function isoFromDay(dayOffset: number): string {
  const t = today0();
  t.setDate(t.getDate() + dayOffset);
  return t.getFullYear() + "-" + pad2(t.getMonth() + 1) + "-" + pad2(t.getDate());
}

function addMonths(months: number): string {
  const t = today0();
  const total = t.getFullYear() * 12 + t.getMonth() + months;
  const y = Math.floor(total / 12);
  const mo = total % 12;
  const d = new Date(y, mo + 1, 0);
  d.setDate(Math.min(t.getDate(), d.getDate()));
  return d.getFullYear() + "-" + pad2(d.getMonth() + 1) + "-" + pad2(d.getDate());
}

const WEEK_MAP: Record<string, number> = {
  一: 1, 二: 2, 三: 3, 四: 4, 五: 5, 六: 6, 日: 0, 天: 0,
  1: 1, 2: 2, 3: 3, 4: 4, 5: 5, 6: 6, 7: 0,
};
const REL_MAP: Record<string, number> = { 今天: 0, 明天: 1, 后天: 2, 大后天: 3, 昨天: -1 };

export function parseDeadline(raw: string): string | null {
  const s = String(raw || "").trim();
  if (!s) return null;

  const off = s.match(/^([+-])\s*(\d+)\s*(d|w|天|周|个?星期|days?)?$/i);
  if (off) {
    const sign = off[1] === "-" ? -1 : 1;
    const unit = (off[3] || "d").toLowerCase();
    const n = sign * +off[2] * (/^(w|周|个?星期|week)/.test(unit) ? 7 : 1);
    return isoFromDay(n);
  }

  let m = s.match(/^(\d{4})\s*[-\/.年月]\s*(\d{1,2})\s*[-\/.月]\s*(\d{1,2})\s*日?$/);
  if (m) return makeISO(+m[1], +m[2], +m[3]);

  if (/^\d{8}$/.test(s)) return makeISO(+s.slice(0, 4), +s.slice(4, 6), +s.slice(6, 8));
  if (/^\d{6}$/.test(s)) return makeISO(2000 + +s.slice(0, 2), +s.slice(2, 4), +s.slice(4, 6));

  const me = s.match(/^(?:本月|这个月|本|下(?:个月|周)?|下个)?月[底末]$/);
  if (me) {
    const t = today0();
    const add = /下/.test(s) ? 1 : 0;
    const y = t.getFullYear();
    const mo = t.getMonth() + add;
    const last = new Date(y, mo + 1, 0);
    return makeISO(last.getFullYear(), last.getMonth() + 1, last.getDate());
  }

  const rl = s.match(/^(\d{1,3})\s*(天|日|周|星期|礼拜|个?月)\s*(之?后|以?后|之?前|以?前)$/);
  if (rl) {
    const n = +rl[1];
    const sign = /后/.test(rl[3]) ? 1 : -1;
    if (/^(天|日)$/.test(rl[2])) return isoFromDay(sign * n);
    if (/^(周|星期|礼拜)$/.test(rl[2])) return isoFromDay(sign * n * 7);
    return addMonths(sign * n);
  }

  if (REL_MAP[s] !== undefined) return isoFromDay(REL_MAP[s]);

  const nextRel = s.match(/^下(?:个)?(周|星期|礼拜)([一二三四五六日天1-7])$/);
  if (nextRel) {
    const target = WEEK_MAP[nextRel[2]];
    const cur = today0().getDay();
    return isoFromDay((7 - cur + target) + 7);
  }

  const thisRel = s.match(/^(?:这|本)?(周|星期|礼拜)([一二三四五六日天1-7])$/);
  if (thisRel) {
    const target = WEEK_MAP[thisRel[2]];
    const cur = today0().getDay();
    const diff = target - cur;
    return isoFromDay(diff >= 0 ? diff : diff + 7);
  }

  m = s.match(/^(\d{1,2})\s*[-\/.月]\s*(\d{1,2})\s*日?$/);
  if (m) {
    const mm = +m[1];
    const dd = +m[2];
    const y = today0().getFullYear();
    let iso = makeISO(y, mm, dd);
    if (!iso) return null;
    const dt = new Date(iso + "T00:00:00");
    if ((dt.getTime() - today0().getTime()) / 86400000 < -60) iso = makeISO(y + 1, mm, dd);
    return iso;
  }
  return null;
}

export function resolveISO(item: TodoItemData): string | null {
  if (item.deadline && isISO(item.deadline)) return item.deadline;
  const cand = item.deadline || item.deadlineText || "";
  if (!cand) return null;
  return parseDeadline(cand);
}

export function applyDeadline(item: TodoItemData, raw: string): void {
  const text = String(raw || "").trim();
  const iso = parseDeadline(text);
  item.deadline = iso || "";
  item.deadlineText = iso ? "" : text;
}

export interface DeadlineInfo {
  diff: number;
  cls: string;
  label: string;
  raw: string;
}

export function deadlineInfo(iso: string): DeadlineInfo | null {
  if (!iso || !isISO(iso)) return null;
  const dt = new Date(iso + "T00:00:00");
  const diff = Math.round((dt.getTime() - today0().getTime()) / 86400000);
  let cls = "";
  let tip = "";
  if (diff < 0) { cls = "overdue"; tip = "已逾期 " + -diff + " 天"; }
  else if (diff === 0) { cls = "today"; tip = "今天到期"; }
  else if (diff === 1) { cls = "soon"; tip = "明天到期"; }
  else if (diff <= 3) { cls = "soon"; tip = "还剩 " + diff + " 天"; }
  const y = dt.getFullYear();
  const mo = dt.getMonth() + 1;
  const d = dt.getDate();
  const label = y === today0().getFullYear() ? mo + "/" + d : y + "/" + mo + "/" + d;
  return { diff, cls, label: label + (tip ? " · " + tip : ""), raw: iso };
}

/* ---------------- 数据加载 / 归一化 ---------------- */

export function normalizeData(raw: TodoData | null): TodoData {
  let items: TodoItemData[];
  let checked: Record<string, boolean>;
  let sortByDeadline: boolean;

  if (raw && Array.isArray(raw.items)) {
    items = raw.items.map((i) => ({ ...i }));
    checked = raw.checked || {};
    sortByDeadline = !!raw.sortByDeadline;
  } else {
    items = DEFAULT_ITEMS.map((i) => ({ ...i }));
    checked = {};
    sortByDeadline = false;
  }

  items.forEach((i) => {
    if (typeof i.owner !== "string") i.owner = "";
    if (typeof i.deadlineText !== "string") i.deadlineText = "";
    if (typeof i.deadline !== "string") i.deadline = "";
    if (typeof i.note !== "string") i.note = "";
    if (typeof i.text !== "string") i.text = "";
    if (i.deadline && !isISO(i.deadline)) {
      if (!i.deadlineText) i.deadlineText = i.deadline;
      i.deadline = "";
    }
    if (!i.deadline && i.deadlineText) {
      const up = parseDeadline(i.deadlineText);
      if (up) {
        i.deadline = up;
        i.deadlineText = "";
      }
    }
  });

  return { updatedAt: Date.now(), items, checked, sortByDeadline };
}

export function noteClass(note: string): string {
  if (!note) return "";
  if (note.includes("✓")) return "check";
  if (note.includes("×")) return "cross";
  return "";
}