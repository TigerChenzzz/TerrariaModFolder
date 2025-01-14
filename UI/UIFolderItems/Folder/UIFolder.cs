using Humanizer;
using ModFolder.Configs;
using ModFolder.Helpers;
using ModFolder.Systems;
using ModFolder.UI.Base;
using ModFolder.UI.Menu;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Terraria.Audio;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader.UI;
using Terraria.UI;

namespace ModFolder.UI.UIFolderItems.Folder;

/// <summary>
/// 文件夹系统列表中的一个文件夹
/// </summary>
public class UIFolder : UIFolderItem {
    public override FolderItemTypeEnum FolderItemType => FolderItemTypeEnum.Folder;
    /// <summary>
    /// 当此文件夹是返回上一级的文件夹时此值为空
    /// </summary>
    public FolderDataSystem.FolderNode? FolderNode;
    /// <summary>
    /// 当此文件夹是返回上一级的文件夹时此值为空
    /// </summary>
    public override FolderDataSystem.Node? Node => FolderNode;
    public string FolderName { get; set; }
    public override string NameToSort => FolderName;
    public override DateTime LastModified => FolderNode?.LastModified ?? default;
    private readonly UIImage _folderIcon = new UIImage(UICommon.ButtonOpenFolder).SettleCommonly(); // 22 x 22
    private readonly UIText _enableStatusText = new(string.Empty) {
        VAlign = 0.5f,
        HAlign = 1,
        TextOriginX = 1,
    };

