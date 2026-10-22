export interface TodoItemData {
  id: number;
  text: string;
  note: string;
  owner: string;
  deadline: string;
  deadlineText: string;
}

export interface TodoData {
  updatedAt: number;
  items: TodoItemData[];
  checked: Record<string, boolean>;
  sortByDeadline: boolean;
}

export const DEFAULT_ITEMS: TodoItemData[] = [
  {
    id: 1,
    text: "示例：整理接口文档，同步确认字段",
    note: "",
    owner: "张三",
    deadline: "",
    deadlineText: "后天",
  },
];

export function emptyData(): TodoData {
  return { updatedAt: 0, items: [], checked: {}, sortByDeadline: false };
}

export function cloneData(d: TodoData): TodoData {
  return {
    updatedAt: d.updatedAt,
    items: d.items.map((i) => ({ ...i })),
    checked: { ...d.checked },
    sortByDeadline: d.sortByDeadline,
  };
}