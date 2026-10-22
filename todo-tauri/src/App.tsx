import { useEffect, useMemo, useRef, useState } from "react";
import type { TodoData, TodoItemData } from "./types";
import { applyDeadline, deadlineInfo, normalizeData, resolveISO } from "./utils/date";
import { getDataPathCmd, loadDataCmd, revealPathCmd, saveDataCmd, saveExportCmd } from "./utils/tauri";
import TodoInput from "./components/TodoInput";
import TodoItem from "./components/TodoItem";
import EditModal from "./components/EditModal";
import ConfirmModal from "./components/ConfirmModal";

interface ConfirmState {
  title: string;
  message: string;
  confirmText?: string;
  danger?: boolean;
  onOk: () => void;
}

function todayString(): string {
  const t = new Date();
  return (
    "今天是 " +
    t.toLocaleDateString("zh-CN", { year: "numeric", month: "long", day: "numeric", weekday: "long" })
  );
}

export default function App() {
  const [data, setData] = useState<TodoData>({ updatedAt: 0, items: [], checked: {}, sortByDeadline: false });
  const [dataPath, setDataPath] = useState("");
  const [loaded, setLoaded] = useState(false);
  const [saveMsg, setSaveMsg] = useState<string | null>(null);
  const [editIdx, setEditIdx] = useState(-1);
  const [delIdx, setDelIdx] = useState(-1);
  const [confirm, setConfirm] = useState<ConfirmState | null>(null);

  const [addText, setAddText] = useState("");
  const [addOwner, setAddOwner] = useState("");
  const [addDeadline, setAddDeadline] = useState("");

  const inputRef = useRef<HTMLInputElement>(null);
  const saveTimer = useRef<number | null>(null);
  const toastTimer = useRef<number | null>(null);

  const showToast = (msg: string) => {
    setSaveMsg(msg);
    if (toastTimer.current) window.clearTimeout(toastTimer.current);
    toastTimer.current = window.setTimeout(() => setSaveMsg(null), 1600);
  };

  /* 初始加载 */
  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const raw = await loadDataCmd();
        const d = normalizeData(raw);
        if (!cancelled) setData(d);
      } catch (e) {
        const d = normalizeData(null);
        if (!cancelled) setData(d);
        showToast("读取数据失败，已用默认数据");
      }
      getDataPathCmd()
        .then(setDataPath)
        .catch(() => {});
      if (!cancelled) setLoaded(true);
    })();
    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  /* 数据变化 → 防抖保存 */
  useEffect(() => {
    if (!loaded || data.updatedAt === 0) return;
    if (saveTimer.current) window.clearTimeout(saveTimer.current);
    saveTimer.current = window.setTimeout(() => {
      saveDataCmd(data)
        .then(() => showToast("已保存"))
        .catch((e) => showToast("保存失败：" + e));
    }, 400);
  }, [data, loaded]);

  /* ESC 关闭弹窗 */
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key !== "Escape") return;
      if (editIdx >= 0) setEditIdx(-1);
      else if (delIdx >= 0) setDelIdx(-1);
      else if (confirm) setConfirm(null);
    };
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, [editIdx, delIdx, confirm]);

  const rows = useMemo(() => {
    const r = data.items.map((item, idx) => ({ item, idx }));
    if (data.sortByDeadline) {
      const val = (it: TodoItemData) => resolveISO(it) || "9999-12-31";
      r.sort((a, b) => {
        const av = val(a.item);
        const bv = val(b.item);
        if (av !== bv) return av < bv ? -1 : 1;
        return a.idx - b.idx;
      });
    }
    return r;
  }, [data]);

  const doneCount = rows.filter(({ item }) => !!data.checked[item.id]).length;
  const lateCount = rows.filter(({ item }) => {
    if (data.checked[item.id]) return false;
    const iso = resolveISO(item);
    const dl = iso ? deadlineInfo(iso) : null;
    return !!dl && dl.diff < 0;
  }).length;

  const soonCount = useMemo(() => {
    let n = 0;
    data.items.forEach((item) => {
      if (data.checked[item.id]) return;
      const iso = resolveISO(item);
      if (!iso) return;
      const info = deadlineInfo(iso);
      if (info && info.diff >= 0 && info.diff <= 3) n++;
    });
    return n;
  }, [data]);

  const metaLine = soonCount ? `${todayString()} · 未来 3 天内到期 ${soonCount} 项` : `${todayString()} · 近 3 天无到期项`;

  const toggle = (id: number) => {
    setData((d) => {
      const checked = { ...d.checked };
      if (checked[id]) delete checked[id];
      else checked[id] = true;
      return { ...d, checked };
    });
  };

  const add = () => {
    const text = addText.trim();
    if (!text) {
      inputRef.current?.focus();
      if (addDeadline.trim()) showToast("⚠ 请先输入待办内容");
      return;
    }
    setData((d) => {
      const maxId = d.items.reduce((m, i) => Math.max(m, i.id), 0);
      const it: TodoItemData = { id: maxId + 1, text, note: "", owner: addOwner.trim(), deadline: "", deadlineText: "" };
      applyDeadline(it, addDeadline);
      return { ...d, items: [...d.items, it] };
    });
    setAddText("");
    setAddOwner("");
    setAddDeadline("");
    inputRef.current?.focus();
  };

  const saveEdit = (item: TodoItemData) => {
    setData((d) => {
      const items = d.items.map((i) => (i.id === item.id ? { ...item } : i));
      return { ...d, items };
    });
    setEditIdx(-1);
  };

  const confirmDelete = () => {
    if (delIdx < 0) return;
    setData((d) => {
      const idx = delIdx;
      const items = d.items.filter((_, i) => i !== idx);
      const checked = { ...d.checked };
      delete checked[d.items[idx].id];
      return { ...d, items, checked };
    });
    setDelIdx(-1);
  };

  const resetChecks = () => {
    setConfirm({
      title: "确认重置",
      message: "重置所有勾选状态？",
      confirmText: "重置",
      onOk: () => setData((d) => ({ ...d, checked: {} })),
    });
  };

  const clearAll = () => {
    setConfirm({
      title: "确认清空",
      message: "清空所有数据并恢复默认？此操作不可撤销！",
      confirmText: "清空重建",
      danger: true,
      onOk: () => setData(() => normalizeData(null)),
    });
  };

  const finishConfirm = () => {
    if (confirm) {
      confirm.onOk();
      setConfirm(null);
    }
  };

  const exportData = async () => {
    const lines = rows.map(({ item }, i) => {
      const chk = data.checked[item.id] ? "✓" : " ";
      const parts = [`${i + 1}. [${chk}] ${item.text}`];
      if (item.owner) parts.push(`负责人: ${item.owner}`);
      const iso = resolveISO(item);
      if (iso) {
        const dl = deadlineInfo(iso);
        parts.push(`截止: ${iso}${dl && dl.diff < 0 ? "（已逾期）" : ""}`);
      } else if (item.deadlineText) {
        parts.push(`截止: ${item.deadlineText}`);
      }
      if (item.note) parts.push(`备注: ${item.note}`);
      return parts.join("  |  ");
    });
    const head = `制卡技术待办清单  导出时间：${new Date().toLocaleString("zh-CN")}\n共 ${lines.length} 项\n\n`;
    try {
      const path = await saveExportCmd(head + lines.join("\n"));
      await revealPathCmd(path);
      showToast("✅ 已导出，已在文件夹中显示");
    } catch (e) {
      showToast("导出失败：" + e);
    }
  };

  const editingItem = editIdx >= 0 ? data.items[editIdx] : null;
  const deletingItem = delIdx >= 0 ? data.items[delIdx] : null;

  return (
    <div className="container">
      <h1>待办清单</h1>
      <p className="meta">{metaLine}</p>

      <div className="filebar">
        <span className="fb-status ok" title="每次改动的防抖保存到本地 JSON（自动原子写入，含 .bak 备份）">
          ✅ 数据自动保存：{dataPath || "…"}
        </span>
      </div>

      <div className="stats">
        <div className="stat">
          共 <b>{data.items.length}</b> 项
        </div>
        <div className="stat done">
          已完成 <b>{doneCount}</b> 项
        </div>
        <div className="stat late">
          已逾期 <b>{lateCount}</b> 项
        </div>
      </div>

      <div className="toolbar">
        <button className={"secondary" + (data.sortByDeadline ? " active" : "")} onClick={() => setData((d) => ({ ...d, sortByDeadline: !d.sortByDeadline }))}>
          {data.sortByDeadline ? "✓ 按截止日期排序" : "按截止日期排序"}
        </button>
        <button className="secondary" onClick={resetChecks}>
          重置勾选
        </button>
        <button className="secondary" onClick={exportData}>
          导出数据
        </button>
        <button className="danger" onClick={clearAll}>
          清空重建
        </button>
      </div>

      <div className="list">
        {data.items.length === 0 ? (
          <div className="empty">暂无待办项，在下方输入框添加一条试试 ✍️</div>
        ) : (
          rows.map(({ item, idx }, order) => (
            <TodoItem
              key={item.id}
              item={item}
              order={order + 1}
              checked={!!data.checked[item.id]}
              onToggle={() => toggle(item.id)}
              onEdit={() => setEditIdx(idx)}
              onDelete={() => setDelIdx(idx)}
            />
          ))
        )}
      </div>

      <TodoInput
        text={addText}
        owner={addOwner}
        deadline={addDeadline}
        onText={setAddText}
        onOwner={setAddOwner}
        onDeadline={setAddDeadline}
        onAdd={add}
        inputRef={inputRef}
      />

      <div className="footer">
        <span className={"save-indicator" + (saveMsg ? " show" : "")}>{saveMsg || "已保存"}</span>
        <span>数据保存在本地 data.json · 改动自动写入 · 原子写 + .bak 备份</span>
      </div>

      {editingItem && (
        <EditModal key={editingItem.id} item={editingItem} onSave={saveEdit} onCancel={() => setEditIdx(-1)} />
      )}

      {deletingItem && (
        <ConfirmModal
          title="确认删除"
          message={`删除「${deletingItem.text}」？`}
          confirmText="删除"
          danger
          onConfirm={confirmDelete}
          onCancel={() => setDelIdx(-1)}
        />
      )}

      {confirm && (
        <ConfirmModal
          title={confirm.title}
          message={confirm.message}
          confirmText={confirm.confirmText}
          danger={confirm.danger}
          onConfirm={finishConfirm}
          onCancel={() => setConfirm(null)}
        />
      )}
    </div>
  );
}