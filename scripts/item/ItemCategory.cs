/// <summary>
/// 物品分类。新增分类时在此追加枚举项即可（数据层不依赖具体分类行为）。
/// 分类特有的数值/开关放在 ItemData.Stats 字典里，不需要改代码。
/// </summary>
public enum ItemCategory
{
    /// <summary>杂物（无特殊用途）。</summary>
    Misc,

    /// <summary>武器（引用 GunData 战斗属性）。</summary>
    Weapon,

    /// <summary>武器配件（安装到武器上的改装件）。</summary>
    Attachment,

    /// <summary>弹药。</summary>
    Ammo,

    /// <summary>医疗用品。</summary>
    Medical,

    /// <summary>食物 / 饮水。</summary>
    Food,

    /// <summary>护甲 / 头盔。</summary>
    Armor,

    /// <summary>
    /// 容器：可以装东西的物品（背包属于这一类）。
    /// 容器自身占格子（GridW×GridH）且**自身重量计入负重**；
    /// 能装多少由挂载的 <see cref="ContainerData"/> 决定（每个容器只有一个仓，仓是二维格子）。
    /// </summary>
    Container,

    /// <summary>战利品（主要用于变现）。</summary>
    Loot,

    /// <summary>钥匙 / 任务物品。</summary>
    Key,

    /// <summary>投掷物。</summary>
    Throwable,
}
