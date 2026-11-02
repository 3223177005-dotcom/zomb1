using Godot;

/// <summary>
/// 游戏状态管理单例（GameManager）。
/// </summary>
public partial class GameManager : Node
{
    public enum GameState { Boot, MainMenu, InGame, Paused, GameOver, Extracted }

    public GameState CurrentState { get; private set; } = GameState.Boot;

    public override void _Ready()
    {
        ChangeState(GameState.MainMenu);
    }

    public void ChangeState(GameState newState)
    {
        CurrentState = newState;
        EventBus.Instance.EmitSignal(EventBus.SignalName.GameStateChanged, (int)newState);
    }

    public void StartGame() => ChangeState(GameState.InGame);
    public void GameOver() => ChangeState(GameState.GameOver);
    public void Extracted() => ChangeState(GameState.Extracted);

    public void PauseGame()
    {
        if (CurrentState == GameState.InGame)
        {
            ChangeState(GameState.Paused);
            EventBus.Instance.EmitSignal(EventBus.SignalName.GamePaused, true);
        }
    }

    public void ResumeGame()
    {
        if (CurrentState == GameState.Paused)
        {
            ChangeState(GameState.InGame);
            EventBus.Instance.EmitSignal(EventBus.SignalName.GamePaused, false);
        }
    }
}
