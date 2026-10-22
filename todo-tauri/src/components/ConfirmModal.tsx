interface ConfirmModalProps {
  title: string;
  message: string;
  confirmText?: string;
  danger?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}

export default function ConfirmModal({
  title,
  message,
  confirmText = "确定",
  danger = false,
  onConfirm,
  onCancel,
}: ConfirmModalProps) {
  return (
    <div className="modal-overlay show" onMouseDown={(e) => e.target === e.currentTarget && onCancel()}>
      <div className="modal">
        <h3>{title}</h3>
        <p style={{ fontSize: 15, color: "#555", margin: "12px 0" }}>{message}</p>
        <div className="modal-btns">
          <button onClick={onCancel}>取消</button>
          <button
            className="primary"
            style={danger ? { background: "#c62828", borderColor: "#c62828" } : undefined}
            onClick={onConfirm}
          >
            {confirmText}
          </button>
        </div>
      </div>
    </div>
  );
}