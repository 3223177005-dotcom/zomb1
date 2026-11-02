using Godot;

/// <summary>
/// 音频管理单例（骨架版本）。
/// </summary>
public partial class AudioManager : Node
{
    public float MasterVolume { get; set; } = 1.0f;
    public float SfxVolume { get; set; } = 1.0f;
    public float MusicVolume { get; set; } = 1.0f;

    public void PlaySfx(AudioStream stream, float volumeDb = 0.0f)
    {
        var player = new AudioStreamPlayer
        {
            Stream = stream,
            VolumeDb = volumeDb
        };
        AddChild(player);
        player.Finished += player.QueueFree;
        player.Play();
    }
}
