using Godot;

/// <summary>
/// 场景切换管理单例。
/// </summary>
public partial class SceneManager : Node
{
    public static SceneManager Instance { get; private set; }

    public override void _EnterTree()
    {
        Instance = this;
    }

    public void ChangeScene(string path) => GetTree().ChangeSceneToFile(path);

    public void ReloadCurrentScene() => GetTree().ReloadCurrentScene();

    public void GotoMainMenu() => ChangeScene("res://scenes/main_menu.tscn");

    public void GotoGame() => ChangeScene("res://scenes/game.tscn");
}
