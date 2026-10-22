import type { TodoItemData } from "../types";
import { deadlineInfo, noteClass, resolveISO } from "../utils/date";

interface TodoItemProps {
  item: TodoItemData;
  order: number;
  checked: boolean;
  onToggle: () => void;
  onEdit: () => void;
  onDelete: () => void;
}

export default function TodoItem({ item, order, checked, onToggle, onEdit, onDelete }: TodoItemProps) {
  const iso = resolveISO(item);
  const dl = iso ? deadlineInfo(iso) : null;

  const badges: JSX.Element[] = [];
  if (item.owner) {
    badges.push(
      <span key="owner" className="badge owner" title="负责人">
        👤 {item.owner}
      </span>,
    );
  }
  if (dl) {
    badges.push(
      <span key="deadline" className={"badge deadline " + dl.cls} title={"截止：" + dl.raw}>
        ⏰ {dl.label}
      </span>,
    );
  } else if (item.deadlineText) {
    badges.push(
      <span key="deadlineText" className="badge deadline unparsed" title="未能识别为日期，原样显示">
        ⏰ {item.deadlineText}
      </span>,
    );
  }

  return (
    <div className={"item" + (checked ? " checked" : "")}>
      <div className="checkbox" title="标记完成" onClick={onToggle}>
        <svg viewBox="0 0 24 24" fill="none" stroke="white" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round">
          <polyline points="20 6 9 17 4 12" />
        </svg>
      </div>
      <span className="no">{order}</span>
      <div className="body">
        <div className="line1">
          <span className="content">{item.text}</span>
          {item.note ? <span className={"note " + noteClass(item.note)}>{item.note}</span> : null}
        </div>
        {badges.length ? <div className="metaRow">{badges}</div> : null}
      </div>
      <div className="actions">
        <button className="act-btn edit" title="编辑" onClick={onEdit}>
          ✎
        </button>
        <button className="act-btn del" title="删除" onClick={onDelete}>
          ✕
        </button>
      </div>
    </div>
  );
}