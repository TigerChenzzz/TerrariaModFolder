using Terraria.UI;

namespace ModFolder.UI.Base;

public class UIElementCustom : UIElement {
    public event Action<SpriteBatch>? OnDraw;
    public override void DrawSelf(SpriteBatch spriteBatch) {
        OnDraw?.Invoke(spriteBatch);
    }
}
