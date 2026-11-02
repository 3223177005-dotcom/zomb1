using System.Collections.Generic;

/// <summary>
/// 一个「身上某处放着的配件」引用：哪一叠、在哪个口袋里、从哪个口袋翻出来的。
/// </summary>
public readonly struct AttachmentRef
{
    public AttachmentRef(ItemStack stack, Inventory pocket, string where, string note)
    {
        Stack = stack;
        Pocket = pocket;
        Where = where;
        Note = note;
    }

    /// <summary>那一叠配件。</summary>
    public ItemStack Stack { get; }

    /// <summary>它实际所在的口袋（把东西拖到这一行走的就是这个口袋）。</summary>
    public Inventory Pocket { get; }

    /// <summary>最外层口袋的名字（例如「基础口袋」「背包」）。</summary>
    public string Where { get; }

    /// <summary>嵌套时的补充说明（例如「在 背包 里」）。</summary>
    public string Note { get; }
}

/// <summary>
/// **配件检索**：把一个口袋（含它里面的容器，递归）里所有配件类物品找出来。
///
/// 改装面板用这个来出"身上有哪些配件"的清单 —— 不再把整个背包摊成格子，
/// 而是直接把 <see cref="ItemData.AttachmentStats"/> 非空的物品挑出来。
///
/// 纯 C#，不依赖 UI，方便自测。
/// </summary>
public static class AttachmentFinder
{
    /// <summary>从 <paramref name="pocket"/> 开始收集配件（会往容器里继续找）。</summary>
    public static List<AttachmentRef> Collect(Inventory pocket, string where)
    {
        var found = new List<AttachmentRef>();
        Walk(pocket, where, "", 0, new HashSet<Inventory>(), found);
        return found;
    }

    /// <summary>某口袋里（含嵌套）有几件配件。</summary>
    public static int Count(Inventory pocket) => pocket == null ? 0 : Collect(pocket, "").Count;

    private static void Walk(Inventory pocket, string where, string note, int depth,
                             HashSet<Inventory> seen, List<AttachmentRef> found)
    {
        if (pocket == null || depth > Inventory.MaxNestingDepth) return;
        if (!seen.Add(pocket)) return;   // 防环（理论上不会，但别递归炸了）

        foreach (var stack in pocket.Stacks)
        {
            if (stack?.Data == null) continue;

            if (stack.Data.AttachmentStats != null)
            {
                found.Add(new AttachmentRef(stack, pocket, where, note));
                continue;
            }

            if (stack.IsContainer && stack.Contents != null)
            {
                string inner = string.IsNullOrEmpty(note) ? $"在 {stack.Data.Name} 里" : note;
                Walk(stack.Contents, where, inner, depth + 1, seen, found);
            }
        }
    }
}
