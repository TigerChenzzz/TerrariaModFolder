using Terraria.ModLoader.UI;
using Terraria.UI;

namespace ModFolder.UI;

/// <summary>
/// 文件夹系统列表中的一件物品, 可能是文件夹, 可能是模组, 也可能是其它什么东西
/// </summary>
public class UIFolderItem : UIElement {
    #region 构造
    public UIFolderItem() : base() {
        Height.Pixels = 30;
        Width.Percent = 1f;
    }
    #endregion
    public override void DrawSelf(SpriteBatch spriteBatch) {
        var dimensions = GetDimensions();
        var rectangle = dimensions.ToRectangle();
        #region 画分割线
        Rectangle dividerRect = new((int)dimensions.X, (int)(dimensions.Y + dimensions.Height - 1), (int)dimensions.Width, 4);
        spriteBatch.Draw(UICommon.DividerTexture.Value, dividerRect, Color.White);
        #endregion
        #region 鼠标在上面时高亮
        if (IsMouseHovering) {
            spriteBatch.DrawBox(rectangle, Color.White * 0.3f, Color.White * 0.1f);
        }
        #endregion
    }
}
