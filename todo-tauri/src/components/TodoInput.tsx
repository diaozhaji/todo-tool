import DeadlineField from "./DeadlineField";

const ADD_HINT = "截止日期支持：10-20 / 2026-10-20 / 10月20日 / 明天 / 下周三";

interface TodoInputProps {
  text: string;
  owner: string;
  deadline: string;
  onText: (v: string) => void;
  onOwner: (v: string) => void;
  onDeadline: (v: string) => void;
  onAdd: () => void;
  inputRef: React.Ref<HTMLInputElement>;
}

export default function TodoInput({
  text,
  owner,
  deadline,
  onText,
  onOwner,
  onDeadline,
  onAdd,
  inputRef,
}: TodoInputProps) {
  const onKey = (e: React.KeyboardEvent) => {
    if (e.key === "Enter") {
      e.preventDefault();
      onAdd();
    }
  };

  return (
    <div className="add-row">
      <input
        ref={inputRef}
        className="add-text"
        placeholder="输入新待办项..."
        value={text}
        onChange={(e) => onText(e.target.value)}
        onKeyDown={onKey}
      />
      <input
        className="add-owner"
        placeholder="负责人"
        value={owner}
        onChange={(e) => onOwner(e.target.value)}
        onKeyDown={onKey}
      />
      <DeadlineField
        value={deadline}
        onChange={onDeadline}
        onInputKeyDown={onKey}
        wrapperClass="add-deadline-wrap"
        previewClass="add-hint dl-preview"
        hintWhenEmpty={ADD_HINT}
        placeholder="截止日期（可点 📅）"
      />
      <button className="add-btn" onClick={onAdd}>
        添加
      </button>
    </div>
  );
}