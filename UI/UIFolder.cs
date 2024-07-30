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
    public override FolderItemTypeEnum FolderItemType => FolderItemTypeEnum.Folder;
    public FolderDataSystem.FolderNode? FolderNode;
    public override FolderDataSystem.Node? Node => FolderNode;
    public string Name { get; set; }
    public override string NameToSort => Name;
    // TODO
    public override DateTime LastModified => base.LastModified;
    private UIImage _folderIcon = null!;
    private UIText _folderName = null!;
    private UIFocusInputTextFieldPro _renameText = null!;
    private UIImage? _deleteButton;
    private UIImage? _renameButton;

    public UIFolder(FolderDataSystem.FolderNode folderNode) {
        FolderNode = folderNode;
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
    private bool directlyReplaceToRenameText;
    public void SetReplaceToRenameText() => replaceToRenameText = true;
    private void CheckReplace() {
        if (replaceToFolderName) {
            replaceToFolderName = false;
            ReplaceChildren(_renameText, _folderName, false);
        }
        if (replaceToRenameText) {
            replaceToRenameText = false;
            _renameText.CurrentString = _folderName.Text;
            ReplaceChildren(_folderName, _renameText, false);
            _renameText.Focused = true;
        }
        if (directlyReplaceToRenameText) {
            directlyReplaceToRenameText = false;
            _renameText.CurrentString = string.Empty;
            ReplaceChildren(_folderName, _renameText, false);
            _renameText.Focused = true;
        }
        }
    public void DirectlyReplaceToRenameText() => directlyReplaceToRenameText = true;
    #endregion
    public override void OnInitialize() {
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
        // _folderName.Top.Pixels = 7;
        _folderName.VAlign = 0.5f;
        Append(_folderName);
        #endregion
        #region 删除按钮
        int rightRowOffset = -30;
        if (FolderNode != null) {
            _deleteButton = new UIImage(Textures.ButtonDelete) {
                Width = { Pixels = 24 },
                Height = { Pixels = 24 },
                Left = { Pixels = rightRowOffset, Precent = 1 },
                Top = { Pixels = -12, Percent = 0.5f },
                ScaleToFit = true,
                AllowResizingDimensions = false,
            };
            _deleteButton.OnLeftClick += (_, _) => {
                // TODO: 未加载完时...
                // TODO: 删除的二次确认, 按住 Shift 时才直接删除
                // TODO: 直接取消订阅所有内含模组, 三次确认
                if (FolderNode != null) {
                    UIModFolderMenu.Instance.CurrentFolderNode.Children.Remove(FolderNode);
                    UIModFolderMenu.Instance.ArrangeGenerate();
                }
            };
            Append(_deleteButton);
        }
        rightRowOffset -= 24;
        #endregion
        #region 重命名按钮
        if (FolderNode != null) {
            _renameButton = new UIImage(Textures.ButtonRename) {
                Width = { Pixels = 24 },
                Height = { Pixels = 24 },
                Left = { Pixels = rightRowOffset, Precent = 1 },
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
        #region 重命名输入框
        _renameText = new(ModFolder.Instance.GetLocalization("UI.NewFolderDefaultName").Value) {
            Left = { Pixels = 30 },
            Top = { Pixels = 5 },
            Height = { Pixels = -5, Percent = 1 },
            Width = { Pixels = -30 + rightRowOffset, Percent = 1 },
            UnfocusOnTab = true,
        };
        _renameText.OnUnfocus += OnUnfocus_TryRename;
        #endregion
        #region 双击进入文件夹
        // TODO: 双击某些位置时不进入文件夹 / 测试
        OnLeftDoubleClick += (e, target) => {
            if (Name == "..") {
                UIModFolderMenu.Instance.GotoUpperFolder();
                return;
            }
            if (FolderNode == null) {
                return;
            }
            if (e.Target == _renameText || e.Target == _deleteButton || e.Target == _renameButton) {
                return;
            }
            UIModFolderMenu.Instance.EnterFolder(FolderNode);
        };
        #endregion
    }

    private void OnUnfocus_TryRename(object sender, EventArgs e) {
        var newName = _renameText.CurrentString;
        // TODO: 更加完备的新名字检测 (可能需要保存父节点?)
        if (FolderNode == null || newName == ".." || newName == string.Empty) {
            replaceToFolderName = true;
            return;
        }
        FolderNode.FolderName = newName;
        Name = newName;
        _folderName.SetText(newName);
        replaceToFolderName = true;
        UIModFolderMenu.Instance.ArrangeGenerate();
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
        if (_deleteButton?.IsMouseHovering == true) {
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
