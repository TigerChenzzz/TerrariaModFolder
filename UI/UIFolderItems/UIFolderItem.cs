﻿using ModFolder.Systems;
using ModFolder.UI.Base;
using ModFolder.UI.Menu;
using ModFolder.UI.UIFolderItems.Folder;
using ReLogic.Content;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader.UI;
using Terraria.UI;

namespace ModFolder.UI.UIFolderItems;

/// <summary>
/// 文件夹系统列表中的一件物品, 可能是文件夹, 可能是模组, 也可能是其它什么东西
/// </summary>
public abstract partial class UIFolderItem : UIElement {
    #region 构造
    public UIFolderItem() : base() {
        rightButtons = new UIImageWithVisibility?[RightButtonsLength];
        _name = new(this);
        Height.Pixels = 32;
        Width.Percent = 1f;
        OnLeftMouseDown += OnLeftMouseDown_TrySelect;
        OnRightMouseDown += OnRightMouseDown_TryDrag;
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
    public bool Selectable { get; set; } = true;
    // 在本层中的序号
    public int IndexCache { get; set; }

    #region 右边的按钮
    #region 板板
    protected void AppendRightButtonsPanel() {
        var panel = _rightButtonsPanel;
        _rightButtonsPanelIndex = this.AppendAndGetIndex(panel);
        foreach (var button in rightButtons) {
            if (button is not null) {
                panel.Append(button);
            }
        }
    }
    private int _rightButtonsPanelIndex;
    protected readonly UIElementWithContainsPointByChildren _rightButtonsPanel = new();
    private readonly UIElement _rightButtonsPanelPlaceHolder = GetAPlaceHolderElement();
    private bool _rightButtonsPanelIsPlaceHolder;
    protected void SetRightButtonsPanelToNormal() {
        if (_rightButtonsPanelIsPlaceHolder) {
            _rightButtonsPanelIsPlaceHolder = false;
            this.ReplaceChildrenByIndex(_rightButtonsPanelIndex, _rightButtonsPanel);
            ArrangeRecalculateChildren();
        }
    }
    protected void SetRightButtonsPanelToPlaceHolder() {
        if (!_rightButtonsPanelIsPlaceHolder) {
            _rightButtonsPanelIsPlaceHolder = true;
            this.ReplaceChildrenByIndex(_rightButtonsPanelIndex, _rightButtonsPanelPlaceHolder);
            ArrangeRecalculateChildren();
        }
    }
    protected float SetRightButtonsPanelToStripeLayout() {
        _rightButtonsPanel.Left = new();
        _rightButtonsPanel.Width = new(-4, 1);
        _rightButtonsPanel.Top = new();
        _rightButtonsPanel.Height = new(0, 1);
        SetRightButtonsPanelToNormal();
        return SettleRightButtons();
    }
    protected void SetRightButtonsPanelToBlockLayout() {
        _rightButtonsPanel.Left = new(2, 0);
        _rightButtonsPanel.Width = new(-4, 1);
        _rightButtonsPanel.Top = new(2, 0);
        _rightButtonsPanel.Height = new(-4, 1);
        SetRightButtonsPanelToPlaceHolder();
        SettleRightButtons();
    }
    protected void SetRightButtonsPanelToBlockWithNameLayout() {
        _rightButtonsPanel.Left = new(2, 0);
        _rightButtonsPanel.Width = new(-4, 1);
        _rightButtonsPanel.Top = new(2, 0);
        _rightButtonsPanel.Height = new(-4, 1);
        SetRightButtonsPanelToPlaceHolder();
        SettleRightButtons();
    }
    private void Draw_UpdateRightButtons() {
        if (LayoutType != LayoutTypes.Stripe) {
            if (IsMouseHovering) {
                SetRightButtonsPanelToNormal();
            }
            else {
                SetRightButtonsPanelToPlaceHolder();
            }
        }
    }
    #endregion
    #region 按钮数组
    protected virtual int RightButtonsLength => 0;
    protected readonly UIImageWithVisibility?[] rightButtons;
    #endregion
    protected static UIImageWithVisibility NewRightButton(Asset<Texture2D> texture, float visibility = 1) => new UIImageWithVisibility(texture) {
        Width = new(24, 0),
        Height = new(24, 0),
        Visibility = visibility,
    }.SettleCommonly();
    protected float SettleRightButtons() {
        // TODO: 配置右边这堆按钮是否向右缩紧
        bool leanToTheRight = true;
        float rightOffset = 0;
        int len = rightButtons.Length;
        if (NoStripeLayout) {
            float topOffset = 0;
            for (int i = 0; i < len; ++i) {
                var button = rightButtons[i];
                if (button != null && button.Visibility > 0) {
                    rightOffset -= 24;
                    if (rightOffset < -24 && -rightOffset > BlockWidth - 4 /* _rightButtonsPanel._dimensions.Width */) {
                        rightOffset = -24;
                        topOffset += 24;
                    }
                    button.Left = new(rightOffset, 1);
                    button.Top = new(topOffset, 0);
                    button.VAlign = 0;
                }
                else if (!leanToTheRight) {
                    rightOffset -= 24;
                    if (rightOffset < -24 && -rightOffset > BlockWidth - 4 /* _rightButtonsPanel._dimensions.Width */) {
                        rightOffset = -24;
                        topOffset += 24;
                    }
                }
            }
        }
        else {
            for (int i = 0; i < len; ++i) {
                var button = rightButtons[i];
                if (button != null && button.Visibility > 0) {
                    rightOffset -= 24;
                    button.Left = new(rightOffset, 1);
                    button.Top = new();
                    button.VAlign = 0.5f;
                }
                else if (!leanToTheRight) {
                    rightOffset -= 24;
                }
            }
        }
        ArrangeRecalculateChildren();
        return rightOffset;
    }
    #endregion
    #region 名字与重命名
    protected UINamePanel _name;
    protected void OnInitialize_Name() {
        Append(_name);
        mouseOverTooltips.Add(_name, GetNameMouseOverTooltipFunc());
    }
    protected abstract Func<string> GetNameMouseOverTooltipFunc();
    protected void SetNameToPlaceHolder(bool force) {
        if (force) {
            _name.SetToPlaceHolderF();
        }
        else {
            _name.SetToPlaceHolderS();
        }
    }
    protected void SetNameToNormal(bool force) {
        if (force) {
            _name.SetToTextF();
        }
        else {
            _name.SetToTextS();
        }
    }
    protected virtual bool ShouldHideNameWhenNotMouseOver => LayoutType == LayoutTypes.Block;
    private void Draw_UpdateName() {
        if (ShouldHideNameWhenNotMouseOver) {
            if (IsMouseHovering) {
                SetNameToNormal(false);
            }
            else {
                SetNameToPlaceHolder(false);
            }
        }
    }
    /// <summary>
    /// 获取在 UIText 上显示的名字
    /// </summary>
    protected abstract string GetDisplayName();
    /// <summary>
    /// 获取重命名输入框初始值
    /// </summary>
    protected abstract string GetRenameText();
    protected abstract string GetRenameHintText();
    protected abstract bool TryRename(string newName);
    #endregion
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
        if (!IsMouseHovering) {
            return;
        }
        var tooltip = GetTooltip();
        if (tooltip != null) {
            UICommon.TooltipMouseText(tooltip);
        }
    }
    #endregion
    #region Draw
    #region 颜色
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
    #endregion
    protected virtual Color PanelColor => UICommon.DefaultUIBlue;
    protected virtual Color PanelHoverColor => UICommon.DefaultUIBlueMouseOver;
    public override void DrawSelf(SpriteBatch spriteBatch) {
        var dimensions = GetDimensions();
        var rectangle = dimensions.ToRectangle();
        UIModFolderMenu menu = UIModFolderMenu.Instance;
        #region 画分割线
        Rectangle dividerRect;
        if (NoStripeLayout) {
            dividerRect = new((int)(dimensions.X + dimensions.Width - 1), (int)dimensions.Y, 4, (int)dimensions.Height); // 右方
            Utils.DrawInvBG(spriteBatch, dimensions.X, dimensions.Y, dimensions.Width, dimensions.Height, IsMouseHovering ? PanelHoverColor : PanelColor);
        }
        else {
            dividerRect = new((int)dimensions.X, (int)(dimensions.Y + dimensions.Height - 1), (int)dimensions.Width, 4); // 下方
            spriteBatch.Draw(UICommon.DividerTexture.Value, dividerRect, Color.White);
        }
        #endregion
        #region 收藏
        if (Favorite) {
            // TODO: 金光闪闪冒粒子
            var gold = Color.Gold;
            int a = menu.Timer % 180;
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
        bool isDraggingTo = menu.DraggingTo == this;
        if (menu.SelectingItems.Count != 0) {
            if (menu.SelectingItems.Contains(this)) {
                spriteBatch.DrawDashedOutline(rectangle, Color.White * 0.8f, start: menu.Timer / 3);
            }
            if (isDraggingTo) {
                if (menu.RealDraggingTo(this)) {
                    switch (menu.DraggingDirection) {
                    case > 0:
                        // 在下方画线
                        spriteBatch.Draw(MTextures.White, dividerRect, Color.White);
                        break;
                    case < 0:
                        // 若是返回上一级则仍然在下方画线
                        if (this is UIFolder f && f.FolderName == "..") {
                            spriteBatch.Draw(MTextures.White, dividerRect, Color.White);
                            break;
                        }
                        // 在上方 / 左方画线
                        Rectangle r = StripeLayout ? new((int)dimensions.X, (int)(dimensions.Y - 3), (int)dimensions.Width, 4)
                            : new((int)(dimensions.X - 3), (int)dimensions.Y, 4, (int)dimensions.Height);
                        spriteBatch.Draw(MTextures.White, r, Color.White);
                        break;
                    default:
                        // 拖入时高亮
                        spriteBatch.DrawBox(rectangle, Color.White * 0.3f, Color.White * 0.1f);
                        break;
                    }
                }
            }
            else if (IsMouseHovering) {
                spriteBatch.DrawBox(rectangle, Color.White * 0.3f, Color.White * 0.1f);
            }
        }
        else if (IsMouseHovering) {
            spriteBatch.DrawBox(rectangle, Color.White * 0.3f, Color.White * 0.1f);
        }
        #endregion
    }
    public override void Draw(SpriteBatch spriteBatch) {
        Draw_UpdateRightButtons();
        Draw_UpdateName();
        Draw_ArrangeRecalculateChildren();
        base.Draw(spriteBatch);
        Draw_Tooltip();
    }
    #region DrawParallelogram
    private readonly static Dictionary<int, Asset<Texture2D>> _slashTextures = [];
    /// <summary>
    /// 从右上到左下的一根斜线
    /// </summary>
    private static Texture2D GetSlashTexture(int size) {
        if (_slashTextures.TryGetValue(size, out var result)) {
            return result.Value;
        }
        if (size <= 0) {
            return Textures.Colors.White.Value;
        }
        Color[] colors = new Color[size * size];
        for (int i = 1; i <= size; ++i) {
            colors[(size - 1) * i] = Color.White;
        }
        result = AssetTextureFromColors(size, size, colors);
        _slashTextures.Add(size, result);
        return result.Value;
    }

    protected static void DrawParallelogramLoopByLayout(LayoutTypes layout, SpriteBatch spriteBatch, Rectangle rect, int start, int end, Color borderColor, Color innerColor) {
        if (layout == LayoutTypes.BlockWithName) {
            DrawParallelogramLoopVertical(spriteBatch, rect, start, end, borderColor, innerColor);
        }
        else {
            DrawParallelogramLoopHorizontal(spriteBatch, rect, start, end, borderColor, innerColor);
        }
    }
    protected static void DrawParallelogramByLayout(LayoutTypes layout, SpriteBatch spriteBatch, Rectangle rect, int start, int end, Color borderColor, Color innerColor) {
        if (layout == LayoutTypes.BlockWithName) {
            DrawParallelogramVertical(spriteBatch, rect, start, end, borderColor, innerColor);
        }
        else {
            DrawParallelogramHorizontal(spriteBatch, rect, start, end, borderColor, innerColor);
        }
    }
    protected static void DrawParallelogramLoop(SpriteBatch spriteBatch, Rectangle rect, int start, int end, Color borderColor, Color innerColor) {
        if (rect.Width >= rect.Height) {
            DrawParallelogramLoopHorizontal(spriteBatch, rect, start, end, borderColor, innerColor);
        }
        else {
            DrawParallelogramLoopVertical(spriteBatch, rect, start, end, borderColor, innerColor);
        }
    }
    protected static void DrawParallelogram(SpriteBatch spriteBatch, Rectangle rect, int start, int end, Color borderColor, Color innerColor) {
        if (rect.Width >= rect.Height) {
            DrawParallelogramHorizontal(spriteBatch, rect, start, end, borderColor, innerColor);
        }
        else {
            DrawParallelogramVertical(spriteBatch, rect, start, end, borderColor, innerColor);
        }
    }
    #region Horizontal
    /// <summary>
    /// 画一根单斜线, 包含左右边界, 不包含上下边界
    /// </summary>
    private static void DrawParallelogramLoopHorizontal_SingleSlash(SpriteBatch spriteBatch, Rectangle rect, int position, Color borderColor, Color innerColor) {
        position = Modular(position, rect.Width);
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
    protected static void DrawParallelogramLoopHorizontal(SpriteBatch spriteBatch, Rectangle rect, int start, int end, Color borderColor, Color innerColor) {
        int startOrigin = start;
        start = Modular(start, rect.Width);
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
                DrawParallelogramLoopHorizontal_SingleSlash(spriteBatch, rect, i, borderColor, innerColor);
            }
            end = rect.Width;
        }
        else {
            spriteBatch.Draw(MTextures.White, new Rectangle(rect.X + start, rect.Y, end - start, 1), borderColor);
        }
        for (int i = start; i < end; ++i) {
            DrawParallelogramLoopHorizontal_SingleSlash(spriteBatch, rect, i, borderColor, innerColor);
        }
    }
    /// <inheritdoc cref="DrawParallelogramLoopHorizontal_SingleSlash(SpriteBatch, Rectangle, int, Color, Color)"/>
    /// <param name="position">需要在 [1, rect.Width + rect.Height - 3] 区间</param>
    private static void DrawParallelogramHorizontal_SingleSlash(SpriteBatch spriteBatch, Rectangle rect, int position, Color borderColor, Color innerColor) {
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
    protected static void DrawParallelogramHorizontal(SpriteBatch spriteBatch, Rectangle rect, int start, int end, Color borderColor, Color innerColor) {
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
            DrawParallelogramHorizontal_SingleSlash(spriteBatch, rect, i, borderColor, innerColor);
        }
    }
    #endregion
    #region Vertical
    /// <summary>
    /// 画一根单斜线, 包含上下边界, 不包含左右边界
    /// </summary>
    private static void DrawParallelogramLoopVertical_SingleSlash(SpriteBatch spriteBatch, Rectangle rect, int position, Color borderColor, Color innerColor) {
        position = Modular(position, rect.Height);
        if (position >= rect.Width - 1) {
            spriteBatch.Draw(GetSlashTexture(rect.Width - 2), new Rectangle(rect.X + 1, rect.Y + position - rect.Width + 2, rect.Width - 2, rect.Width - 2), innerColor);
            return;
        }
        var slash = GetSlashTexture(rect.Width - 2);
        if (position >= 1) {
            spriteBatch.Draw(MTextures.White, new Rectangle(rect.X + position, rect.Y, 1, 1), borderColor);
            if (position >= 2) {
                spriteBatch.Draw(slash, new Rectangle(rect.X + 1, rect.Y + 1, rect.Width - 2, position - 1), new Rectangle(0, rect.Width - 2 - position + 1, rect.Width - 2, position - 1), innerColor);
            }
        }
        if (position <= rect.Width - 3) {
            spriteBatch.Draw(MTextures.White, new Rectangle(rect.X + position + 1, rect.Bottom - 1, 1, 1), borderColor);
            if (position <= rect.Width - 4) {
                spriteBatch.Draw(slash, new Rectangle(rect.X + 1, rect.Bottom + position - rect.Width + 2, rect.Width - 2, rect.Width - position - 3), new Rectangle(0, 0, rect.Width - 2, rect.Width - position - 3), innerColor);
            }
        }
    }
    protected static void DrawParallelogramLoopVertical(SpriteBatch spriteBatch, Rectangle rect, int start, int end, Color borderColor, Color innerColor) {
        int startOrigin = start;
        start = Modular(start, rect.Height);
        end = end + start - startOrigin;
        if (end <= start) {
            return;
        }
        if (end - start >= rect.Height) {
            spriteBatch.DrawBox(rect, borderColor, innerColor);
            return;
        }
        #region 右边框
        if (start >= rect.Width - 1) {
            if (end < rect.Width + rect.Height) {
                // 两边都在边界内
                spriteBatch.Draw(MTextures.White, new Rectangle(rect.Right - 1, rect.Y + start - rect.Width + 1, 1, end - start), borderColor);
            }
            else {
                // 下边超界
                spriteBatch.Draw(MTextures.White, new Rectangle(rect.Right - 1, rect.Y + start - rect.Width + 1, 1, rect.Bottom - (rect.Y + start - rect.Width + 1)), borderColor);
                spriteBatch.Draw(MTextures.White, new Rectangle(rect.Right - 1, rect.Top, 1, end - rect.Width - rect.Height + 1), borderColor);
            }
        }
        else /*if (end < rect.Width + rect.Height)*/ {
            // 上边超界
            if (end < rect.Width) {
                // 下边不足时
                spriteBatch.Draw(MTextures.White, new Rectangle(rect.Right - 1, rect.Bottom + start - rect.Width, 1, end - start), borderColor);
            }
            else {
                // 下边跨界时
                spriteBatch.Draw(MTextures.White, new Rectangle(rect.Right - 1, rect.Top, 1, end - rect.Width + 1), borderColor);
                spriteBatch.Draw(MTextures.White, new Rectangle(rect.Right - 1, rect.Bottom + start - rect.Width, 1, -start + rect.Width), borderColor);
            }
        }
        #endregion
        if (end > rect.Height) {
            #region 左边框
            spriteBatch.Draw(MTextures.White, new Rectangle(rect.X, rect.Y + start, 1, rect.Height - start), borderColor);
            spriteBatch.Draw(MTextures.White, new Rectangle(rect.X, rect.Y, 1, end - rect.Height), borderColor);
            #endregion
            end -= rect.Height;
            for (int i = 0; i < end; ++i) {
                DrawParallelogramLoopVertical_SingleSlash(spriteBatch, rect, i, borderColor, innerColor);
            }
            end = rect.Height;
        }
        else {
            spriteBatch.Draw(MTextures.White, new Rectangle(rect.X, rect.Y + start, 1, end - start), borderColor);
        }
        for (int i = start; i < end; ++i) {
            DrawParallelogramLoopVertical_SingleSlash(spriteBatch, rect, i, borderColor, innerColor);
        }
    }
    private static void DrawParallelogramVertical_SingleSlash(SpriteBatch spriteBatch, Rectangle rect, int position, Color borderColor, Color innerColor) {
        if (position < 1 || position > rect.Width + rect.Height - 3 || rect.Width <= 0 || rect.Height <= 0) {
            return;
        }
        var slash = GetSlashTexture(rect.Width - 2);
        bool reachTop = position <= rect.Width - 2;
        bool reachBottom = position >= rect.Height;
        if (reachTop) {
            spriteBatch.Draw(MTextures.White, new Rectangle(rect.X + position, rect.Y, 1, 1), borderColor);
        }
        if (reachBottom) {
            spriteBatch.Draw(MTextures.White, new Rectangle(rect.X + position - rect.Height + 1, rect.Bottom - 1, 1, 1), borderColor);
        }
        if (rect.Width <= 2 || rect.Height <= 2) {
            return;
        }
        if (reachTop) {
            if (position < 2) {
                return;
            }
            if (reachBottom) {
                spriteBatch.Draw(slash, new Rectangle(rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2), new Rectangle(0, rect.Width - 2 - position + 1, rect.Width - 2, rect.Height - 2), innerColor);
                return;
            }
            spriteBatch.Draw(slash, new Rectangle(rect.X + 1, rect.Y + 1, rect.Width - 2, position - 1), new Rectangle(0, rect.Width - 2 - position + 1, rect.Width - 2, position - 1), innerColor);

        }
        else if (!reachBottom) {
            spriteBatch.Draw(slash, new Rectangle(rect.X + 1, rect.Y + position - rect.Width + 2, rect.Width - 2, rect.Width - 2), innerColor);
        }
        else {
            position -= rect.Height;
            if (position <= rect.Width - 4) {
                spriteBatch.Draw(slash, new Rectangle(rect.X + 1, rect.Bottom + position - rect.Width + 2, rect.Width - 2, rect.Width - position - 3), new Rectangle(0, 0, rect.Width - 2, rect.Width - position - 3), innerColor);
            }
        }
    }
    protected static void DrawParallelogramVertical(SpriteBatch spriteBatch, Rectangle rect, int start, int end, Color borderColor, Color innerColor) {
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
        #region 右边框
        int rightStart = Math.Max(start - rect.Width + 1, 0);
        int rightEnd = end - rect.Width + 1;
        if (rightEnd > rightStart) {
            spriteBatch.Draw(MTextures.White, new Rectangle(rect.Right - 1, rect.Top + rightStart, 1, rightEnd - rightStart), borderColor);
        }
        #endregion
        #region 左边框
        if (start < rect.Height) {
            int leftEnd = Math.Min(end, rect.Height);
            spriteBatch.Draw(MTextures.White, new Rectangle(rect.Left, rect.Top + start, 1, leftEnd - start), borderColor);
        }
        #endregion
        for (int i = start; i < end; ++i) {
            DrawParallelogramVertical_SingleSlash(spriteBatch, rect, i, borderColor, innerColor);
        }
    }
    #endregion
    #endregion
    #endregion
    #region 排序与过滤
    public enum PassFilterResults {
        NotFiltered,
        FilteredBySearch,
        FilteredByModSide,
        FilteredByEnabled,
        FilteredByLoaded,
    }
    public virtual PassFilterResults PassFiltersInner() => PassFilterResults.NotFiltered;
    public bool PassFilters(UIFolderItemFilterResults filterResults) {
        switch (PassFiltersInner()) {
        case PassFilterResults.FilteredBySearch:
            filterResults.FilteredBySearch += 1;
            return false;
        case PassFilterResults.FilteredByModSide:
            filterResults.FilteredByModSide += 1;
            return false;
        case PassFilterResults.FilteredByEnabled:
            filterResults.FilteredByEnabled += 1;
            return false;
        case PassFilterResults.FilteredByLoaded:
            filterResults.FilteredByLoaded += 1;
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
    #region 选择与拖动
    private void OnLeftMouseDown_TrySelect(UIMouseEvent evt, UIElement listeningElement) => UIModFolderMenu.Instance.LeftMouseDownOnFolderItem(this);
    private void OnRightMouseDown_TryDrag(UIMouseEvent evt, UIElement listeningElement) => UIModFolderMenu.Instance.RightMouseDownOnFolderItem(this);
    #endregion
    #region 布局 Layout
#if DEBUG
    public static float StripeHeight => 32;
    public static float BlockWidth => 90;
    public static float BlockHeight => 90;
    public static float BlockWithNameHeight => 112;
#else
    public const float StripeHeight = 32;
    public const float BlockWidth = 90;
    public const float BlockHeight = 90;
    public const float BlockWithNameHeight = 112;
#endif
    protected static LayoutTypes MenuLayoutType => UIModFolderMenu.Instance.LayoutType;
    protected LayoutTypes LayoutType { get; set; }
    protected bool NoStripeLayout => LayoutType != LayoutTypes.Stripe;
    protected bool StripeLayout => LayoutType == LayoutTypes.Stripe;
    protected bool BlockLayout => LayoutType == LayoutTypes.Block;
    protected bool BlockWithNameLayout => LayoutType == LayoutTypes.BlockWithName;
    protected void ForceRecalculateLayout() {
        switch (LayoutType) {
        case LayoutTypes.Stripe:
            RecalculateStripeLayout();
            break;
        case LayoutTypes.Block:
            RecalculateBlockLayout();
            break;
        case LayoutTypes.BlockWithName:
            RecalculateBlockWithNameLayout();
            break;
        }
    }
    protected virtual void RecalculateStripeLayout() {
        Left.Set(0, 0);
        Width.Set(0, 1);
        Height.Set(StripeHeight, 0);
    }
    protected virtual void RecalculateBlockLayout() {
        Width.Set(BlockWidth, 0);
        Height.Set(BlockHeight, 0);
    }
    protected virtual void RecalculateBlockWithNameLayout() {
        Width = new(BlockWidth, 0);
        Height = new(BlockWithNameHeight, 0);
    }
    protected static UIElement GetAPlaceHolderElement() => new() {
        IgnoresMouseInteraction = true,
    };
    #endregion
    #region ArrangeRecalculateChildren
    private bool _needRecalculateChildren;
    public void ArrangeRecalculateChildren() => _needRecalculateChildren = true;
    private void Draw_ArrangeRecalculateChildren() {
        if (_needRecalculateChildren) {
            RecalculateChildren();
        }
    }
    #endregion
    #region overrides
    public sealed override void RecalculateChildren() {
        if (!_isInitialized) {
            ArrangeRecalculateChildren();
            return;
        }
        if (LayoutType != MenuLayoutType) {
            LayoutType = MenuLayoutType;
            ForceRecalculateLayout();
        }
        _needRecalculateChildren = false;
        base.RecalculateChildren();
    }
    #endregion
}
