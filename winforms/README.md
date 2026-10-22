# 待办清单 · Windows 桌面版（C# WinForms）

原 HTML 单文件工具（`../待办清单.html`）的 Windows 原生桌面版。
目标框架 **.NET Framework 4.6**（Win10 1507 起全系自带），产物为**一个裸 exe，无任何运行时依赖**：任意 Win10/Win11 机器零安装直接双击运行（包括只有 4.7.2 / 4.6.2 的老机器，.NET Framework 向下兼容）。

## 功能对照

| 原 HTML 版 | 本版实现 |
|---|---|
| 勾选完成 / 删除线置灰 | ListView 原生勾选框 + 内容列删除线 |
| 截止日期自然语言解析（10-20 / 明天 / 下周三 / 月底 / +3d / 3天之后…） | `DeadlineParser.cs` 逐条规则移植，识别结果与原版一致 |
| 📅 自建日历弹层 + 快捷选项 | `CalendarPopup.cs`（MonthCalendar + 今天/明天/后天/本周五/下周三/月底 + 清除日期，失焦自动关） |
| 截止徽章着色（逾期红/今天橙/将到绿/无法识别灰） | 截止列同等着色规则 |
| 统计卡片（共/已完成/已逾期）+ 近 3 天到期提示 | 顶部统计卡片 + 标题下方日期行 |
| 按截止日期排序（开关）、重置勾选、清空重建、导出文本 | 工具栏全部保留，导出格式与原版一致 |
| 底部快速添加行（内容+负责人+截止+实时预览） | 完整保留 |
| 编辑弹窗（内容/负责人/截止/备注/预览/清除日期） | `EditItemForm.cs` 完整保留 |
| 数据写回 HTML 文件 / localStorage | 数据存 `%APPDATA%\TodoTool\data.json`（原子写，附 .bak） |

### 桌面版增强

- 系统托盘常驻：关闭/最小化即进托盘，双击托盘图标恢复；**开机自启动**（托盘菜单可开关，写 HKCU Run 键）
- 单实例防双开（避免两个进程互相覆盖数据文件）
- 双击行编辑、F2/Enter 编辑、Delete 删除、空格勾选、右键菜单（编辑/勾选/复制/删除）

## 系统兼容性

| 目标机情况 | 能否直接运行 |
|---|---|
| Win10 1507 ~ 1803（自带 4.6/4.6.2/4.7/4.7.1） | ✅ |
| Win10 1809/1903+、Win11（自带 4.7.2/4.8/4.8.1） | ✅ 向下兼容 |
| 检查命令：`reg query "HKLM\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" /v Release` | Release ≥ 393295（0x6004F，即装有 4.6+）即可运行 |
| 英文/精简系统（无 zh-CN 语言包） | ✅ 已加固，自动降级显示 |

## 数据文件与迁移

- 位置：`%APPDATA%\TodoTool\data.json`（状态栏显示完整路径）
- 格式与原 HTML 版内嵌 `todoData` JSON 块完全互通（`items` + `checked` + `sortByDeadline`）。
  想迁移旧数据：把原 HTML 里 `<script id="todoData">` 的 JSON 拷成 `data.json` 即可（桌面版同时兼容 `checked` 字典与内联 `done` 字段，两者都写）。

## 编译

任意机器（Windows / macOS / Linux）装 [.NET SDK](https://dotnet.microsoft.com/download) 8.0+ 后：

```bash
cd winforms
dotnet build -c Release
```

产物：`bin/Release/net46/TodoTool.exe`（实测 **54KB**，无 pdb、无依赖，直接拷到 Win10 双击运行）。

- Windows 上也可用 Visual Studio 2019/2022 直接打开 `TodoTool.csproj` 生成。
- 项目引用 `Microsoft.NETFramework.ReferenceAssemblies`（仅编译期，`PrivateAssets=all`），用于在非 Windows 平台交叉编译 net46；Windows 上已装 targeting pack 时无副作用。

## 直接使用已编译好的成品

`release/TodoTool.exe`（54KB）即编译产物，拷到任意 Win10 1903+ 机器双击即可运行，无需安装任何运行时。`TodoTool.exe.config` 一并拷走（普通 exe 配置文件，169B）。

## 目录结构

```
winforms/
    ├── TodoTool.csproj     # net46 SDK-style 项目（Win10 全系兼容）
├── app.manifest        # DPI 感知 + Win10/11 兼容声明
├── App.ico             # 应用图标（多尺寸）
├── Program.cs          # 入口 + 单实例互斥
├── MainForm.cs         # 主窗体：布局/渲染/事件/托盘/导出
├── EditItemForm.cs     # 新建/编辑弹窗
├── CalendarPopup.cs    # 日历弹层
├── DeadlineParser.cs   # 自然语言日期解析（移植自原版 parseDeadline）
├── TodoItem.cs         # 数据模型
├── TodoStore.cs        # %APPDATA% JSON 持久化（原子写）
├── MiniJson.cs         # 零依赖 JSON 读写
├── release/            # 已编译成品（TodoTool.exe 54KB，拷到 Win10 直接运行）
└── .gitignore          # 忽略 bin/obj
```

> AI生成
