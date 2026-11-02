/// <summary>
/// **属性白名单** —— 配件能改的 7 个属性。
///
/// ★ 为什么要白名单：属性名拼错（`silnce`）**不会报错、也不会生效**，
/// 会造成"装了配件但没感觉"的隐形 bug。校验能把它在**加载时**抓住。
///
/// 说明：<see cref="HigherIsBetter"/> 只影响 UI 上色（变好显绿、变差显红），
/// **不影响计算** —— 公式永远是 <c>(base + Σaddend) × Πmultiplier</c>。
/// </summary>
public static class StatNames
{
    /// <summary>腰射散布（越小越好）。</summary>
    public const string Inaccuracy = "inaccuracy";

    /// <summary>瞄准散布（越小越好）。</summary>
    public const string AimInaccuracy = "aim_inaccuracy";

    /// <summary>瞄准收拢时间（越小越好）。</summary>
    public const string Ads = "ads";

    /// <summary>弹匣容量（**越大越好**）。</summary>
    public const string MagazineSize = "magazine_size";

    /// <summary>换弹时间（越小越好）。</summary>
    public const string ReloadTime = "reload_time";

    /// <summary>枪声音噪半径（越小越好）。</summary>
    public const string Silence = "silence";

    /// <summary>重量 kg（越小越好）。</summary>
    public const string Weight = "weight";

    /// <summary>全部合法属性名。</summary>
    public static readonly string[] All =
    {
        Inaccuracy, AimInaccuracy, Ads, MagazineSize, ReloadTime, Silence, Weight,
    };

    /// <summary>是不是合法属性名。</summary>
    public static bool IsValid(string statName)
    {
        if (string.IsNullOrWhiteSpace(statName)) return false;
        foreach (var name in All)
            if (name == statName) return true;
        return false;
    }

    /// <summary>这个属性是不是**越大越好**（给 UI 上色用）。</summary>
    public static bool HigherIsBetter(string statName) => statName == MagazineSize;

    /// <summary>中文名（UI 直接用）。</summary>
    public static string DisplayName(string statName) => statName switch
    {
        Inaccuracy => "腰射散布",
        AimInaccuracy => "瞄准散布",
        Ads => "瞄准时间",
        MagazineSize => "弹匣容量",
        ReloadTime => "换弹时间",
        Silence => "枪声音噪",
        Weight => "重量",
        _ => statName,
    };
}
