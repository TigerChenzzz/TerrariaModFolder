using Humanizer;
using ModFolder.Configs;
using ModFolder.Systems;
using System.Text;
using Terraria.Audio;
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
    private int _folderNameIndex;
    private UIFocusInputTextFieldPro _renameText = null!;
    private UIImage? _deleteButton;
    private UIImage? _renameButton;
    private UIText _enableStatusText = null!;

    public UIFolder(FolderDataSystem.FolderNode folderNode) {
        FolderNode = folderNode;
        Name = folderNode.FolderName;
    }
    public UIFolder(string name) {
        Name = name;
    }
    #region 名字与输入框之间的替换
    private bool replaceToFolderName;
    private bool replaceToRenameText;
    private bool directlyReplaceToRenameText;
    public void SetReplaceToRenameText() => replaceToRenameText = true;
    private void CheckReplace() {
        if (replaceToFolderName) {
            replaceToFolderName = false;
            this.ReplaceChildrenByIndex(_folderNameIndex, _folderName);
        }
        if (replaceToRenameText) {
            replaceToRenameText = false;
            _renameText.CurrentString = _folderName.Text;
            this.ReplaceChildrenByIndex(_folderNameIndex, _renameText);
            _renameText.Focused = true;
        }
        if (directlyReplaceToRenameText) {
            directlyReplaceToRenameText = false;
            _renameText.CurrentString = string.Empty;
            this.ReplaceChildrenByIndex(_folderNameIndex, _renameText);
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
        _folderName.Left.Pixels = 35;
        // _folderName.Top.Pixels = 7;
        _folderName.VAlign = 0.5f;
        _folderNameIndex = this.AppendAndGetIndex(_folderName);
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
            _deleteButton.OnLeftClick += QuickFolderDelete;
            Append(_deleteButton);
        }
        #endregion
        #region 重命名按钮
        rightRowOffset -= 24;
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
        #endregion
        #region 启用状态
        rightRowOffset -= 10;
        _enableStatusText = new(string.Empty) {
            Left = { Pixels = rightRowOffset, },
            VAlign = 0.5f,
            HAlign = 1,
            TextOriginX = 1,
        };
        Append(_enableStatusText);
        #endregion
        #region 重命名输入框
        _renameText = new(ModFolder.Instance.GetLocalization("UI.NewFolderDefaultName").Value) {
            Left = { Pixels = 35 },
            Top = { Pixels = 5 },
            Height = { Pixels = -5, Percent = 1 },
            Width = { Pixels = -35 + rightRowOffset, Percent = 1 },
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
    #region 删除
    private void QuickFolderDelete(UIMouseEvent evt, UIElement listeningElement) {
        bool loaded = UIModFolderMenu.Instance.Loaded;

        if (!CommonConfig.Instance.AlwaysNeedConfirmWhenDeletingFolder && (Main.keyState.PressingShift() || Main.keyState.PressingControl() || Main.keyState.PressingAlt())) {
            DeleteFolder(loaded, true);
            return;
        }

        SoundEngine.PlaySound(SoundID.MenuOpen);
        var _deleteModDialog = new UIPanel() {
            Width = { Pixels = 440 },
            Height = { Pixels = 300 },
            HAlign = .5f,
            VAlign = .5f,
            BackgroundColor = new Color(63, 82, 151),
            BorderColor = Color.Black
        };
        _deleteModDialog.SetPadding(6f);
        UIModFolderMenu.Instance.AppendConfirmPanel(_deleteModDialog);

        #region 按钮是
        var _dialogYesButton = new UIAutoScaleTextTextPanel<string>(Language.GetTextValue(loaded ? "LegacyMenu.105" : "LegacyMenu.104")) {
            TextColor = Color.White,
            Width = new StyleDimension(-10f, 1f / 3f),
            Height = { Pixels = 40 },
            VAlign = .85f,
            HAlign = .15f
        }.WithFadedMouseOver();
        if (loaded) {
            _dialogYesButton.OnUpdate += _ => {
                if (Main.keyState.PressingControl() || Main.keyState.PressingShift() || Main.keyState.PressingAlt()) {
                    _dialogYesButton.SetText(Language.GetTextValue("LegacyMenu.104"));
                }
                else {
                    _dialogYesButton.SetText(Language.GetTextValue("LegacyMenu.105"));
                }
            };
        }
        _dialogYesButton.OnLeftClick += (_, _) => DeleteFolder(loaded);
        _deleteModDialog.Append(_dialogYesButton);
        #endregion
        #region 按钮否
        var _dialogNoButton = new UIAutoScaleTextTextPanel<string>(Language.GetTextValue("LegacyMenu.105")) {
            TextColor = Color.White,
            Width = new StyleDimension(-10f, 1f / 3f),
            Height = { Pixels = 40 },
            VAlign = .85f,
            HAlign = .85f
        }.WithFadedMouseOver();
        _dialogNoButton.OnLeftClick += (_, _) => UIModFolderMenu.Instance.RemoveConfirmPanel();
        _deleteModDialog.Append(_dialogNoButton);
        #endregion

        string tip = loaded
            ? ModFolder.Instance.GetLocalization("UI.DeleteFolderConfirmText").Value
            : ModFolder.Instance.GetLocalization("UI.DeleteFolderUnloadedConfirmText").Value;
        var _dialogText = new UIText(tip) {
            Width = { Percent = .85f },
            HAlign = .5f,
            VAlign = .3f,
            IsWrapped = true,
        };
        _deleteModDialog.Append(_dialogText);

        UIModFolderMenu.Instance.Recalculate();
    }
    
    private void DeleteFolder(bool loaded, bool quick = false) {
        if (!loaded) {
            DeleteFolderInner(Main.keyState.PressingAlt());
            if (!quick) {
                UIModFolderMenu.Instance.RemoveConfirmPanel();
            }
            return;
        }
        if (UIModFolderMenu.Instance.ShowAllMods) {
            return;
        }
        if (Main.keyState.PressingControl()) {
            UnsubscribeAllDoubleConfirm();
            return;
        }
        bool alt = Main.keyState.PressingAlt();
        if (Main.keyState.PressingShift() || alt) {
            DeleteFolderInner(alt);
        }
        if (!quick) {
            UIModFolderMenu.Instance.RemoveConfirmPanel();
        }
    }
    private void DeleteFolderInner(bool alt) {
        if (FolderNode != null) {
            if (alt) {
                FolderNode.Crash();
            }
            else {
                FolderNode.Parent = null;
            }
            FolderDataSystem.TrySaveWhenChanged();
            UIModFolderMenu.Instance.ArrangeGenerate();
        }
    }
    private void UnsubscribeAllDoubleConfirm() {
        bool shift = Main.keyState.PressingShift();
        bool alt = Main.keyState.PressingAlt();
        var doubleConfirmDialog = new UIPanel() {
            Width = { Pixels = 400 },
            Height = { Pixels = 280 },
            HAlign = .5f,
            VAlign = .5f,
            BackgroundColor = new Color(63, 82, 151),
            BorderColor = Color.Black
        };
        doubleConfirmDialog.SetPadding(6);
        UIModFolderMenu.Instance.AppendConfirmPanel(doubleConfirmDialog);
        #region 按钮是
        var _dialogYesButton = new UIAutoScaleTextTextPanel<string>(Language.GetTextValue("LegacyMenu.104")) {
            TextColor = Color.White,
            Width = new StyleDimension(-10f, 1f / 3f),
            Height = { Pixels = 40 },
            VAlign = .85f,
            HAlign = .85f
        }.WithFadedMouseOver();
        _dialogYesButton.OnLeftClick += (_, _) => UnsubscribeAll(shift, alt);
        doubleConfirmDialog.Append(_dialogYesButton);
        #endregion
        #region 按钮否
        var _dialogNoButton = new UIAutoScaleTextTextPanel<string>(Language.GetTextValue("LegacyMenu.105")) {
            TextColor = Color.White,
            Width = new StyleDimension(-10f, 1f / 3f),
            Height = { Pixels = 40 },
            VAlign = .85f,
            HAlign = .15f
        }.WithFadedMouseOver();
        _dialogNoButton.OnLeftClick += (_, _) => UIModFolderMenu.Instance.RemoveConfirmPanel();
        doubleConfirmDialog.Append(_dialogNoButton);
        #endregion
        string tip = ModFolder.Instance.GetLocalization("UI.DeleteFolderDoubleConfirmText").Value;
        var _dialogText = new UIText(tip) {
            Width = { Percent = .85f },
            HAlign = .5f,
            VAlign = .3f,
            IsWrapped = true,
        };
        doubleConfirmDialog.Append(_dialogText);

        UIModFolderMenu.Instance.Recalculate();
    }
    private void UnsubscribeAll(bool shift, bool alt) {
        if (FolderNode == null) {
            return;
        }
        foreach (var modNode in FolderNode.ModNodesInTree.ToHashSet()) {
            if (!UIModFolderMenu.Instance.ModItemDict.TryGetValue(modNode.ModName, out var uimod)) {
                continue;
            }
            UIModFolderMenu.Instance.ArrangeDeleteMod(uimod);
        }
        if (shift || alt) {
            DeleteFolderInner(alt);
        }
        UIModFolderMenu.Instance.ClearConfirmPanels();
    }
    #endregion

    private void OnUnfocus_TryRename(object sender, EventArgs e) {
        var newName = _renameText.CurrentString;
        // TODO: 更加完备的新名字检测 (可能需要保存父节点?)
        if (FolderNode == null || newName == ".." || newName == string.Empty) {
            replaceToFolderName = true;
            return;
        }
        if (Name == newName) {
            return;
        }
        FolderNode.FolderName = newName;
        Name = newName;
        _folderName.SetText(newName);
        replaceToFolderName = true;
        UIModFolderMenu.Instance.ArrangeGenerate();
        FolderDataSystem.TrySaveWhenChanged();
    }

    private string? _tooltip;
    public override void Update(GameTime gameTime) {
        base.Update(gameTime);
    }
    #region Draw
    public override void DrawSelf(SpriteBatch spriteBatch) {
        base.DrawSelf(spriteBatch);
        DrawEnableStatus(spriteBatch);
        UpdateEnableStatusText();
        CheckReplace();
        #region 当鼠标在某些东西上时显示些东西
        // 更多信息按钮
        // 删除按钮
        if (_deleteButton?.IsMouseHovering == true) {
            _tooltip = Language.GetTextValue("UI.Delete");
        }
        else if (_renameButton?.IsMouseHovering == true) {
            _tooltip = ModFolder.Instance.GetLocalization("UI.Rename").Value;
        }
        else if (CommonConfig.Instance.ShowEnableStatusText.ShowAny && FolderNode != null && _enableStatusText.IsMouseHovering ||
            !CommonConfig.Instance.ShowEnableStatusText.ShowAny && CommonConfig.Instance.ShowEnableStatusBackground && FolderNode != null && IsMouseHovering) {
            _tooltip = ModFolder.Instance.GetLocalization("UI.FolderEnableStatus").Value.FormatWith(FolderNode.ChildrenCount, FolderNode.EnabledCount, FolderNode.ToEnableCount, FolderNode.ToDisableCount);
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
    private readonly int randomStartOffset = Main.rand.Next(0, 10000);
    private void UpdateEnableStatusText() {
        var config = CommonConfig.Instance.ShowEnableStatusText;
        if (FolderNode == null) {
            return;
        }
        if (!config.ShowAny) {
            _enableStatusText.SetText(string.Empty);
            return;
        }
        StringBuilder sb = new();
        bool slashed = false;
        if (config.AllMods.Check(FolderNode.ChildrenCount)) {
            sb.AppendFormat("[c/{0}:{1}]", config.AllModsColor.Hex3(), FolderNode.ChildrenCount);
            slashed = true;
        }
        
        if (config.Enabled.Check(FolderNode.EnabledCount)) {
            if (slashed) {
                sb.Append(config.Seperator);
            }
            else {
                slashed = true;
            }
            sb.AppendFormat("[c/{0}:{1}]", config.EnabledColor.Hex3(), FolderNode.EnabledCount);
        }
        if (config.ToEnable.Check(FolderNode.ToEnableCount)) {
            if (slashed) {
                sb.Append(config.Seperator);
            }
            else {
                slashed = true;
            }
            sb.AppendFormat("[c/{0}:{1}]", config.ToEnableColor.Hex3(), FolderNode.ToEnableCount);
        }
        if (config.ToDisable.Check(FolderNode.ToDisableCount)) {
            if (slashed) {
                sb.Append(config.Seperator);
            }
            else {
                slashed = true;
            }
            sb.AppendFormat("[c/{0}:{1}]", config.ToDisableColor.Hex3(), FolderNode.ToDisableCount);
        }
        if (!slashed) {
            _enableStatusText.SetText(string.Empty);
            return;
        }
        _enableStatusText.SetText(sb.ToString());
    }
    private void DrawEnableStatus(SpriteBatch spriteBatch) {
        if (!CommonConfig.Instance.ShowEnableStatusBackground) {
            return;
        }
        if (FolderNode == null) {
            return;
        }
        if (FolderNode.ChildrenCount == 0) {
            return;
        }
        var rect = _dimensions.ToRectangle();
        int width = rect.Width;
        int height = rect.Height;
        if (width < height || height < 2) {
            return;
        }
        int countNow = FolderNode.EnabledCount - FolderNode.ToDisableCount;
        int enableWidth = (int)(width * (countNow / (float)FolderNode.ChildrenCount));
        countNow += FolderNode.ToEnableCount;
        int toEnableWidth = (int)(width * (countNow / (float)FolderNode.ChildrenCount));
        countNow += FolderNode.ToDisableCount;
        int toDisableWidth = (int)(width * (countNow / (float)FolderNode.ChildrenCount)) - toEnableWidth;
        toEnableWidth -= enableWidth;
        int minWidth = 5;
        if (FolderNode.EnabledCount - FolderNode.ToDisableCount > 0 && enableWidth < minWidth) {
            enableWidth = minWidth;
        }
        if (FolderNode.ToEnableCount > 0 && toEnableWidth < minWidth) {
            toEnableWidth = minWidth;
        }
        if (FolderNode.ToDisableCount > 0 &&  toDisableWidth < minWidth) {
            toDisableWidth = minWidth;
        }
        int start = UIModFolderMenu.Instance.Timer - UIModFolderMenu.Instance.Timer / 3 + randomStartOffset;
        DrawEnableStatus_Single(spriteBatch, rect, start, start + enableWidth, EnabledBorderColor, EnabledInnerColor);
        DrawEnableStatus_Single(spriteBatch, rect, start + enableWidth, start + enableWidth + toEnableWidth, ToEnableBorderColor, ToEnableInnerColor);
        DrawEnableStatus_Single(spriteBatch, rect, start + enableWidth + toEnableWidth, start + enableWidth + toEnableWidth + toDisableWidth, ToDisableBorderColor, ToDisableInnerColor);
    }

    private readonly static Dictionary<int, Texture2D> _slashTextures = [];
    private static Texture2D GetSlashTexture(int size) {
        if (_slashTextures.TryGetValue(size, out var result)) {
            return result;
        }
        Color[] colors = new Color[size * size];
        for (int i = 1; i <= size; ++i) {
            colors[(size - 1) * i] = Color.White;
        }
        result = Textures.FromColors(size, size, colors);
        _slashTextures.Add(size, result);
        return result;
    }
    private static void DrawEnableStatus_Single(SpriteBatch spriteBatch, Rectangle rect, int start, int end, Color borderColor, Color innerColor) {
        int startOrigin = start;
        start %= rect.Width;
        if (start < 0) {
            start += rect.Width;
        }
        end = end + start - startOrigin;
        if (end <= start) {
            return;
        }
        #region 下边框
        if (start >= rect.Height - 1) {
            if (end < rect.Width + rect.Height) {
                // 两边都在边界内
                spriteBatch.Draw(Textures.White, new Rectangle(rect.X + start - rect.Height + 1, rect.Bottom - 1, end - start, 1), borderColor);
            }
            else {
                // 右边超界
                spriteBatch.Draw(Textures.White, new Rectangle(rect.X + start - rect.Height + 1, rect.Bottom - 1, rect.Right - (rect.X + start - rect.Height + 1), 1), borderColor);
                spriteBatch.Draw(Textures.White, new Rectangle(rect.Left, rect.Bottom - 1, end - rect.Width - rect.Height + 1, 1), borderColor);
            }
        } 
        else /*if (end < rect.Width + rect.Height)*/ {
            // 左边超界
            if (end < rect.Height) {
                // 右边不足时
                spriteBatch.Draw(Textures.White, new Rectangle(rect.Right + start - rect.Height, rect.Bottom - 1, end - start, 1), borderColor);
            }
            else {
                // 右边跨界时
                spriteBatch.Draw(Textures.White, new Rectangle(rect.Left, rect.Bottom - 1, end - rect.Height + 1, 1), borderColor);
                spriteBatch.Draw(Textures.White, new Rectangle(rect.Right + start - rect.Height, rect.Bottom - 1, - start + rect.Height, 1), borderColor);
            }
        }
        #endregion
        if (end > rect.Width) {
            #region 上边框
            spriteBatch.Draw(Textures.White, new Rectangle(rect.X + start, rect.Y, rect.Width - start, 1), borderColor);
            spriteBatch.Draw(Textures.White, new Rectangle(rect.X, rect.Y, end - rect.Width, 1), borderColor);
            #endregion
            end -= rect.Width;
            for (int i = 0; i < end; ++i) {
                DrawEnableStatus_SingleSlash(spriteBatch, rect, i, borderColor, innerColor);
            }
            end = rect.Width;
        }
        else {
            spriteBatch.Draw(Textures.White, new Rectangle(rect.X + start, rect.Y, end - start, 1), borderColor);
        }
        for (int i = start; i < end; ++i) {
            DrawEnableStatus_SingleSlash(spriteBatch, rect, i, borderColor, innerColor);
        }
    }
    private static void DrawEnableStatus_SingleSlash(SpriteBatch spriteBatch, Rectangle rect, int position, Color borderColor, Color innerColor) {
        if (position >= rect.Height - 1) {
            spriteBatch.Draw(GetSlashTexture(rect.Height - 2), new Rectangle(rect.X + position - rect.Height + 2, rect.Y + 1, rect.Height - 2, rect.Height - 2), innerColor);
            return;
        }
        var slash = GetSlashTexture(rect.Height - 2);
        if (position >= 1) {
            // 左边的边界点
            spriteBatch.Draw(Textures.White, new Rectangle(rect.X, rect.Y + position, 1, 1), borderColor);
            if (position >= 2) {
                // 左边的斜杠
                spriteBatch.Draw(slash, new Rectangle(rect.X + 1, rect.Y + 1, position - 1, rect.Height - 2), new Rectangle(rect.Height - 2 - position + 1, 0, position - 1, rect.Height - 2), innerColor);
            }
        }
        if (position <=  rect.Height - 3) {
            // 右边的边界点
            spriteBatch.Draw(Textures.White, new Rectangle(rect.Right - 1, rect.Y + position + 1, 1, 1), borderColor);
            if (position <= rect.Height - 4) {
                // 右边的斜杠
                spriteBatch.Draw(slash, new Rectangle(rect.Right + position - rect.Height + 2, rect.Y + 1, rect.Height - position - 3, rect.Height - 2), new Rectangle(0, 0, rect.Height - position - 3, rect.Height - 2), innerColor);
            }
        }
    }
    #endregion
}
