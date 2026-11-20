using Godot;
public partial class ZsArtProbe : Node
{
    public override void _Ready()
    {
        GD.Print("gun svd -> ", ZsArt.GunId("svd"), " icon ", ZsArt.GunIcon("svd"));
        GD.Print("gun dagger -> ", ZsArt.GunId("dagger"));
        GD.Print("silencer art -> ", ZsArt.ModArt("silencer"), " icon ", ZsArt.ModIcon("silencer"));
        GD.Print("extmag art -> ", ZsArt.ModArt("extmag"), " icon ", ZsArt.ModIcon("extmag"));
        GD.Print("tacgrip art -> ", ZsArt.ModArt("tacgrip"), " icon ", ZsArt.ModIcon("tacgrip"));
        GD.Print("svd icon exists -> ", ResourceLoader.Exists(ZsArt.GunIcon("svd")));
        GetTree().Quit();
    }
}
