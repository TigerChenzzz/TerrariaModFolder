using ReLogic.Content;
using Terraria.ModLoader.Core;
using Terraria.UI;

namespace ModFolder.UI;

public class UIModStateCheckBox(LocalMod mod) : UIElement {
    Asset<Texture2D> DisableTexture { get; } = Textures.UI("CheckBox");
    Asset<Texture2D> EnableTexture { get; } = Textures.UI("CheckBox_Full");
    public Color Color { get; set; } = Color.White;
    public override void DrawSelf(SpriteBatch spriteBatch) {
        spriteBatch.Draw((mod.Enabled ? EnableTexture : DisableTexture).Value, GetDimensions().ToRectangle(), Color);
    }
}
