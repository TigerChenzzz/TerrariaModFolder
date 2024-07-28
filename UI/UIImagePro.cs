using ReLogic.Content;
using Terraria.GameContent.UI.Elements;

namespace ModFolder.UI;

// 主要修改: 添加 PreDrawSelf 和 SourceRectangle
public class UIImagePro : UIImage {
    public event Action<SpriteBatch>? PreDrawSelf;
    public Rectangle? SourceRectangle { get; set; }
    public UIImagePro(Texture2D nonReloadingTexture) : base(nonReloadingTexture) { }
    public UIImagePro(Asset<Texture2D> texture) : base(texture) { }
    public override void DrawSelf(SpriteBatch spriteBatch) {
        PreDrawSelf?.Invoke(spriteBatch);
        
        Texture2D texture2D;
        if (_nonReloadingTexture != null) {
            texture2D = _nonReloadingTexture;
        }
        else if (_texture != null && _texture.IsLoaded) {
            texture2D = _texture.Value;
        }
        else {
            return;
        }

        if (ScaleToFit) {
            spriteBatch.Draw(texture2D, _dimensions.ToRectangle(), SourceRectangle, Color);
            return;
        }

        Vector2 vector = texture2D.Size();
        Vector2 vector2 = _dimensions.Position() + vector * (1f - ImageScale) / 2f + vector * NormalizedOrigin;
        if (RemoveFloatingPointsFromDrawPosition) {
            vector2 = vector2.Floor();
        }

        spriteBatch.Draw(texture2D, vector2, SourceRectangle, Color, Rotation, vector * NormalizedOrigin, ImageScale, SpriteEffects.None, 0f);
    }
}
