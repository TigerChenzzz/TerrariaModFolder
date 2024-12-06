using ReLogic.Content;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;

namespace ModFolder.UI.Base;

internal class UIImageWithVisibility(Asset<Texture2D> texture) : UIImage(texture) {
    public float Visibility { get; set; } = 1;

    public override void DrawSelf(SpriteBatch spriteBatch) {
        if (Visibility <= 0) {
            return;
        }
        if (Visibility >= 1) {
            base.DrawSelf(spriteBatch);
            return;
        }
        var originColor = Color;
        Color *= Visibility;
        base.DrawSelf(spriteBatch);
        Color = originColor;
    }
}
