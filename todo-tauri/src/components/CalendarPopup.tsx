import { useEffect, useRef, useState, type ReactNode } from "react";
import { isISO, parseDeadline, pad2 } from "../utils/date";

const CAL_QUICK = ["今天", "明天", "后天", "本周五", "下周三", "月底"];
const WD = ["一", "二", "三", "四", "五", "六", "日"];

interface CalendarPopupProps {
  /** 关联输入框 触发弹出的控件位置 */
  anchor: DOMRect | null;
  selected: string;
  onPick: (iso: string) => void;
  onClose: () => void;
  onClear: () => void;
}

interface CalState {
  y: number;
  m: number;
  sel: string;
}

function makeISOOrNull(y: number, m: number, d: number): string | null {
  const dt = new Date(y, m - 1, d);
  if (dt.getFullYear() !== y || dt.getMonth() + 1 !== m || dt.getDate() !== d) return null;
  return y + "-" + pad2(m) + "-" + pad2(d);
}

function yearOf(iso: string): { y: number; m: number } {
  const b = new Date(iso + "T00:00:00");
  return { y: b.getFullYear(), m: b.getMonth() + 1 };
}

function today(): CalState {
  const t = new Date();
  return { y: t.getFullYear(), m: t.getMonth() + 1, sel: "" };
}

export default function CalendarPopup({ anchor, selected, onPick, onClose, onClear }: CalendarPopupProps) {
  const [cal, setCal] = useState<CalState>(() => {
    const iso = isISO(selected) ? selected : parseDeadline(selected);
    if (iso) return { ...yearOf(iso), sel: iso };
    return today();
  });
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const iso = isISO(selected) ? selected : parseDeadline(selected);
    if (iso) {
      setCal(() => ({ ...yearOf(iso), sel: iso }));
    }
  }, [selected]);

  useEffect(() => {
    const onClick = (e: MouseEvent) => {
      if (!ref.current?.contains(e.target as Node)) onClose();
    };
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") onClose();
    };
    document.addEventListener("mousedown", onClick);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("mousedown", onClick);
      document.removeEventListener("keydown", onKey);
    };
  }, [onClose]);

  const popRect = ref.current?.getBoundingClientRect();
  let left = 0;
  let top = 0;
  if (anchor) {
    const pw = 268;
    const ph = popRect ?? { width: 268, height: 330 };
    top = anchor.bottom + 6;
    if (top + ph.height > window.innerHeight - 8) top = Math.max(8, anchor.top - ph.height - 6);
    left = anchor.left;
    if (left + pw > window.innerWidth - 8) left = Math.max(8, window.innerWidth - pw - 8);
  }

  const lead = (new Date(cal.y, cal.m - 1, 1).getDay() + 6) % 7;
  const days = new Date(cal.y, cal.m, 0).getDate();
  const todayIso = (() => {
    const t = new Date();
    return t.getFullYear() + "-" + pad2(t.getMonth() + 1) + "-" + pad2(t.getDate());
  })();

  const cells: ReactNode[] = [];
  for (let i = 0; i < lead; i++) cells.push(<div key={"e" + i} className="cal-day empty" />);
  for (let d = 1; d <= days; d++) {
    const iso = makeISOOrNull(cal.y, cal.m, d);
    const wd = new Date(cal.y, cal.m - 1, d).getDay();
    const cls =
      "cal-day" +
      (iso === todayIso ? " today" : "") +
      (iso === cal.sel ? " sel" : "") +
      (wd === 0 || wd === 6 ? " weekend" : "");
    cells.push(
      <div key={d} className={cls} onClick={() => iso && onPick(iso)}>
        {d}
      </div>,
    );
  }

  const shiftMonth = (delta: number) => {
    setCal((c) => {
      const total = c.y * 12 + (c.m - 1) + delta;
      return { ...c, y: Math.floor(total / 12), m: ((total % 12) + 12) % 12 + 1 };
    });
  };

  const gotoToday = () => setCal((c) => {
    const t = new Date();
    return { ...c, y: t.getFullYear(), m: t.getMonth() + 1 };
  });

  return (
    <div className="cal-pop" style={{ left, top }} ref={ref}>
      <div className="cal-head">
        <button type="button" className="cal-nav" title="上一年" onClick={() => shiftMonth(-12)}>«</button>
        <button type="button" className="cal-nav" title="上个月" onClick={() => shiftMonth(-1)}>‹</button>
        <div className="cal-title">{cal.y} 年 {cal.m} 月</div>
        <button type="button" className="cal-nav" title="下个月" onClick={() => shiftMonth(1)}>›</button>
        <button type="button" className="cal-nav" title="下一年" onClick={() => shiftMonth(12)}>»</button>
      </div>
      <div className="cal-quick">
        {CAL_QUICK.map((q) => (
          <button key={q} type="button" onClick={() => { const iso = parseDeadline(q); if (iso) onPick(iso); }}>
            {q}
          </button>
        ))}
      </div>
      <div className="cal-week">{WD.map((w) => <span key={w}>{w}</span>)}</div>
      <div className="cal-grid">{cells}</div>
      <div className="cal-foot">
        <button type="button" className="cal-clear" onClick={onClear}>清除日期</button>
        <button type="button" onClick={gotoToday}>回到本月</button>
        <button type="button" onClick={onClose}>关闭</button>
      </div>
    </div>
  );
}