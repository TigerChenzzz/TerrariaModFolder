using ModFolder.Systems;
using ModFolder.UI.Base;
using ModFolder.UI.Menu;
using ModFolder.UI.UIFolderItems.Folder;
using Terraria.ModLoader.UI;
using Terraria.UI;

namespace ModFolder.UI.UIFolderItems;

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
    #region MouseoverTooltip
    protected readonly List<(UIElement, Func<string?>)> mouseOverTooltips = [];
    protected virtual string? GetTooltip() {
        foreach (var (ui, f) in mouseOverTooltips) {
            if (ui.IsMouseHovering) {
                var tooltip = f();
                if (tooltip != null) {
                    return tooltip;
                }
            }
        }
        return null;
    }
    private void Draw_Tooltip() {
        var tooltip = GetTooltip();
        if (tooltip != null) {
            UICommon.TooltipMouseText(tooltip);
        }
    }
    #endregion
    #region Draw
    public static Color EnabledColor { get; } = Color.White;
    public static Color EnabledBorderColor { get; } = Color.White * 0.6f;
    public static Color EnabledInnerColor { get; } = Color.White * 0.2f;
    // TODO: 调色   现在的绿色貌似不是很显眼
    public static Color ToEnableColor { get; } = new Color(0f, 1f, 0f);
    public static Color ToEnableBorderColor { get; } = new Color(0f, 1f, 0f) * 0.6f;
    public static Color ToEnableInnerColor { get; } = new Color(0f, 1f, 0f) * 0.15f;
    public static Color ToDisableColor { get; } = Color.Red;
    public static Color ToDisableBorderColor { get; } = Color.Red * 0.6f;
    public static Color ToDisableInnerColor { get; } = Color.Red * 0.15f;
    // TODO: 和收藏的颜色冲突了
    public static Color ConfigNeedReloadBorderColor { get; } = Color.Yellow * 0.6f;
    public static Color ConfigNeedReloadInnerColor { get; } = Color.Yellow * 0.2f;
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
            spriteBatch.Draw(MTextures.White, rectangle, gold);
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
                    spriteBatch.Draw(MTextures.White, dividerRect, Color.White);
                    break;
                case < 0:
                    if (this is UIFolder f && f.Name == "..") {
                        spriteBatch.Draw(MTextures.White, dividerRect, Color.White);
                        break;
                    }
                    Rectangle r = new((int)dimensions.X, (int)(dimensions.Y - 3), (int)dimensions.Width, 4);
                    spriteBatch.Draw(MTextures.White, r, Color.White);
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
    public override void Draw(SpriteBatch spriteBatch) {
        base.Draw(spriteBatch);
        Draw_Tooltip();
    }
    #region DrawParallelogram
    private readonly static Dictionary<int, Texture2D> _slashTextures = [];
    private static Texture2D GetSlashTexture(int size) {
        if (_slashTextures.TryGetValue(size, out var result)) {
            return result;
        }
        Color[] colors = new Color[size * size];
        for (int i = 1; i <= size; ++i) {
            colors[(size - 1) * i] = Color.White;
        }
        result = MTextures.FromColors(size, size, colors);
        _slashTextures.Add(size, result);
        return result;
    }
    /// <summary>
    /// 画一根单斜线, 包含左右边界, 不包含上下边界
    /// </summary>
    private static void DrawParallelogramLoop_SingleSlash(SpriteBatch spriteBatch, Rectangle rect, int position, Color borderColor, Color innerColor) {
        position %= rect.Width;
        if (position < 0) {
            position += rect.Width;
        }
        if (position >= rect.Height - 1) {
            spriteBatch.Draw(GetSlashTexture(rect.Height - 2), new Rectangle(rect.X + position - rect.Height + 2, rect.Y + 1, rect.Height - 2, rect.Height - 2), innerColor);
            return;
        }
        var slash = GetSlashTexture(rect.Height - 2);
        if (position >= 1) {
            // 左边的边界点
            spriteBatch.Draw(MTextures.White, new Rectangle(rect.X, rect.Y + position, 1, 1), borderColor);
            if (position >= 2) {
                // 左边的斜杠
                spriteBatch.Draw(slash, new Rectangle(rect.X + 1, rect.Y + 1, position - 1, rect.Height - 2), new Rectangle(rect.Height - 2 - position + 1, 0, position - 1, rect.Height - 2), innerColor);
            }
        }
        if (position <= rect.Height - 3) {
            // 右边的边界点
            spriteBatch.Draw(MTextures.White, new Rectangle(rect.Right - 1, rect.Y + position + 1, 1, 1), borderColor);
            if (position <= rect.Height - 4) {
                // 右边的斜杠
                spriteBatch.Draw(slash, new Rectangle(rect.Right + position - rect.Height + 2, rect.Y + 1, rect.Height - position - 3, rect.Height - 2), new Rectangle(0, 0, rect.Height - position - 3, rect.Height - 2), innerColor);
            }
        }
    }
    /// <summary>
    /// 需要 <paramref name="end"/> > <paramref name="start"/>, 否则不会画任何东西
    /// </summary>
    protected static void DrawParallelogramLoop(SpriteBatch spriteBatch, Rectangle rect, int start, int end, Color borderColor, Color innerColor) {
        int startOrigin = start;
        start %= rect.Width;
        if (start < 0) {
            start += rect.Width;
        }
        end = end + start - startOrigin;
        if (end <= start) {
            return;
        }
        if (end - start >= rect.Width) {
            spriteBatch.DrawBox(rect, borderColor, innerColor);
            return;
        }
        #region 下边框
        if (start >= rect.Height - 1) {
            if (end < rect.Width + rect.Height) {
                // 两边都在边界内
                spriteBatch.Draw(MTextures.White, new Rectangle(rect.X + start - rect.Height + 1, rect.Bottom - 1, end - start, 1), borderColor);
            }
            else {
                // 右边超界
                spriteBatch.Draw(MTextures.White, new Rectangle(rect.X + start - rect.Height + 1, rect.Bottom - 1, rect.Right - (rect.X + start - rect.Height + 1), 1), borderColor);
                spriteBatch.Draw(MTextures.White, new Rectangle(rect.Left, rect.Bottom - 1, end - rect.Width - rect.Height + 1, 1), borderColor);
            }
        }
        else /*if (end < rect.Width + rect.Height)*/ {
            // 左边超界
            if (end < rect.Height) {
                // 右边不足时
                spriteBatch.Draw(MTextures.White, new Rectangle(rect.Right + start - rect.Height, rect.Bottom - 1, end - start, 1), borderColor);
            }
            else {
                // 右边跨界时
                spriteBatch.Draw(MTextures.White, new Rectangle(rect.Left, rect.Bottom - 1, end - rect.Height + 1, 1), borderColor);
                spriteBatch.Draw(MTextures.White, new Rectangle(rect.Right + start - rect.Height, rect.Bottom - 1, -start + rect.Height, 1), borderColor);
            }
        }
        #endregion
        if (end > rect.Width) {
            #region 上边框
            spriteBatch.Draw(MTextures.White, new Rectangle(rect.X + start, rect.Y, rect.Width - start, 1), borderColor);
            spriteBatch.Draw(MTextures.White, new Rectangle(rect.X, rect.Y, end - rect.Width, 1), borderColor);
            #endregion
            end -= rect.Width;
            for (int i = 0; i < end; ++i) {
                DrawParallelogramLoop_SingleSlash(spriteBatch, rect, i, borderColor, innerColor);
            }
            end = rect.Width;
        }
        else {
            spriteBatch.Draw(MTextures.White, new Rectangle(rect.X + start, rect.Y, end - start, 1), borderColor);
        }
        for (int i = start; i < end; ++i) {
            DrawParallelogramLoop_SingleSlash(spriteBatch, rect, i, borderColor, innerColor);
        }
    }
    /// <inheritdoc cref="DrawParallelogramLoop_SingleSlash(SpriteBatch, Rectangle, int, Color, Color)"/>
    /// <param name="position">需要在 [1, rect.Width + rect.Height - 3] 区间</param>
    private static void DrawParallelogram_SingleSlash(SpriteBatch spriteBatch, Rectangle rect, int position, Color borderColor, Color innerColor) {
        if (position < 1 || position > rect.Width + rect.Height - 3 || rect.Width <= 0 || rect.Height <= 0) {
            return;
        }
        var slash = GetSlashTexture(rect.Height - 2);
        bool reachLeft = position <= rect.Height - 2;
        bool reachRight = position >= rect.Width;
        // 左边的边界点
        if (reachLeft) {
            spriteBatch.Draw(MTextures.White, new Rectangle(rect.X, rect.Y + position, 1, 1), borderColor);
        }
        // 右边的边界点
        if (reachRight) {
            spriteBatch.Draw(MTextures.White, new Rectangle(rect.Right - 1, rect.Y + position - rect.Width + 1, 1, 1), borderColor);
        }
        if (rect.Width <= 2 || rect.Height <= 2) {
            return;
        }
        if (reachLeft) {
            if (position < 2) {
                return;
            }
            if (reachRight) {
                spriteBatch.Draw(slash, new Rectangle(rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2), new Rectangle(rect.Height - 2 - position + 1, 0, rect.Width - 2, rect.Height - 2), innerColor);
                return;
            }
            // 左边的斜杠
            spriteBatch.Draw(slash, new Rectangle(rect.X + 1, rect.Y + 1, position - 1, rect.Height - 2), new Rectangle(rect.Height - 2 - position + 1, 0, position - 1, rect.Height - 2), innerColor);

        }
        else if (!reachRight) {
            // 两边都没有接触时画出完整的斜杠
            spriteBatch.Draw(slash, new Rectangle(rect.X + position - rect.Height + 2, rect.Y + 1, rect.Height - 2, rect.Height - 2), innerColor);
        }
        else {
            position -= rect.Width;
            if (position <= rect.Height - 4) {
                // 右边的斜杠
                spriteBatch.Draw(slash, new Rectangle(rect.Right + position - rect.Height + 2, rect.Y + 1, rect.Height - position - 3, rect.Height - 2), new Rectangle(0, 0, rect.Height - position - 3, rect.Height - 2), innerColor);
            }
        }
    }
    /// <inheritdoc cref="DrawParallelogramLoop(SpriteBatch, Rectangle, int, int, Color, Color)"/>
    protected static void DrawParallelogram(SpriteBatch spriteBatch, Rectangle rect, int start, int end, Color borderColor, Color innerColor) {
        if (end <= start) {
            return;
        }
        if (start <= 0) {
            if (end >= rect.Width + rect.Height - 1) {
                spriteBatch.DrawBox(rect, borderColor, innerColor);
                return;
            }
            start = 0;
        }
        else if (end > rect.Width + rect.Height - 1) {
            end = rect.Width + rect.Height - 1;
        }
        #region 下边框
        int downStart = Math.Max(start - rect.Height + 1, 0);
        int downEnd = end - rect.Height + 1;
        if (downEnd > downStart) {
            spriteBatch.Draw(MTextures.White, new Rectangle(rect.Left + downStart, rect.Bottom - 1, downEnd - downStart, 1), borderColor);
        }
        #endregion
        #region 上边框
        if (start < rect.Width) {
            int upEnd = Math.Min(end, rect.Width);
            spriteBatch.Draw(MTextures.White, new Rectangle(rect.Left + start, rect.Top, upEnd - start, 1), borderColor);
        }
        #endregion
        for (int i = start; i < end; ++i) {
            DrawParallelogram_SingleSlash(spriteBatch, rect, i, borderColor, innerColor);
        }
    }
    #endregion
    #endregion
    #region 排序与过滤
    public enum PassFilterResults {
        NotFiltered,
        FilteredBySearch,
        FilteredByModSide,
        FilteredByEnabled,
    }
    public virtual PassFilterResults PassFiltersInner() => PassFilterResults.NotFiltered;
    public bool PassFilters(UIModsFilterResults filterResults) {
        switch (PassFiltersInner()) {
        case PassFilterResults.FilteredBySearch:
            filterResults.filteredBySearch += 1;
            return false;
        case PassFilterResults.FilteredByModSide:
            filterResults.filteredByModSide += 1;
            return false;
        case PassFilterResults.FilteredByEnabled:
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
