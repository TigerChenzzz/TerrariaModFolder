using ModFolder.Systems;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader.UI;
using Terraria.Social.Steam;
using Terraria.UI;

namespace ModFolder.UI;

// TODO: 分为正在加载时的版本和加载后仍没有对应 mod 的版本
public class UIModItemInFolderUnloaded(FolderDataSystem.ModNode modNode) : UIFolderItem {
    private UIText _uiModName = null!;
    private UIImage? _deleteModButton;
    private UIAutoScaleTextTextPanel<string>? _dialogYesButton;
    private UIAutoScaleTextTextPanel<string>? _dialogNoButton;
    private UIText? _dialogText;
    private UIImage? _blockInput;
    private UIPanel? _deleteModDialog;
    // private bool modFromLocalModFolder;
    private string? _tooltip;

    public string ModName => _modNode.ModName;
    private readonly FolderDataSystem.ModNode _modNode = modNode;

    public override void OnInitialize() {
        #region 名字
        string text = ModName;
        _uiModName = new UIText(text) {
            Top = { Pixels = 7, },
        };
        Append(_uiModName);
        #endregion
        #region 删除
        int bottomRightRowOffset = -30;
        _deleteModButton = new UIImage(TextureAssets.Trash) {
            Width = { Pixels = 24 },
            Height = { Pixels = 24 },
            Left = { Pixels = bottomRightRowOffset, Precent = 1 },
            Top = { Pixels = -12, Percent = 0.5f },
            ScaleToFit = true,
        };
        _deleteModButton.OnLeftClick += QuickModDelete;
        Append(_deleteModButton);
        #endregion
        // TODO: 显示 SteamId, 以及引导到 Steam 处
        // TODO: 自动订阅?
        // TODO: 显示下载进度 (滚动宽斜条表示)
        // SteamedWraps.ModDownloadInstance downloadInstance = new();
        // downloadInstance.Download(new(_modNode.PublishId));
        // SteamedWraps.UninstallWorkshopItem(new(_modNode.PublishId));
    }

    public void TrySubscribeMod() {
        // TODO: 判断是否需要订阅并下载
        SteamedWraps.ModDownloadInstance downloadInstance = new();
        // TODO: 填写后面的参数
        downloadInstance.Download(new(_modNode.PublishId));
    }

    public override void Draw(SpriteBatch spriteBatch) {
        _tooltip = null;
        base.Draw(spriteBatch);
        if (!string.IsNullOrEmpty(_tooltip)) {
            //var bounds = GetOuterDimensions().ToRectangle();
            //bounds.Height += 16;
            UICommon.TooltipMouseText(_tooltip);
        }
    }

    public override void DrawSelf(SpriteBatch spriteBatch) {
        base.DrawSelf(spriteBatch);
        #region 当鼠标在某些东西上时显示些东西
        // 更多信息按钮
        // 删除按钮
        if (_deleteModButton?.IsMouseHovering == true) {
            _tooltip = Language.GetTextValue("UI.Delete");
        }
        #endregion
    }

    public override int CompareTo(object obj) {
        if (obj is not UIModItemInFolderUnloaded item)
            return 1;
        string name = ModName;
        string othername = item.ModName;
        return UIModFolderMenu.Instance.sortMode switch {
            ModsMenuSortMode.RecentlyUpdated => 0,
            ModsMenuSortMode.DisplayNameAtoZ => string.Compare(name, othername, StringComparison.Ordinal),
            ModsMenuSortMode.DisplayNameZtoA => -1 * string.Compare(name, othername, StringComparison.Ordinal),
            _ => base.CompareTo(obj),
        };
    }

    private void QuickModDelete(UIMouseEvent evt, UIElement listeningElement) {
        // TODO: 删除的提示
        // TODO: 是否正在加载时的不同提示 (不管哪种只能删除索引)
        bool shiftPressed = Main.keyState.PressingShift();

        if (!shiftPressed) {
            SoundEngine.PlaySound(SoundID.MenuOpen);
            _blockInput = new UIImage(TextureAssets.Extra[190]) {
                Width = { Percent = 1 },
                Height = { Percent = 1 },
                Color = new Color(0, 0, 0, 0),
                ScaleToFit = true
            };
            _blockInput.OnLeftMouseDown += CloseDialog;
            UIModFolderMenu.Instance.Append(_blockInput);

            _deleteModDialog = new UIPanel() {
                Width = { Percent = .30f },
                Height = { Percent = .30f },
                HAlign = .5f,
                VAlign = .5f,
                BackgroundColor = new Color(63, 82, 151),
                BorderColor = Color.Black
            };
            _deleteModDialog.SetPadding(6f);
            UIModFolderMenu.Instance.Append(_deleteModDialog);

            _dialogYesButton = new UIAutoScaleTextTextPanel<string>(Language.GetTextValue("LegacyMenu.104")) {
                TextColor = Color.White,
                Width = new StyleDimension(-10f, 1f / 3f),
                Height = { Pixels = 40 },
                VAlign = .85f,
                HAlign = .15f
            }.WithFadedMouseOver();
            _dialogYesButton.OnLeftClick += DeleteModNode;
            _deleteModDialog.Append(_dialogYesButton);

            _dialogNoButton = new UIAutoScaleTextTextPanel<string>(Language.GetTextValue("LegacyMenu.105")) {
                TextColor = Color.White,
                Width = new StyleDimension(-10f, 1f / 3f),
                Height = { Pixels = 40 },
                VAlign = .85f,
                HAlign = .85f
            }.WithFadedMouseOver();
            _dialogNoButton.OnLeftClick += CloseDialog;
            _deleteModDialog.Append(_dialogNoButton);

            _dialogText = new UIText(Language.GetTextValue("tModLoader.DeleteModConfirm")) {
                Width = { Percent = .75f },
                HAlign = .5f,
                VAlign = .3f,
                IsWrapped = true
            };
            _deleteModDialog.Append(_dialogText);

            UIModFolderMenu.Instance.Recalculate();
        }
        else {
            DeleteModNode(evt, listeningElement);
        }
    }

    private void CloseDialog(UIMouseEvent evt, UIElement listeningElement) {
        SoundEngine.PlaySound(SoundID.MenuClose);
        _blockInput?.Remove();
        _deleteModDialog?.Remove();
    }

    private void DeleteModNode(UIMouseEvent evt, UIElement listeningElement) {
        UIModFolderMenu.Instance.CurrentFolderNode.Children.Remove(_modNode);
        UIModFolderMenu.Instance.ArrangeRemove(this);

        CloseDialog(evt, listeningElement);
    }
}
