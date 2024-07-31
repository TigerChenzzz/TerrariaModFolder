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
    // private bool modFromLocalModFolder;
    private string? _tooltip;

    public override string NameToSort => ModDisplayNameClean;
    public string ModName => _modNode.ModName;
    public string ModDisplayName => _modNode.DisplayName;
    private string? _modDisplayNameClean;
    public string ModDisplayNameClean => _modDisplayNameClean ??= Utils.CleanChatTags(ModDisplayName);
    private readonly FolderDataSystem.ModNode _modNode = modNode;
    public FolderDataSystem.ModNode ModNode => _modNode;
    public override FolderDataSystem.Node? Node => ModNode;
    // TODO
    public override DateTime LastModified => base.LastModified;
    public override bool Favorite {
        get => ModNode.Favorite;
        set {
            ModNode.Favorite = value;
        }
    }

    public override void OnInitialize() {
        #region 名字
        string text = ModDisplayName;
        _uiModName = new UIText(text) {
            Top = { Pixels = 7, },
        };
        Append(_uiModName);
        #endregion
        #region 删除
        int bottomRightRowOffset = -30;
        _deleteModButton = new UIImage(Textures.ButtonDelete) {
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

    private void QuickModDelete(UIMouseEvent evt, UIElement listeningElement) {
        // TODO: 删除的提示
        // TODO: 是否正在加载时的不同提示 (不管哪种只能删除索引)
        bool shiftPressed = Main.keyState.PressingShift();

        if (shiftPressed) {
            DeleteModNode(evt, listeningElement);
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

        var _dialogYesButton = new UIAutoScaleTextTextPanel<string>(Language.GetTextValue("LegacyMenu.104")) {
            TextColor = Color.White,
            Width = new StyleDimension(-10f, 1f / 3f),
            Height = { Pixels = 40 },
            VAlign = .85f,
            HAlign = .15f
        }.WithFadedMouseOver();
        _dialogYesButton.OnLeftClick += DeleteModNode;
        _deleteModDialog.Append(_dialogYesButton);

        var _dialogNoButton = new UIAutoScaleTextTextPanel<string>(Language.GetTextValue("LegacyMenu.105")) {
            TextColor = Color.White,
            Width = new StyleDimension(-10f, 1f / 3f),
            Height = { Pixels = 40 },
            VAlign = .85f,
            HAlign = .85f
        }.WithFadedMouseOver();
        _dialogNoButton.OnLeftClick += (_, _) => UIModFolderMenu.Instance.RemoveConfirmPanel();
        _deleteModDialog.Append(_dialogNoButton);
        string tip = Language.GetTextValue("tModLoader.DeleteModConfirm");
        tip = string.Join('\n', tip, ModFolder.Instance.GetLocalization("UI.DeleteModItemUnloadedComfirmTextToAdd").Value);
        var _dialogText = new UIText(tip) {
            Width = { Percent = .85f },
            HAlign = .5f,
            VAlign = .3f,
            IsWrapped = true
        };
        _deleteModDialog.Append(_dialogText);

        UIModFolderMenu.Instance.Recalculate();
    }

    private void DeleteModNode(UIMouseEvent evt, UIElement listeningElement) {
        UIModFolderMenu.Instance.CurrentFolderNode.Children.Remove(_modNode);
        UIModFolderMenu.Instance.ArrangeGenerate();
        UIModFolderMenu.Instance.RemoveConfirmPanel();
    }
}
