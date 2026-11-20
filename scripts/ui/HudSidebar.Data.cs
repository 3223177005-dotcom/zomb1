using Godot;
using System.Collections.Generic;

/// <summary>
/// <see cref="HudSidebar"/> 的**数据入口**：把游戏状态喂给四角 HUD 的各个小控件。
///
/// ★ 减字约定：这里只写**图标 / 条 / 极短数值**（`100%`、`18.6kg`、`10 / 30`），
///   绝不写物品全名 —— 具体是什么，看图标与悬停提示。
///
/// 为什么单独一个文件：搭建在 `HudSidebar.cs`，喂数据在这里，
/// 两个文件各自守在 400 行以内（AGENTS 规则 1）；也让 <see cref="Hud"/> 只剩"取数据 + 调用"。
/// </summary>
public partial class HudSidebar
{
    private const int MaxVisibleMessages = 3;

    /// <summary>负重超过上限的这个比例就换琥珀色警示。</summary>
    private const float WeightWarnRatio = 0.75f;

    /// <summary>空槽时槽位剪影图标的透明度。</summary>
    private const float EmptyIconAlpha = 0.30f;

    private readonly List<(string text, float time)> _messages = new();

    /// <summary>各条上一次用的填充贴图（只在换档时重建 StyleBox，避免每帧分配）。</summary>
    private string _armorFill = "";
    private string _helmetFill = "";
    private string _weightFill = "bar_armor.png";

    // ---------- 生存状态 ----------

    /// <summary>生命 / 耐力：数值压进 / 贴着条显示。</summary>
    public void ApplyVitals(float health, float healthMax, float stamina, float staminaMax)
    {
        _healthBar.MaxValue = Mathf.Max(1.0f, healthMax);
        _healthBar.Value = Mathf.Clamp(health, 0.0f, healthMax);
        _healthText.Text = $"{Mathf.Max(0.0f, health):0} / {healthMax:0}";

        _staminaBar.MaxValue = Mathf.Max(1.0f, staminaMax);
        _staminaBar.Value = Mathf.Clamp(stamina, 0.0f, staminaMax);
        _staminaText.Text = $"{Mathf.Max(0.0f, stamina):0}";
    }

    /// <summary>护甲 / 头盔：图标 + 耐久条 + 百分比（**不写物品名**）。</summary>
    public void ApplyArmor(ItemStack body, ItemStack helmet)
    {
        ApplyArmorRow(body, _armorIcon, _armorBar, _armorText, "icon_armor", ref _armorFill);
        ApplyArmorRow(helmet, _helmetIcon, _helmetBar, _helmetText, "icon_helmet", ref _helmetFill);
        FitVitalsBox();
    }

    private static void ApplyArmorRow(ItemStack item, TextureRect icon, ProgressBar bar, Label value,
        string kindIcon, ref string cachedFill)
    {
        if (item?.Data == null)
        {
            icon.Texture = UiStyle.LoadUiIcon(kindIcon);
            icon.Modulate = new Color(1.0f, 1.0f, 1.0f, EmptyIconAlpha);
            bar.Visible = false;
            bar.Value = 0.0;
            value.Text = "—";
            value.AddThemeColorOverride("font_color", UiStyle.EmptySlotColor);
            return;
        }

        var data = item.Data;
        icon.Texture = data.Icon;
        icon.Modulate = Colors.White;

        float max = Mathf.Max(1.0f, data.MaxDurability);
        float ratio = data.HasDurability ? Mathf.Clamp(item.Durability / max, 0.0f, 1.0f) : 1.0f;
        bool broken = data.HasDurability && ratio <= 0.0f;
        var color = UiStyle.DurabilityColor(ratio, broken);

        bar.Visible = data.HasDurability;
        if (data.HasDurability)
        {
            bar.MaxValue = max;
            bar.Value = item.Durability;
        }

        // 只留百分比：破甲显示"破损"，没耐久概念的物品留空
        if (!data.HasDurability) value.Text = "";
        else value.Text = broken ? "破损" : $"{ratio * 100.0f:0}%";

        value.AddThemeColorOverride("font_color", color);

        string fill = UiStyle.DurabilityFill(ratio);
        if (cachedFill == fill) return;

        cachedFill = fill;
        UiStyle.StyleDurabilityBar(bar, ratio);
    }