    public UIFolder(FolderDataSystem.FolderNode folderNode) {
        FolderNode = folderNode;
        FolderName = folderNode.FolderName ?? string.Empty;
    }
    public UIFolder(string name) {
        FolderName = name;
    }
    #region 名字与输入框之间的替换
    public void DirectlyRename() => _name.DirectlyRename();
    #endregion
    public override void OnInitialize() {
        #region 文件夹图标
        Append(_folderIcon);
        #endregion
        #region 名称
        OnInitialize_Name();
        #endregion
        #region 右边的按钮
        #region 删除按钮
        if (FolderNode != null) {
            var deleteButton = DeleteButton = NewRightButton(MTextures.ButtonDelete);
            deleteButton.OnLeftClick += QuickFolderDelete;
            mouseOverTooltips.Add((deleteButton, () => Language.GetTextValue("UI.Delete")));
        }
        #endregion
        #region 重命名按钮
        if (FolderNode != null) {
            var renameButton = RenameButton = NewRightButton(MTextures.ButtonRename);
            _name.AttachRenameButtton(renameButton);
            mouseOverTooltips.Add((renameButton, () => ModFolder.Instance.GetLocalizedValue("UI.Rename")));
        }
        #endregion
        #region 导出按钮
        if (FolderNode != null) {
            var exportButton = ExportButton = NewRightButton(MTextures.ButtonExport);
            exportButton.OnLeftClick += (_, _) => {
                SoundEngine.PlaySound(SoundID.MenuTick);
                ShareHelper.Export(FolderNode, !Main.keyState.PressingShift(), Main.keyState.PressingControl(), Main.keyState.PressingAlt());
                UIModFolderMenu.PopupInfoByKey("UI.PopupInfos.Exported");
            };
            mouseOverTooltips.Add((exportButton, () => ModFolder.Instance.GetLocalizedValue("UI.Buttons.Export.Tooltip")));
        }
        #endregion
        AppendRightButtonsPanel();
        #endregion
        #region 启用状态
        Append(_enableStatusText);
        #endregion
        #region 双击进入文件夹
        // TODO: 双击某些位置时不进入文件夹 / 测试
        OnLeftDoubleClick += (e, target) => {
            if (FolderName == "..") {
                UIModFolderMenu.Instance.GotoUpperFolder();
                return;
            }
            if (FolderNode == null) {
                return;
            }
            if (e.Target == _name.RenameText || e.Target == DeleteButton || e.Target == RenameButton || e.Target == ExportButton) {
                return;
            }
            UIModFolderMenu.Instance.EnterFolder(FolderNode);
        };
        #endregion
        ForceRecalculateLayout();
    }
    #region 右边的按钮
    protected override int RightButtonsLength => 3;
    private UIImageWithVisibility? DeleteButton { get => rightButtons[0]; set => rightButtons[0] = value; }
    private UIImageWithVisibility? RenameButton { get => rightButtons[1]; set => rightButtons[1] = value; }
    private UIImageWithVisibility? ExportButton { get => rightButtons[2]; set => rightButtons[2] = value; }
    #endregion
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
    #region 名字与重命名
    protected override bool ShouldHideNameWhenNotMouseOver => LayoutType != LayoutTypes.Stripe && FolderName == "..";
    protected override string GetDisplayName() => FolderName;
    protected override string GetRenameText() => FolderName;
    protected override string GetRenameHintText() => ModFolder.Instance.GetLocalization("UI.NewFolderDefaultName").Value;
    protected override Func<string> GetNameMouseOverTooltipFunc() => () => FolderName;
    protected override bool TryRename(string newName) {
        if (FolderNode == null || newName == ".." || newName == string.Empty) {
            return false;
        }
        if (FolderName == newName) {
            return false;
        }
        FolderNode.FolderName = newName;
        FolderName = newName;
        UIModFolderMenu.Instance.ArrangeGenerate();
        FolderDataSystem.DataChanged();
        return true;
    }
    #endregion
    #region Draw
    protected override string? GetTooltip() {
        var tooltip = base.GetTooltip();
        if (tooltip != null) {
            return tooltip;
        }
        if (ShowEnableStatusWhenNoTooltipCondition()) {
            return ModFolder.Instance.GetLocalization("UI.FolderEnableStatus").Value.FormatWith(FolderNode.ChildrenCount, FolderNode.EnabledCount, FolderNode.ToEnableCount, FolderNode.ToDisableCount);
        }
        return null;
    }
    [MemberNotNullWhen(true, nameof(FolderNode))]
    private bool ShowEnableStatusWhenNoTooltipCondition() {
        if (FolderNode == null) {
            return false;
        }
        var config = CommonConfig.Instance;
        if (config.ShowEnableStatus.ShowAny && StripeLayout) {
            return _enableStatusText.IsMouseHovering;
        }
        if (config.ShowEnableStatusBackground) {
            return true;
        }
        return false;
    }
    public override void DrawSelf(SpriteBatch spriteBatch) {
        base.DrawSelf(spriteBatch);
        DrawEnableStatus(spriteBatch);
        UpdateEnableStatusText();
    }
    private int RandomStartOffset => FolderNode?.EnableStatusRandomOffset ?? 0;
    private void UpdateEnableStatusText() {
        var config = CommonConfig.Instance.ShowEnableStatus;
        if (FolderNode == null) {
            return;
        }
        if (!config.ShowAny || NoStripeLayout) {
            _enableStatusText.SetText(string.Empty);
            return;
        }
        StringBuilder sb = SharedStringBuilder;
        sb.Clear();
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
        sb.Clear();
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
        if (FolderNode.ToDisableCount > 0 && toDisableWidth < minWidth) {
            toDisableWidth = minWidth;
        }
        int start = UIModFolderMenu.Instance.Timer - UIModFolderMenu.Instance.Timer / 3 + RandomStartOffset;
        DrawParallelogramLoop(spriteBatch, rect, start, start + enableWidth, EnabledBorderColor, EnabledInnerColor);
        DrawParallelogramLoop(spriteBatch, rect, start + enableWidth, start + enableWidth + toEnableWidth, ToEnableBorderColor, ToEnableInnerColor);
        DrawParallelogramLoop(spriteBatch, rect, start + enableWidth + toEnableWidth, start + enableWidth + toEnableWidth + toDisableWidth, ToDisableBorderColor, ToDisableInnerColor);
    }
    #endregion
    #region Layout
    protected override void RecalculateStripeLayout() {
        base.RecalculateStripeLayout();
        #region Icon
        _folderIcon.SetImage(UICommon.ButtonOpenFolder);
        _folderIcon.Left.Set(1, 0);
        _folderIcon.Top.Set(1, 0);
        _folderIcon.Width.Set(30, 0);
        _folderIcon.Height.Set(30, 0);
        #endregion
        #region 右边的按钮
        float rightOffset = SetRightButtonsPanelToStripeLayout();
        #endregion
        #region 启用状态
        _enableStatusText.Left.Set(rightOffset -= 10, 0);
        #endregion
        #region 名字
        _name.Left = new(37, 0);
        _name.Width = new(-37 + rightOffset, 1);
        _name.Height = new(0, 1);
        _name.VAlign = 0;
        #endregion
    }
    protected override void RecalculateBlockLayout() {
        base.RecalculateBlockLayout();
        #region Icon
        _folderIcon.SetImage(FolderName == ".." ? MTextures.FolderBack : MTextures.Folder);
        _folderIcon.Left.Set((BlockWidth - 80) / 2, 0);
        _folderIcon.Top.Set((BlockHeight - 80) / 2, 0);
        _folderIcon.Width.Set(80, 0);
        _folderIcon.Height.Set(80, 0);
        #endregion
        #region 右边的按钮
        SetRightButtonsPanelToBlockLayout();
        #endregion
        #region 名字
        _name.Left = new(2, 0);
        _name.Width = new(-4, 1);
        _name.Height = new(StripeHeight, 0);
        _name.VAlign = 1;
        #endregion
    }
    protected override void RecalculateBlockWithNameLayout() {
        base.RecalculateBlockWithNameLayout();
        #region Icon
        _folderIcon.SetImage(FolderName == ".." ? MTextures.FolderBack : MTextures.Folder);
        _folderIcon.Left.Set((BlockWidth - 80) / 2, 0);
        _folderIcon.Top.Set((BlockHeight - 80) / 2, 0);
        _folderIcon.Width.Set(80, 0);
        _folderIcon.Height.Set(80, 0);
        #endregion
        #region 右边的按钮
        SetRightButtonsPanelToBlockWithNameLayout();
        #endregion
        #region 名字
        _name.Left = new(2, 0);
        _name.Width = new(-4, 1);
        _name.Height = new(StripeHeight, 0);
        _name.VAlign = 1;
        #endregion
    }
    #endregion
}
