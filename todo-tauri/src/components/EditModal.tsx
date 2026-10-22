import { useState } from "react";
import type { TodoItemData } from "../types";
import { applyDeadline } from "../utils/date";
import DeadlineField from "./DeadlineField";

interface EditModalProps {
  item: TodoItemData;
  onSave: (item: TodoItemData) => void;
  onCancel: () => void;
}

export default function EditModal({ item, onSave, onCancel }: EditModalProps) {
  const [text, setText] = useState(item.text);
  const [owner, setOwner] = useState(item.owner || "");
  const [deadline, setDeadline] = useState(item.deadline || item.deadlineText || "");
  const [note, setNote] = useState(item.note || "");

  const save = () => {
    if (!text.trim()) return;
    const updated: TodoItemData = { ...item, text: text.trim(), owner: owner.trim(), deadline: "", deadlineText: "", note: note.trim() };
    applyDeadline(updated, deadline);
    onSave(updated);
  };

  const enterSaves = (e: React.KeyboardEvent) => {
    if (e.key === "Enter") {
      e.preventDefault();
      save();
    }
  };

  return (
    <div className="modal-overlay show" onMouseDown={(e) => e.target === e.currentTarget && onCancel()}>
      <div className="modal">
        <h3>编辑待办项</h3>
        <label>内容</label>
        <textarea rows={2} value={text} onChange={(e) => setText(e.target.value)} />
        <div className="row2">
          <div>
            <label>负责人</label>
            <input placeholder="可选" autoComplete="off" value={owner} onChange={(e) => setOwner(e.target.value)} onKeyDown={enterSaves} />
          </div>
          <div>
            <label>截止日期</label>
            <DeadlineField value={deadline} onChange={setDeadline} onInputKeyDown={enterSaves} />
            <button className="clear-btn" onClick={() => setDeadline("")}>
              清除日期
            </button>
          </div>
        </div>
        <label>备注</label>
        <input placeholder="可选" autoComplete="off" value={note} onChange={(e) => setNote(e.target.value)} onKeyDown={enterSaves} />
        <div className="modal-btns">
          <button onClick={onCancel}>取消</button>
          <button className="primary" onClick={save}>
            保存
          </button>
        </div>
      </div>
    </div>
  );
}