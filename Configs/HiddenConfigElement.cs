using Terraria.ModLoader.Config.UI;

namespace ModFolder.Configs;

public class HiddenConfigElement : ConfigElement {
    public override void OnBind() {
        base.OnBind();
        Height.Set(0, 0);
    }
}
