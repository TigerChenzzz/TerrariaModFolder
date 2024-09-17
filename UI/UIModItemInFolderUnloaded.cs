using ModFolder.Configs;
using ModFolder.Systems;
using Steamworks;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Terraria.Audio;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader.Core;
using Terraria.ModLoader.UI;
using Terraria.ModLoader.UI.DownloadManager;
using Terraria.ModLoader.UI.ModBrowser;
using Terraria.Social.Base;
using Terraria.Social.Steam;
using Terraria.UI;
using static Microsoft.CodeAnalysis.IOperation;

namespace ModFolder.UI;

// TODO: 分为正在加载时的版本和加载后仍没有对应 mod 的版本
public class UIModItemInFolderUnloaded(FolderDataSystem.ModNode modNode) : UIModItemInFolder {
    private UIText _uiModName = null!;
    private UIImage _deleteModButton = null!;
    // 当没有 PublishId 时或 Steam 不可用时为空
    private UIImage? _subsribeButton;
    // private bool modFromLocalModFolder;
    private string? _tooltip;

    public override string ModName => _modNode.ModName;
    public override string ModDisplayName => _modNode.DisplayName;
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
        _uiModName = new UIText(GetAlias() ?? ModDisplayName) {
            Left = { Pixels = 35 },
            Top = { Pixels = 7, },
        };
        Append(_uiModName);
        #endregion
        #region 删除按钮
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
        #region 重新订阅按钮
        bottomRightRowOffset -= 24;
        if (ModNode.PublishId != 0 && SteamedWraps.SteamAvailable) {
            _subsribeButton = new(Textures.ButtonSubscribe) {
                Width = { Pixels = 24 },
                Height = { Pixels = 24 },
                Left = { Pixels = bottomRightRowOffset, Precent = 1 },
                Top = { Pixels = -12, Percent = 0.5f },
                ScaleToFit = true,
            };
            _subsribeButton.OnLeftClick += TrySubscribeMod;
            Append(_subsribeButton);
        }
        #endregion
        // TODO: 显示 SteamId, 以及引导到 Steam 处
        // TODO: 自动订阅?
        // TODO: 显示下载进度 (滚动宽斜条表示)
        // SteamedWraps.ModDownloadInstance downloadInstance = new();
        // downloadInstance.Download(new(_modNode.PublishId));
        // SteamedWraps.UninstallWorkshopItem(new(_modNode.PublishId));
    }

    #region Subscribe
    CancellationTokenSource SubscribeTaskTokenSource { get; } = new();
    Task? SubscribeTask { get; set; }
    private void TrySubscribeMod(UIMouseEvent evt, UIElement listeningElement) {
        SoundEngine.PlaySound(SoundID.MenuTick);
        if (SubscribeTask != null && !SubscribeTask.IsCompleted || UIModFolderMenu.Instance.Loading) {
            return;
        }
        if (ModNode.PublishId == 0) {
            return;
        }
        SubscribeTask = SubscribeModAsync(SubscribeTaskTokenSource.Token);

        // 翻源码:
        // ModDownloadItem 由来:
        #region ModDownloadItem 由来
        Show(typeof(UIModDownloadItem)); // 构造
        Show(nameof(UIModBrowser.UIAsyncList_ModDownloadItem.GenElement));
        Show(nameof(UIModBrowser.PopulateModBrowser)); // ModList.SetEnumerable(SocialBackend.QueryBrowser(FilterParameters));
        Show(nameof(WorkshopBrowserModule.QueryBrowser));
        Show(nameof(WorkshopBrowserModule.DirectQueryItems));
        #endregion
        #region ModPubId_t 由来
        // 应该就是直接将 ulong 变成了字符串
        // 参考: WorkshopBrowserModule.DownloadItem 中用 ulong.Parse 处理
        #endregion

        // UIModDownloadItem.DownloadWithDeps
        Show(nameof(UIModDownloadItem.DownloadWithDeps));
        Show(nameof(UIModBrowser.DownloadMods));
        Show(nameof(WorkshopBrowserModule.DownloadItem));

        #region 重要方法
        #region 下载单个模组
        Show(nameof(WorkshopBrowserModule.DownloadItem));
        Show(nameof(ModOrganizer.LocalModsChanged)); // 放在 finally 中, 见 UIModBrowser.DownloadMods
        #endregion
        #region 查找全部依赖
        Show(nameof(SocialBrowserModule.GetDependenciesRecursive));
        Show(nameof(WorkshopBrowserModule.DirectQueryItems));
        Show(nameof(WorkshopHelper.QueryHelper.AQueryInstance.QueryItemsSynchronously));
        #endregion
        #endregion
    }
    private async Task SubscribeModAsync(CancellationToken token) {
        // TODO: 急需区分未加载完成和没订阅的模组
        #region 获取 ModDownloadItem
        QueryParameters queryParameters = new() {
            searchModIds = [new() { m_ModPubId = ModNode.PublishId.ToString() }]
        };
        WorkshopHelper.QueryHelper.AQueryInstance aQueryInstance = new(queryParameters);
        ModDownloadItem? modDownloadItem = null;
        // 在 WorkshopHelper.QueryHelper.AQueryInstance.WaitForQueryResultAsync 有一个硬编码的 10s 超时, 希望网差的不会有事
        // TODO: 改为异步?
        var mods = aQueryInstance.QueryItemsSynchronously(out _);
        if (mods.Count != 0) {
            modDownloadItem = mods[0];
        }
        if (modDownloadItem == null) {
            return;
        }
        #endregion
        #region 查找依赖
        // 取自 UIModBrowser
        HashSet<ModDownloadItem> set = [modDownloadItem];
        Interface.modBrowser.SocialBackend.GetDependenciesRecursive(set);

        var fullList = ModDownloadItem.NeedsInstallOrUpdate(set).ToList();
        if (fullList.Count == 0)
            return;
        #endregion
        #region 下载模组
        // 取自 UIModBrowser
		var downloadedList = new HashSet<string>();
        try {
            foreach (var mod in fullList) {
                bool wasInstalled = mod.IsInstalled;

                if (ModLoader.TryGetMod(mod.ModName, out var loadedMod)) {
                    loadedMod.Close();

                    // We must clear the Installed reference in ModDownloadItem to facilitate downloading, in addition to disabling - Solxan
                    mod.Installed = null;
                    // TODO: 强制需求重载
                    // setReloadRequred?.Invoke();
                }

                // TODO: 传入 IDownloadProgress
                Interface.modBrowser.SocialBackend.DownloadItem(mod, new UIWorkshopDownload());
                // 摘自 WorkshopBrowserModule.DownloadItem
                var uiProgress = new UIWorkshopDownload();
		        mod.UpdateInstallState();

		        var publishId = new PublishedFileId_t(ulong.Parse(mod.PublishId.m_ModPubId));
                bool forceUpdate = true;// mod.NeedUpdate || !SteamedWraps.IsWorkshopItemInstalled(publishId);

		        uiProgress?.DownloadStarted(mod.DisplayName);
		        Utils.LogAndConsoleInfoMessage(Language.GetTextValue("tModLoader.BeginDownload", mod.DisplayName));
		        new SteamedWraps.ModDownloadInstance().Download(publishId, uiProgress, forceUpdate);

                downloadedList.Add(mod.ModName);
            }
        }
        catch (Exception e) {
            // TODO: 更加明显的错误显示
            ModFolder.Instance.Logger.Error("Downloading mod error!", e);
        }
		finally {
			ModOrganizer.LocalModsChanged(downloadedList, isDeletion:false);
            UIModFolderMenu.Instance.Populate();
		}
        #endregion
        #region 收尾
        Thread.MemoryBarrier();
        SubscribeTask = null;
        #endregion
    }
    [Conditional("NEVER")]
    private static void Show<T>(T any) {
        _ = any;
    }
    #endregion

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
        if (_deleteModButton.IsMouseHovering) {
            _tooltip = Language.GetTextValue("UI.Delete");
        }
        else if (_subsribeButton?.IsMouseHovering == true) {
            // TODO: 本地化
            _tooltip = "订阅";
        }
        #endregion
    }

    #region 删除
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
        _modNode.Parent = null;
        UIModFolderMenu.Instance.ArrangeGenerate();
        UIModFolderMenu.Instance.RemoveConfirmPanel();
        FolderDataSystem.TrySaveWhenChanged();
    }
    #endregion
}
