using Microsoft.Xna.Framework.Input;
using ModFolder.Configs;
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
    public override void Update(GameTime gameTime) {
        Update_Scrollbar();
        base.Update(gameTime);
        Update_PageUpDownSupport();
    }
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
}
