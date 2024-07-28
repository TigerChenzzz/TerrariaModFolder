using ModFolder.Systems;
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
        OnRightMouseDown += (_, _) => {
            if (RightDraggable) {
                UIModFolderMenu.Instance.DraggingTarget = this;
            }
        };
    }
    #endregion
    public virtual FolderDataSystem.Node? Node { get => null; }
    public bool RightDraggable { get; set; } = true;
    public override void DrawSelf(SpriteBatch spriteBatch) {
        var dimensions = GetDimensions();
        var rectangle = dimensions.ToRectangle();
        #region 画分割线
        Rectangle dividerRect = new((int)dimensions.X, (int)(dimensions.Y + dimensions.Height - 1), (int)dimensions.Width, 4);
        spriteBatch.Draw(UICommon.DividerTexture.Value, dividerRect, Color.White);
        #endregion
        #region 鼠标在上面时高亮
        if (UIModFolderMenu.Instance.DraggingTarget != null) {
            if (UIModFolderMenu.Instance.DraggingTarget == this) {
                spriteBatch.DrawDashedOutline(rectangle, Color.White * 0.8f, start: UIModFolderMenu.Instance.Timer / 2);
            }
            else if (UIModFolderMenu.Instance.DraggingTo == this) {
                switch (UIModFolderMenu.Instance.DraggingDirection) {
                case > 0:
                    spriteBatch.Draw(Textures.White, dividerRect, Color.White);
                    break;
                case < 0:
                    if (this is UIFolder f && f.Name == "..") {
                        spriteBatch.Draw(Textures.White, dividerRect, Color.White);
                        break;
                    }
                    Rectangle r = new((int)dimensions.X, (int)(dimensions.Y - 3), (int)dimensions.Width, 4);
                    spriteBatch.Draw(Textures.White, r, Color.White);
                    break;
                default:
                    spriteBatch.DrawBox(rectangle, Color.White * 0.3f, Color.White * 0.1f);
                    break;
                }
            }
        }
        else if (IsMouseHovering) {
            spriteBatch.DrawBox(rectangle, Color.White * 0.3f, Color.White * 0.1f);
        }
        #endregion
    }
}
