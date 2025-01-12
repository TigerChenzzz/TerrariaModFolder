using Microsoft.Xna.Framework.Input;
using ModFolder.Configs;
using ModFolder.UI.Base;
using ModFolder.UI.UIFolderItems;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;

namespace ModFolder.UI.Menu;

// TODO: 边缘虚化
public class UIFolderItemList : UIList {
    public UIFolderItemList() {
        ManualSortMethod = _ => { };
    }
    #region 使滚轮更加顺滑
    public void StopMoving() {
        scrollbarAim = 0;
    }
    // TODO: 滚动太上或太下时的缓动
    public override void ScrollWheel(UIScrollWheelEvent evt) {
        var scrollbar = _scrollbar;
        _scrollbar = null;
        base.ScrollWheel(evt);
        _scrollbar = scrollbar;
        if (_scrollbar != null) {
            scrollbarAim -= evt.ScrollWheelValue;
            // _scrollbar.ViewPosition -= evt.ScrollWheelValue;
        }
    }
    private float scrollbarAim;
    private void Update_Scrollbar() {
        if (_scrollbar == null || _scrollbar._isDragging) {
            scrollbarAim = 0;
            return;
        }

        if (CommonConfig.Instance.AutoMoveListWhenDragging && UIModFolderMenu.Instance.IsReadyToDrag) {
            float upDelta = _dimensions.Y + _dimensions.Height / 8 - Main.mouseY;
            if (upDelta > 0) {
                scrollbarAim -= upDelta / 5;
                Main.mouseY += (int)MathF.Round(upDelta / 5);
            }
            else {
                float downDelta = Main.mouseY - _dimensions.Y - _dimensions.Height * 7 / 8;
                if (downDelta > 0) {
                    scrollbarAim += downDelta / 5;
                    Main.mouseY -= (int)MathF.Round(downDelta / 5);
                }
            }
        }

        if (scrollbarAim == 0) {
            return;
        }
        var absAim = Math.Abs(scrollbarAim);
        var signAim = Math.Sign(scrollbarAim);
        float delta = absAim > 40 ? scrollbarAim * 0.15f : Math.Min(absAim / 8 + 1, absAim) * signAim;
        _scrollbar.ViewPosition += delta;
        if (signAim > 0 && _scrollbar.ViewPosition >= _scrollbar.MaxViewSize - _scrollbar.ViewSize) {
            scrollbarAim = 0;
        }
        else if (signAim < 0 && _scrollbar.ViewPosition <= 0) {
            scrollbarAim = 0;
        }
        else {
            scrollbarAim -= delta;
        }
    }
    #endregion
    #region Update
    public override void Update(GameTime gameTime) {
        Update_Scrollbar();
        base.Update(gameTime);
        Update_PageUpDownSupport();
    }
    #endregion
    #region PageUp / PageDown
    /// <summary>
    /// 改自 <see cref="UIModBrowser.PageUpDownSupport(UIList)"/>
    /// </summary>
    private void Update_PageUpDownSupport() {
        if (Main.inputText.IsKeyDown(Keys.PageDown) && !Main.oldInputText.IsKeyDown(Keys.PageDown)) {
            StopMoving();
            ViewPosition += _innerDimensions.Height;
        }

        if (Main.inputText.IsKeyDown(Keys.PageUp) && !Main.oldInputText.IsKeyDown(Keys.PageUp)) {
            StopMoving();
            ViewPosition -= _innerDimensions.Height;
        }
    }
    #endregion
    #region Recalculate
    public override void RecalculateChildren() {
        if (UIModFolderMenu.Instance.LayoutType == LayoutTypes.Stripe) {
            base.RecalculateChildren();
            return;
        }

        // 块状布局:
        foreach (var element in Elements) {
            element.Recalculate();
        }
        float top = 0;
        float endOffset = 0;
        bool firstFolderItem = true;
        float itemWidth = 96, itemHeight = 96;
        float listWidth = _dimensions.Width;
        float padding = ListPadding;
        int columnCount = 6;
        int currentColumnCount = 0;
        foreach (var item in _items) {
            var itemSize = item._dimensions;
            if (item is not UIFolderItem) {
                item.Top.Set(top, 0);
                item.Recalculate();
                top += itemSize.Height + padding;
                endOffset = -padding;
                continue;
            }
            if (firstFolderItem) {
                firstFolderItem = false;
                itemWidth = itemSize.Width;
                itemHeight = itemSize.Height;
                columnCount = ((int)((listWidth + padding) / (itemWidth + padding))).WithMin(1);
            }
            item.Top.Set(top, 0);
            item.Left.Set(((currentColumnCount * 2 - columnCount) * (itemWidth + padding) + padding) / 2, 0.5f);
            // totalWidth = columnCount * itemWidth + padding * (columnCount - 1) = (itemWidth + padding) * columnCount - padding
            // offset = currentColumnCount * (itemWidth + padding)
            // offset - totalWidth / 2 = (itemWidth + padding) * (currentColumnCount - columnCount / 2) + padding / 2
            item.Recalculate();
            currentColumnCount += 1;
            if (currentColumnCount >= columnCount) {
                currentColumnCount = 0;
                top += itemHeight + padding;
                endOffset = -padding;
            }
            else {
                endOffset = itemHeight;
            }
        }
        _innerListHeight = top + endOffset;
    }
    #endregion
}
