import { useRef, useState, type CSSProperties } from "react";
import { deadlineInfo, parseDeadline } from "../utils/date";
import CalendarPopup from "./CalendarPopup";

interface DeadlineFieldProps {
  value: string;
  onChange: (raw: string) => void;
  wrapperClass?: string;
  wrapperStyle?: CSSProperties;
  previewClass?: string;
  hintWhenEmpty?: string;
  placeholder?: string;
  onInputKeyDown?: (e: React.KeyboardEvent<HTMLInputElement>) => void;
}

export default function DeadlineField({
  value,
  onChange,
  wrapperClass = "dl-wrap",
  wrapperStyle,
  previewClass = "dl-preview",
  hintWhenEmpty,
  placeholder = "如 10-20 / 下周三",
  onInputKeyDown,
}: DeadlineFieldProps) {
  const [open, setOpen] = useState(false);
  const wrapRef = useRef<HTMLDivElement>(null);
  const [anchorRect, setAnchorRect] = useState<DOMRect | null>(null);

  const togglePicker = () => {
    if (open) {
      setOpen(false);
    } else {
      const el = wrapRef.current;
      setAnchorRect(el ? el.getBoundingClientRect() : null);
      setOpen(true);
    }
  };

  const pick = (iso: string) => {
    setOpen(false);
    onChange(iso);
  };

  const clear = () => {
    setOpen(false);
    onChange("");
  };

  const raw = (value || "").trim();
  const iso = raw ? parseDeadline(raw) : null;

  let previewText = "";
  let previewExtra = "";
  if (!raw) {
    previewText = hintWhenEmpty || "";
  } else if (iso) {
    const dl = deadlineInfo(iso);
    previewText = "✓ 识别为 " + iso + "（" + (dl ? dl.label : "") + "）";
    previewExtra = " ok " + (dl ? dl.cls : "");
  } else {
    previewText = "⚠ 无法识别为日期，将按文字原样显示：" + raw;
    previewExtra = " warn";
  }

  return (
    <>
      <div className={wrapperClass} style={wrapperStyle} ref={wrapRef}>
        <input
          value={value}
          onChange={(e) => onChange(e.target.value)}
          onKeyDown={onInputKeyDown}
          placeholder={placeholder}
          autoComplete="off"
        />
        <button type="button" className={"pick-btn" + (open ? " on" : "")} title="从日历选择" onClick={togglePicker}>
          📅
        </button>
      </div>
      <div className={previewClass + previewExtra}>{previewText}</div>
      {open && anchorRect && (
        <CalendarPopup anchor={anchorRect} selected={value} onPick={pick} onClose={() => setOpen(false)} onClear={clear} />
      )}
    </>
  );
}