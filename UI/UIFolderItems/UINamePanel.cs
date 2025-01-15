using ModFolder.Configs;
using ModFolder.UI.Base;
using ModFolder.UI.Menu;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;
using Terraria.UI.Chat;

namespace ModFolder.UI.UIFolderItems;

partial class UIFolderItem {
    //TODO: 滚动显示 (左右移动) 可配置 (显示开头还是来回显示)
    public class UINamePanel : UIElement {
        public UIText Text => _text;
        public UIFocusInputTextFieldPro RenameText => _renameText;
        private readonly UIFolderItem _item;
        private readonly UIText _text;
        private readonly UIFocusInputTextFieldPro _renameText;
        private readonly UIElement _placeHolder = GetAPlaceHolderElement();
        private UIElement Current => Elements[0];
        public UINamePanel(UIFolderItem item) {
            _item = item;
            OverflowHidden = true;

            _text = new(string.Empty) {
                Height = new(0, 1),
                TextOriginY = 0.5f,
            };
            Append(_text);
            _renameText = new(string.Empty) {
                Top = new(6, 0),
                Height = new(-6, 1),
                Width = new(0, 1),
                UnfocusOnTab = true,
            };
            _renameText.OnUnfocus += OnUnfocus_TryRename;
        }
        public void AttachRenameButtton(UIElement renameButton)
            => renameButton.OnLeftClick += (_, _) => Rename();
        public void Rename() => replaceToRenameText = true;
        /// <summary>
        /// 不会设置 CurrentString
        /// </summary>
        public void DirectlyRename() => directReplaceToRenameText = true;
        public void SetToPlaceHolderS() {
            if (Current != _renameText) {
                replaceToPlaceHolder = true;
                return;
            }
            shouldBePlaceHolder = true;
        }
        public void SetToTextS() {
            if (Current != _renameText) {
                replaceToText = true;
                return;
            }
            shouldBePlaceHolder = false;
        }
        public void SetToPlaceHolderF() {
            replaceToPlaceHolder = true;
        }
        public void SetToTextF() {
            replaceToText = true;
        }

        private bool shouldBePlaceHolder;
        private bool replaceToPlaceHolder;
        private bool replaceToText;
        private bool replaceToRenameText;
        private bool directReplaceToRenameText;
        private void Draw_CheckReplace() {
            if (replaceToPlaceHolder) {
                replaceToPlaceHolder = false;
                shouldBePlaceHolder = true;
                this.ReplaceChildrenByIndex(0, _placeHolder);
            }
            if (replaceToText) {
                replaceToText = false;
                shouldBePlaceHolder = false;
                this.ReplaceChildrenByIndex(0, _text);
                UpdateText();
            }
            if (replaceToRenameText) {
                replaceToRenameText = false;
                _renameText.HintText = _item.GetRenameHintText();
                _renameText.CurrentString = _item.GetRenameText();
                this.ReplaceChildrenByIndex(0, _renameText);
                _renameText.Focused = true;
            }
            if (directReplaceToRenameText) {
                directReplaceToRenameText = false;
                _renameText.HintText = _item.GetRenameHintText();
                _renameText.CurrentString = string.Empty;
                this.ReplaceChildrenByIndex(0, _renameText);
                _renameText.Focused = true;
            }
        }
        private void OnUnfocus_TryRename(object sender, EventArgs e) {
            var newName = _renameText.CurrentString;
            if (shouldBePlaceHolder) {
                replaceToPlaceHolder = true;
            }
            else {
                replaceToText = true;
            }
            if (_item.TryRename(newName)) {
                UpdateText();
            }
        }
        private void UpdateText() => UpdateText(_item.GetDisplayName());
        private void UpdateText(string? name) {
            if (_text.Text == name) {
                return;
            }
            _text.SetText(name);
            ArrangeRecalculateChildren();
        }

        private bool _arrangeRecalculateChildren;
        private void ArrangeRecalculateChildren() => _arrangeRecalculateChildren = true;
        private void Draw_ArrangeRecalculateChildren() {
            if (_arrangeRecalculateChildren) {
                _arrangeRecalculateChildren = false;
                RecalculateChildren();
            }
        }

        private readonly int scrollingRandomStart = Main.rand.Next();
#if DEBUG
        private static int ScrollingStop => 40;
        private static float ScrollingSpeed => .5f;
#else
        private const int ScrollingStop = 40;
        private const float ScrollingSpeed = .5f;
#endif
        private void Draw_AdjustTextPosition() {
            if (Current == _renameText) {
                if (_item.StripeLayout) {
                    _renameText.TextXAlign = 0;
                }
                else {
                    _renameText.TextXAlign = 0.5f;
                }
                return;
            }
            else {
                var textSize = _text.MinWidth.Pixels;
                var width = _dimensions.Width;
                float left;
                if (width >= textSize) {
                    if (_item.StripeLayout) {
                        left = 0;
                    }
                    else {
                        left = (width - textSize) / 2;
                    }
                }
                else if (!CommonConfig.Instance.ScrollingName) {
                    left = 0;
                }
                else {
                    left = width - textSize;
                    var timer = UIModFolderMenu.Instance.Timer + scrollingRandomStart;
                    var moveTime = (int)Math.Ceiling(-left / ScrollingSpeed);
                    var period = 2 * ScrollingStop + 2 * moveTime;
                    timer = Modular(timer, period) - ScrollingStop;
                    if (timer <= 0) {
                        left = 0;
                    }
                    else if (timer <= moveTime) {
                        left = -timer * ScrollingSpeed;
                    }
                    else if ((timer -= moveTime + ScrollingStop) > 0) {
                        left += timer * ScrollingSpeed;
                    }
                }
                if (_text.Left.Pixels != left) {
                    _text.Left.Pixels = left;
                    ArrangeRecalculateChildren();
                }
            }
        }

        public override void OnInitialize() {
            _text.SetText(_item.GetDisplayName());
            _renameText.HintText = _item.GetRenameHintText();
        }
        public override void Draw(SpriteBatch spriteBatch) {
            Draw_CheckReplace();
            Draw_AdjustTextPosition();
            Draw_ArrangeRecalculateChildren();
            base.Draw(spriteBatch);
        }
    }
}
