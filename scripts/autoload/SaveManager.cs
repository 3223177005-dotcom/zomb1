using Godot;

/// <summary>
/// 存档管理单例（骨架版本）。
/// </summary>
public partial class SaveManager : Node
{
    private const string SavePath = "user://savegame.json";

    public Godot.Collections.Dictionary Data { get; set; } = new();

    public void SaveGame()
    {
        var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
        if (file == null)
        {
            GD.PushError($"SaveManager: 无法写入存档（{SavePath}）");
            return;
        }
        file.StoreString(Json.Stringify(Data, "\t"));
        file.Close();
    }

    public bool LoadGame()
    {
        if (!FileAccess.FileExists(SavePath))
            return false;

        var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
        if (file == null)
            return false;

        var parsed = Json.ParseString(file.GetAsText());
        file.Close();

        if (parsed.VariantType == Variant.Type.Dictionary)
        {
            Data = parsed.AsGodotDictionary();
            return true;
        }
        return false;
    }

    public void ResetSave() => Data = new Godot.Collections.Dictionary();
}
