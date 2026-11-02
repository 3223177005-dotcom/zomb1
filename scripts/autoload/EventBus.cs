using Godot;

/// <summary>
/// 全局信号总线（EventBus），用于跨系统通信，避免节点之间的强耦合。
/// </summary>
public partial class EventBus : Node
{
    public static EventBus Instance { get; private set; }

    public override void _EnterTree()
    {
        Instance = this;
    }

    // ---------- 游戏流程 ----------
    [Signal] public delegate void GameStateChangedEventHandler(int newState);
    [Signal] public delegate void LevelLoadedEventHandler(string levelName);
    [Signal] public delegate void GamePausedEventHandler(bool paused);

    // ---------- 玩家 ----------
    [Signal] public delegate void PlayerHealthChangedEventHandler(float current, float maximum);
    [Signal] public delegate void PlayerStaminaChangedEventHandler(float current, float maximum);
    [Signal] public delegate void PlayerDiedEventHandler();

    /// <summary>当前手持武器信息变化（切武器槽 / 换开火模式时发出）。
    /// isRanged = false 表示近战或空手：此时 HUD **不显示开火模式，也不显示弹药**。</summary>
    [Signal] public delegate void WeaponInfoChangedEventHandler(string weaponName, int fireMode, bool isRanged);

    // ---------- UI 面板 ----------
    /// <summary>UI 面板开合。打开时角色应屏蔽开火，避免点 UI 的同时误射。</summary>
    [Signal] public delegate void UiPanelToggledEventHandler(bool open);

    // ---------- 物品 / 背包 ----------
    [Signal] public delegate void ItemPickedUpEventHandler(string itemId, int amount);
    [Signal] public delegate void InventoryChangedEventHandler();

    // ---------- 敌人 / 声音 ----------
    [Signal] public delegate void NoiseEmittedEventHandler(Vector2 position, float radius);

    // ---------- 撤离 / 结算 ----------
    [Signal] public delegate void ExtractionStartedEventHandler();
    [Signal] public delegate void ExtractionCompletedEventHandler();
}
