using ModFolder.UI.Base;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;

namespace ModFolder.UI.UIFolderItems;

partial class UIFolderItem {
    //TODO: 滚动显示 (左右移动) 可配置 (显示开头还是来回显示)
    public class UINamePanel : UIElement {
        public UIText Text => _text;
        public UIFocusInputTextFieldPro RenameText => _renameText;
        private readonly UIFolderItem _item;
        private readonly UIText _text;
        private readonly UIFocusInputTextFieldPro _renameText;
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

        private bool replaceToText;
        private bool replaceToRenameText;
        private bool directReplaceToRenameText;
        private void Draw_CheckReplace() {
            if (replaceToText) {
                replaceToText = false;
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
            replaceToText = true;
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

        public override void OnInitialize() {
            _text.SetText(_item.GetDisplayName());
            _renameText.HintText = _item.GetRenameHintText();
        }
        public override void Draw(SpriteBatch spriteBatch) {
            Draw_CheckReplace();
            Draw_ArrangeRecalculateChildren();
            base.Draw(spriteBatch);
        }
    }
}
