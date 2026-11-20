using Godot;

/// <summary>主菜单。</summary>
public partial class MainMenu : Control
{
    public override void _Ready()
    {
        var start = GetNode<Button>("Center/StartButton");
        var quit = GetNode<Button>("Center/QuitButton");
        var title = GetNodeOrNull<Label>("Center/Title");
        title?.AddThemeColorOverride("font_color", UiStyle.AmberHeader);
        UiStyle.StylePixelButton(start);
        UiStyle.StylePixelButton(quit);
        start.Pressed += OnStartPressed;
        quit.Pressed += OnQuitPressed;
    }

    private void OnStartPressed() => SceneManager.Instance.GotoGame();

    private void OnQuitPressed() => GetTree().Quit();
}
