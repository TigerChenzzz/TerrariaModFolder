using ReLogic.Content;
using Terraria.GameContent.UI.Elements;

namespace ModFolder.UI;

internal class UIImageWithVisible : UIImage {
    private bool _visible = true;
    public bool Visible {
        get => _visible;
        set {
            _visible = value;
            IgnoresMouseInteraction = !value;
        }
    }
    public UIImageWithVisible(Asset<Texture2D> texture) : base(texture) { }
    public UIImageWithVisible(Texture2D nonReloadingTexture) : base(nonReloadingTexture) { }
    public override void DrawSelf(SpriteBatch spriteBatch) {
        if (Visible) {
            base.DrawSelf(spriteBatch);
        }
    }
}
