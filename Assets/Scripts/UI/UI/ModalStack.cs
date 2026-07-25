using System;
using System.Collections.Generic;

// 模态栈——统一管理全屏 UI 的打开/关闭/输入阻塞。
// 所有全屏面板（角色/技能/设置/任务/商店/聊天/死亡）通过 Push/Pop 入栈出栈，
// GameInput.IsGameBlocked 统一查 IsAnyModalOpen，Escape 优先关闭栈顶。
public static class ModalStack
{
    private static readonly Stack<string> stack = new();

    public static bool IsAnyModalOpen => stack.Count > 0;

    // 栈顶模态 ID，null 表示无模态
    public static string Top => stack.Count > 0 ? stack.Peek() : null;

    // 栈变化事件（供 GameInput 等监听刷新 IsGameBlocked）
    public static event Action OnChanged;

    public static void Push(string id)
    {
        if (string.IsNullOrEmpty(id)) return;

        stack.Push(id);
        OnChanged?.Invoke();
    }

    // 仅当 id 在栈顶时才弹出（防止乱序 Pop 破坏栈状态）
    public static void Pop(string id)
    {
        if (stack.Count > 0 && stack.Peek() == id)
        {
            stack.Pop();
            OnChanged?.Invoke();
        }
    }

    // 移除栈中所有指定 id 的条目（面板切换时使用）
    public static void PopAll(string id)
    {
        var temp = new List<string>(stack);
        temp.RemoveAll(item => item == id);
        stack.Clear();
        for (int i = temp.Count - 1; i >= 0; i--)
            stack.Push(temp[i]);
        OnChanged?.Invoke();
    }

    // 清空栈（场景切换时使用）
    public static void Clear()
    {
        if (stack.Count > 0)
        {
            stack.Clear();
            OnChanged?.Invoke();
        }
    }
}
