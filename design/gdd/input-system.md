---
status: reverse-documented
source: Assets/Scripts/InputSystem/
date: 2026-07-28
verified-by: oy
---

# 输入系统 — 设计文档

> **⚠️ 反向文档**: 从现有实现反向生成。

---

## 1. 架构

```
GameInput (静态类)
  ├── Action 枚举 (33 个动作)
  │     ├── 移动: MoveLeft/Right/Up/Down
  │     ├── 战斗: Jump, Dash, Attack, CounterAttack, PlatformDrop
  │     ├── 技能: SkillSlot1-5, DomainExpansion
  │     ├── 面板: ToggleCharacter/Skill/Settings/Quest
  │     └── 通用: Interact, Escape
  │
  ├── 键位绑定: Dictionary<Action, KeyCode>
  ├── 持久化: PlayerPrefs ("GameInputBindings")
  └── UI_KeyRebind: MonoBehaviour — 重绑UI界面
```

---

## 2. 输入阻塞

```
GameInput.IsGameBlocked:
  true when:
    ├── UI_Chat.IsChatFocused
    └── ModalStack.IsAnyModalOpen

所有 GetKeyDown/GetKey/GetKeyUp → 先检查 IsGameBlocked → return false

例外 (不受阻塞):
  Escape, Interact, ToggleCharacterPanel, ToggleSkillPanel,
  ToggleSettingsPanel, ToggleQuestPanel
  → 即使面板打开也能关闭/切换

Horizontal/Vertical 轴向:
  同时检查 MoveLeft/Right/Up/Down → -1/0/1
  也受 IsGameBlocked 阻塞
```

---

## 3. 键位重绑

```
GameInput.SetBinding(action, key):
  → s_bindings[action] = key
  → SaveBindings() → PlayerPrefs
  → OnBindingsChanged 事件

ResetToDefaults():
  → 恢复预设键位
  → Save + 事件

UI_KeyRebind:
  └── 每个可重绑按键的 UI 交互 (等待按键→设置→刷新显示)
```

## 4. 持久化

```
存储: PlayerPrefs
格式: "GameInputBindings" = "Jump:Space;Dash:LeftShift;Attack:J;..."
解析: 启动时 LoadBindings() → Split(';') → Split(':') → Enum.Parse
```

---

## 版本历史

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-07-28 | Claude (reverse-doc) | 初始反向文档 |
