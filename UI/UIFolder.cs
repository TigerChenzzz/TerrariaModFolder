using ModFolder.Systems;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader.UI;
using Terraria.UI;

namespace ModFolder.UI;

/// <summary>
/// 文件夹系统列表中的一个文件夹
/// </summary>
public class UIFolder : UIFolderItem {
    public FolderDataSystem.FolderNode? Node;
    public string? Name { get; set; }
    // TODO
    public DateTime LastModified { get; set; }
    public override int CompareTo(object obj) {
        if (obj is UIModItemInFolder) {
            return -1;
        }
        if (obj is not UIFolder other) {
            return 1;
        }
        return UIModFolderMenu.Instance.sortMode switch {
            ModsMenuSortMode.RecentlyUpdated => other.LastModified.CompareTo(LastModified),
            ModsMenuSortMode.DisplayNameAtoZ => string.Compare(Name, other.Name, StringComparison.Ordinal),
            ModsMenuSortMode.DisplayNameZtoA => string.Compare(other.Name, Name, StringComparison.Ordinal),
            _ => base.CompareTo(obj),
        };
    }

    private UIImage _folderIcon = null!;
    private UIText _folderName = null!;
    private UIFocusInputTextFieldPro _renameText = null!;
    private UIImage _deleteButton =  null!;
    private UIImage? _renameButton;

    public UIFolder(FolderDataSystem.FolderNode folderNode) {
        Node = folderNode;
        Name = folderNode.FolderName;
    }
    public UIFolder(string name) {
        Name = name;
    }
    #region 名字与输入框之间的替换
    private void ReplaceChildren(UIElement from, UIElement to, bool forceAdd = true) {
        for (int i = 0; i < Elements.Count; ++i) {
            if (Elements[i] == from) {
                from.Parent = null;
                Elements[i] = to;
                to.Parent = this;
                to.Recalculate();
                return;
            }
        }
        if (forceAdd) {
            Append(to);
        }
    }
    private bool replaceToFolderName;
    private bool replaceToRenameText;
    public void SetReplaceToRenameText() => replaceToRenameText = true;
    private void CheckReplace() {
        if (replaceToFolderName) {
            replaceToFolderName = false;
            ReplaceChildren(_renameText, _folderName, false);
        }
        if (replaceToRenameText) {
            replaceToRenameText = false;
            _renameText.CurrentString = string.Empty;
            ReplaceChildren(_folderName, _renameText, false);
            _renameText.Focused = true;
        }
    }
    #endregion
    public override void OnInitialize() {
        #region 删除按钮
        int rightRowOffset = -30;
        _deleteButton = new UIImage(TextureAssets.Trash) {
            Width = { Pixels = 24 },
            Height = { Pixels = 24 },
            Left = { Pixels = rightRowOffset, Precent = 1 },
            Top = { Pixels = -12, Percent = 0.5f },
            ScaleToFit = true,
            AllowResizingDimensions = false,
        };
        rightRowOffset -= 24;
        _deleteButton.OnLeftClick += (_, _) => {
            // TODO: 未加载完时...
            // TODO: 删除的二次确认, 按住 Shift 时才直接删除
            // TODO: 直接取消订阅所有内含模组, 三次确认
            if (Node != null) {
                UIModFolderMenu.Instance.CurrentFolderNode.Children.Remove(Node);
                UIModFolderMenu.Instance.SetUpdateNeeded();
            }
        };
        Append(_deleteButton);
        #endregion
        #region 重命名按钮
        if (Node != null) {
            _renameButton = new UIImage(TextureAssets.Star[2]) {
                Width = { Pixels = 24 },
                Height = { Pixels = 24 },
                Left = { Pixels = rightRowOffset - 2, Precent = 1 },
                Top = { Pixels = -12, Percent = 0.5f },
                ScaleToFit = true,
                AllowResizingDimensions = false,
            };
            _renameButton.OnLeftClick += (_, _) => {
                replaceToRenameText = true;
            };
            Append(_renameButton);
        }
        rightRowOffset -= 24;
        #endregion
        #region 文件夹图标
        _folderIcon = new(UICommon.ButtonOpenFolder) {
            Left = { Pixels = 1 },
            Top = { Pixels = 1 },
            Width = { Pixels = 28 },
            Height = { Pixels = 28 },
            ScaleToFit = true,
            AllowResizingDimensions = false,
        };
        Append(_folderIcon);
        #endregion
        #region 名称
        _folderName = new(Name ?? string.Empty);
        _folderName.Left.Pixels = 30;
        _folderName.Top.Pixels = 7;
        Append(_folderName);
        #endregion
        #region 重命名输入框
        // TODO: 本地化
        _renameText = new("新名字");
        _renameText.Left.Pixels = 30;
        _renameText.Top.Pixels = 5;
        _renameText.Height.Set(-5, 1);
        _renameText.Width.Set(-30 + rightRowOffset, 1);
        _renameText.OnUnfocus += (_, _) => {
            var newName = _renameText.CurrentString;
            // TODO: 更加完备的新名字检测 (可能需要保存父节点?)
            if (Node == null || newName == ".." || newName == string.Empty) {
                replaceToFolderName = true;
                return;
            }
            Node.FolderName = newName;
            Name = newName;
            _folderName.SetText(newName);
            replaceToFolderName = true;
        };
        _renameText.UnfocusOnTab = true;
        #endregion
        #region 双击进入文件夹
        // TODO: 双击某些位置时不进入文件夹 / 测试
        OnLeftDoubleClick += (_, target) => {
            if (Name == null) {
                return;
            }
            if (target == _renameText || target == _deleteButton || target == _renameButton) {
                return;
            }
            if (Name == "..") {
                UIModFolderMenu.Instance.GotoUpperFolder();
            }
            UIModFolderMenu.Instance.EnterFolder(Name);
        };
        #endregion
    }

    private string? _tooltip;
    public override void Update(GameTime gameTime) {
        base.Update(gameTime);
    }
    public override void DrawSelf(SpriteBatch spriteBatch) {
        base.DrawSelf(spriteBatch);
        CheckReplace();
        // TODO: 状态显示: 全启用 / 部分启用 / 全禁用 / 待启用 / 待禁用 / 待启用及禁用
        // TODO: 悬浮提示中显示详细状态 : 启用状态, 待启用数, 待禁用数
        // TODO: 配置是否显示状态 (因为数据庞大时可能会影响性能)
        #region 当鼠标在某些东西上时显示些东西
        // 更多信息按钮
        // 删除按钮
        if (_deleteButton.IsMouseHovering) {
            _tooltip = Language.GetTextValue("UI.Delete");
        }
        else if (_renameButton?.IsMouseHovering == true) {
            // TODO: 本地化
            _tooltip = "重命名";
        }
        #endregion
    }
    public override void Draw(SpriteBatch spriteBatch) {
        _tooltip = null;
        base.Draw(spriteBatch);
        if (!string.IsNullOrEmpty(_tooltip)) {
            UICommon.TooltipMouseText(_tooltip);
        }
    }
}