    /// <summary>负重：只写 `18.6kg`，上限交给条的占比表达。</summary>
    public void ApplyWeight(float weight, float cap)
    {
        float limit = Mathf.Max(0.01f, cap);

        _weightText.Text = $"{weight:0.0}kg";
        _weightBar.MaxValue = limit;
        _weightBar.Value = Mathf.Min(weight, limit);

        string fill = weight > limit
            ? "bar_hp.png"
            : (weight > limit * WeightWarnRatio ? "bar_amber.png" : "bar_armor.png");

        if (_weightFill == fill) return;

        _weightFill = fill;
        UiStyle.StylePixelBar(_weightBar, fill);
    }

    // ---------- 武器与弹药（不印武器名） ----------

    /// <summary>
    /// 右下武器盒：武器贴图 + 大号弹匣数 + `/ 备弹` + 开火模式角标。
    /// 近战 / 空手不显示弹药，只显示"近战"/"—"。
    /// </summary>
    public void ApplyWeapon(ItemStack item, bool ranged, string modeText,
        int magazine, int reserve, bool infinite)
    {
        var data = item?.Data;
        _weaponIcon.Texture = data?.Icon;
        _weaponIcon.Visible = data?.Icon != null;

        if (data == null)
        {
            _ammoText.Text = "—";
            _ammoTotal.Text = "";
            _modeTag.Text = "";
            return;
        }

        if (!ranged)
        {
            _ammoText.Text = "近战";
            _ammoTotal.Text = "";
            _modeTag.Text = "";
            return;
        }

        _ammoText.Text = magazine.ToString();
        _ammoTotal.Text = "/ " + (infinite ? "∞" : reserve.ToString());
        _modeTag.Text = modeText;
    }

    // ---------- 底部快捷栏（只有格号 + 图标） ----------

    /// <summary>三个快捷槽：图标 + 格号；激活槽换样式。**不印物品名**。</summary>
    public void ApplyQuickSlots(EquipmentSlots slots, int activeSlot)
    {
        for (int i = 0; i < _quickSlots.Length; i++)
        {
            var data = slots.Get(i)?.Data;
            bool active = i == activeSlot;

            _quickIcons[i].Texture = data?.Icon;
            _quickIcons[i].Visible = data?.Icon != null;
            _quickKeys[i].AddThemeColorOverride("font_color",
                active ? UiStyle.AmberHeader : UiStyle.CaptionColor);

            if (_quickActive[i] == active) continue;

            _quickActive[i] = active;
            _quickSlots[i].AddThemeStyleboxOverride("panel",
                active ? UiStyle.SlotPressed() : UiStyle.SlotNormal());
        }
    }

    // ---------- 右上浮窗（消息 / 查看） ----------

    /// <summary>中键查看结果；传空字符串即收起浮窗。</summary>
    public void SetInspect(string bbText)
    {
        if (_inspectText == null) return;

        _inspectText.Text = bbText ?? "";
        _inspectText.Visible = !string.IsNullOrEmpty(bbText);
        RefreshFeed();
    }

    /// <summary>添加一条信息，duration 秒后自动消失。</summary>
    public void AddMessage(string text, float duration = 3.0f)
    {
        _messages.Add((text, duration));
        RefreshMessages();
    }

    /// <summary>推进消息倒计时（每帧由 Hud 调一次）。</summary>
    public void TickMessages(float delta)
    {
        bool changed = false;
        for (int i = _messages.Count - 1; i >= 0; i--)
        {
            var (text, time) = _messages[i];
            time -= delta;
            if (time <= 0.0f)
            {
                _messages.RemoveAt(i);
                changed = true;
            }
            else
            {
                _messages[i] = (text, time);
            }
        }
        if (changed) RefreshMessages();
    }

    private void RefreshMessages()
    {
        if (_messageText == null) return;

        var sb = new System.Text.StringBuilder();
        int start = Mathf.Max(0, _messages.Count - MaxVisibleMessages);
        for (int i = start; i < _messages.Count; i++)
        {
            if (i > start) sb.Append('\n');
            sb.Append(UiStyle.BbText("▸ ", UiStyle.CaptionColor)).Append(_messages[i].text);
        }

        _messageText.Text = sb.ToString();
        _messageText.Visible = _messages.Count > 0;
        RefreshFeed();
    }

    /// <summary>两块都没内容 → 整块浮窗隐藏，不挡右上角视野。</summary>
    private void RefreshFeed()
    {
        if (_feedBox == null) return;
        _feedBox.Visible = (_messageText?.Visible ?? false) || (_inspectText?.Visible ?? false);
    }
}
