using System;
using System.Collections.Generic;

/// <summary>
/// **面板栈**：每次按 Esc 关闭**最新打开**的那一个面板。
///
/// 用法：
/// - 面板打开时 <c>PanelStack.Set(closeSelf, true)</c>，关闭时 <c>PanelStack.Set(closeSelf, false)</c>
/// - 全局只在 <see cref="EquipmentPanel"/> 里挂一个 Esc 处理器，调 <see cref="CloseTop"/>
///
/// 这样「Tab 装备面板 → 右键改装面板 → Esc」会先关改装，再按 Esc 才关装备面板。
/// </summary>
public static class PanelStack
{
    private static readonly List<Action> Layers = new();

    /// <summary>面板状态变化时调用：打开就入栈，关闭就出栈。</summary>
    public static void Set(Action closeSelf, bool open)
    {
        if (closeSelf == null) return;

        if (open)
        {
            // 同一个面板重复打开时不要重复入栈
            if (Layers.Contains(closeSelf)) return;
            Layers.Add(closeSelf);
        }
        else
        {
            Layers.Remove(closeSelf);
        }
    }

    /// <summary>关掉**最新打开**的那一层。没有可关的返回 false（交给别人处理 Esc）。</summary>
    public static bool CloseTop()
    {
        for (int i = Layers.Count - 1; i >= 0; i--)
        {
            var close = Layers[i];
            if (close == null)
            {
                Layers.RemoveAt(i);
                continue;
            }

            close();          // 面板自己的 SetOpen(false) 会把它从栈里移除
            return true;
        }
        return false;
    }

    /// <summary>当前打开了几层（调试用）。</summary>
    public static int Count => Layers.Count;

    /// <summary>清空（切场景时用）。</summary>
    public static void Clear() => Layers.Clear();
}
