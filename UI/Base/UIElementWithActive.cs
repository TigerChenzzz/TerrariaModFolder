using Terraria.UI;

namespace ModFolder.UI.Base;

public class UIElementWithActive : UIElement {
    public bool Active { get; set; } = true;
    #region Update
    public sealed override void Update(GameTime gameTime) {
        if (Active) {
            UpdateInner(gameTime);
        }
    }
    protected void BaseUpdate(GameTime gameTime) => base.Update(gameTime);
    protected virtual void UpdateInner(GameTime gameTime) => base.Update(gameTime);
    #endregion
    #region Draw
    public sealed override void Draw(SpriteBatch spriteBatch) {
        if (Active) {
            DrawInner(spriteBatch);
        }
    }
    protected void BaseDraw(SpriteBatch spriteBatch) => base.Draw(spriteBatch);
    protected virtual void DrawInner(SpriteBatch spriteBatch) => base.Draw(spriteBatch);
    #endregion
}
