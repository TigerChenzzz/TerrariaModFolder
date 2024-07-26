using Terraria.GameInput;
using Terraria.UI;

namespace ModFolder.UI;

public class UIHorizontalList : UIElement {
    private class UIInnerList : UIElement {
        public override bool ContainsPoint(Vector2 point) => true;
        public override void DrawChildren(SpriteBatch spriteBatch) {
            var dim = Parent.GetDimensions();
            var left = dim.X;
            var right = dim.X + dim.Width;
            foreach (UIElement element in Elements) {
                var dim2 = element.GetDimensions();
                var left2 = dim2.X;
                var right2 = dim2.X + dim2.Width;
                if (left2 < right && right2 > left) {
                    element.Draw(spriteBatch);
                }
            }
        }
        public override Rectangle GetViewCullingArea() {
            return Parent.GetDimensions().ToRectangle();
        }
    }
    private readonly UIInnerList _innerList = new();
    public List<UIElement> Items => _innerList.Elements;
    public void MarkItemsModified() {
        foreach (var item in Items) {
            item.Parent = _innerList;
        }
        RecalculateChildren();
    }
    public float ListPadding = 5f;
    public int Count => Items.Count;
    public UIHorizontalList() {
        _innerList.OverflowHidden = false;
        _innerList.Width.Set(0f, 1f);
        _innerList.Height.Set(0f, 1f);
        OverflowHidden = true;
        Append(_innerList);
    }
    public float InnerListWidth { get; private set; }
    #region 增删项
    public void Add(UIElement item) {
        _innerList.Append(item);
        _innerList.Recalculate();
    }
    public void AddRange(IEnumerable<UIElement> items) {
        foreach (var item in items) {
            _innerList.Append(item);
        }
        _innerList.Recalculate();
    }
    public void Remove(UIElement item) {
        _innerList.RemoveChild(item);
    }
    public void Clear() {
        _innerList.RemoveAllChildren();
    }
    #endregion
    public override void RecalculateChildren() {
        base.RecalculateChildren();
        float totalWidth = 0;
        for (int i = 0; i < Items.Count; i++) {
            if (i != 0) {
                totalWidth += ListPadding;
            }
            Items[i].Left.Pixels = totalWidth;
            Items[i].Recalculate();
            totalWidth += Items[i].GetOuterDimensions().Width;
        }

        InnerListWidth = totalWidth;
    }
    #region 滚轮操作
    public float ViewPosition {
        get => -_innerList.Left.Pixels;
        set {
            var viewPosition = Math.Max(Math.Min(value, MaxViewPosition), 0);
            _innerList.Left.Pixels = -viewPosition;
            _innerList.Recalculate();
        }
    }
    public float MaxViewPosition => Math.Max(0, InnerListWidth - GetDimensions().Width);
    public override void ScrollWheel(UIScrollWheelEvent evt) {
        base.ScrollWheel(evt);
        scrollbarAim -= evt.ScrollWheelValue;
    }
    private float scrollbarAim;
    private void Update_Scrollbar() {
        if (scrollbarAim == 0) {
            return;
        }
        var absAim = Math.Abs(scrollbarAim);
        var signAim = Math.Sign(scrollbarAim);
        float delta = absAim > 40 ? scrollbarAim * 0.15f : Math.Min(absAim / 8 + 1, absAim) * signAim;
        ViewPosition += delta;
        if (signAim > 0 && ViewPosition >= MaxViewPosition) {
            scrollbarAim = 0;
        }
        else if (signAim < 0 && ViewPosition <= 0) {
            scrollbarAim = 0;
        }
        else {
            scrollbarAim -= delta;
        }
    }
    #endregion
    public event Action<SpriteBatch>? OnDraw;
    public override void DrawSelf(SpriteBatch spriteBatch) {
        if (ViewPosition > MaxViewPosition) {
            ViewPosition = MaxViewPosition;
        }
        OnDraw?.Invoke(spriteBatch);
    }
    public override void Update(GameTime gameTime) {
        Update_Scrollbar();
        base.Update(gameTime);
    }
    public override void MouseOver(UIMouseEvent evt) {
        base.MouseOver(evt);
        PlayerInput.LockVanillaMouseScroll("ModFolder/UIHorizontalList");
    }
}
