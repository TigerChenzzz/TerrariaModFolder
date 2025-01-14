using ReLogic.Content;
using Terraria.GameContent.UI.Elements;

namespace ModFolder.UI.Base;

public class UIImageWithVisibility(Asset<Texture2D> texture) : UIImage(texture) {
    private float _visibility = 1;
    public float Visibility {
        get => _visibility;
        set {
            if (_visibility == value) {
                return;
            }
            _visibility = value;
            IgnoresMouseInteraction = _visibility <= 0;
        }
    }

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
