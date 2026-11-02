using Godot;

/// <summary>主菜单。</summary>
public partial class MainMenu : Control
{
    public override void _Ready()
    {
        GetNode<Button>("Center/StartButton").Pressed += OnStartPressed;
        GetNode<Button>("Center/QuitButton").Pressed += OnQuitPressed;
    }

    private void OnStartPressed() => SceneManager.Instance.GotoGame();

    private void OnQuitPressed() => GetTree().Quit();
}
