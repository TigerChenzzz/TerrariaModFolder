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
    public enum FolderItemTypeEnum {
        Mod,
        Folder
    }
    public virtual DateTime LastModified { get => default; }
    public virtual string NameToSort { get => string.Empty; }
    public virtual FolderItemTypeEnum FolderItemType => FolderItemTypeEnum.Mod;
    public virtual bool Favorite { get => false; set { } }
    public virtual FolderDataSystem.Node? Node { get => null; }
    public bool RightDraggable { get; set; } = true;
    public override void DrawSelf(SpriteBatch spriteBatch) {
        var dimensions = GetDimensions();
        var rectangle = dimensions.ToRectangle();
        #region 画分割线
        Rectangle dividerRect = new((int)dimensions.X, (int)(dimensions.Y + dimensions.Height - 1), (int)dimensions.Width, 4);
        spriteBatch.Draw(UICommon.DividerTexture.Value, dividerRect, Color.White);
        #endregion
        #region 收藏
        if (Favorite) {
            // TODO: 金光闪闪冒粒子
            var gold = Color.Gold;
            int a = UIModFolderMenu.Instance.Timer % 180;
            if (a < 0) {
                a += 180;
            }
            if (a > 90) {
                a = 180 - a;
            }
            gold *= (float)a / 450 + 0.05f;
            spriteBatch.Draw(Textures.White, rectangle, gold);
        }
        #endregion
        #region 鼠标在上面时高亮; 当为拖动对象时虚线显示, 为拖动目的地时显示高亮或上下
        if (UIModFolderMenu.Instance.DraggingTarget != null) {
            if (UIModFolderMenu.Instance.DraggingTarget == this) {
                spriteBatch.DrawDashedOutline(rectangle, Color.White * 0.8f, start: UIModFolderMenu.Instance.Timer / 3);
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
    #region 排序与过滤
    /// <summary>
    /// <br/>0 代表没有被过滤
    /// <br/>1 代表被搜索过滤
    /// <br/>2 代表被模组类型过滤
    /// <br/>3 代表被启用状态过滤
    /// </summary>
    public virtual int PassFiltersInner() => 0;
    public bool PassFilters(UIModsFilterResults filterResults) {
        switch (PassFiltersInner()) {
        case 1:
            filterResults.filteredBySearch += 1;
            return false;
        case 2:
            filterResults.filteredByModSide += 1;
            return false;
        case 3:
            filterResults.filteredByEnabled += 1;
            return false;
        default:
            return true;
        }
    }
    public override int CompareTo(object obj) {
        if (obj is not UIFolderItem i)
            return base.CompareTo(obj);
        if (FolderItemType != i.FolderItemType) {
            var fm = UIModFolderMenu.Instance.FmSortMode;
            if (fm != FolderModSortMode.Custom) {
                // 如果文件夹优先但不是文件夹或模组优先但是文件夹, 则排在后面, 否则排在前面
                return fm == FolderModSortMode.FolderFirst ^ FolderItemType == FolderItemTypeEnum.Folder ? 1 : -1;
            }
        }
        return UIModFolderMenu.Instance.SortMode switch {
            FolderMenuSortMode.Custom => 0,
            FolderMenuSortMode.RecentlyUpdated => i.LastModified.CompareTo(LastModified),
            FolderMenuSortMode.OldlyUpdated => LastModified.CompareTo(i.LastModified),
            FolderMenuSortMode.DisplayNameAtoZ => string.Compare(NameToSort, i.NameToSort, StringComparison.OrdinalIgnoreCase),
            FolderMenuSortMode.DisplayNameZtoA => string.Compare(i.NameToSort, NameToSort, StringComparison.OrdinalIgnoreCase),
            _ => base.CompareTo(obj),
        };
    }
    #endregion
}
