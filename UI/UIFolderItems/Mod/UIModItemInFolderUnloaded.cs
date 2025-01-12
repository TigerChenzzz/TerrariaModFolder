using Humanizer;
using ModFolder.Helpers;
using ModFolder.Systems;
using ModFolder.UI.Base;
using ModFolder.UI.Menu;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Terraria.Audio;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader.Core;
using Terraria.ModLoader.UI;
using Terraria.ModLoader.UI.ModBrowser;
using Terraria.Social.Base;
using Terraria.Social.Steam;
using Terraria.UI;
using QueryInstance = Terraria.Social.Steam.WorkshopHelper.QueryHelper.AQueryInstance;
using SteamworksConstances = Steamworks.Constants;

namespace ModFolder.UI.UIFolderItems.Mod;

// TODO: 分为正在加载时的版本和加载后仍没有对应 mod 的版本
public class UIModItemInFolderUnloaded(FolderDataSystem.ModNode modNode) : UIModItemInFolder {
    private UIImage deleteModButton = null!;
    // 当没有 PublishId 时或 Steam 不可用时为空
    private UIImage? subsribeButton;
    private UIImage renameButton = null!;

    public override string ModName => _modNode.ModName;
    public override string ModDisplayName => _modNode.DisplayName;
    private readonly FolderDataSystem.ModNode _modNode = modNode;
    public FolderDataSystem.ModNode ModNode => _modNode;
    public override FolderDataSystem.Node? Node => ModNode;
    public override DateTime LastModified => FolderDataSystem.LastModifieds.GetValueOrDefault(ModName);
    public override bool Favorite {
        get => ModNode.Favorite;
        set {
            ModNode.Favorite = value;
        }
    }

    public override void OnInitialize() {
        #region 名字与重命名输入框
        OnInitialize_ProcessName(new(GetModDisplayName()) {
            Left = { Pixels = 37 },
            Height = { Precent = 1 },
            TextOriginY = 0.5f,
        }, new(ModDisplayNameClean) {
            Left = { Pixels = 37 },
            Top = { Pixels = 6 },
            Height = { Pixels = -6, Percent = 1 },
            UnfocusOnTab = true,
        });
        #endregion
        #region 删除按钮
        int bottomRightRowOffset = -30;
        deleteModButton = new UIImage(MTextures.ButtonDelete) {
            Width = new(24, 0),
            Height = new(24, 0),
            Left = new(bottomRightRowOffset, 1),
            VAlign = .5f,
            ScaleToFit = true,
            AllowResizingDimensions = false,
            RemoveFloatingPointsFromDrawPosition = true,
        };
        deleteModButton.OnLeftClick += QuickModDelete;
        Append(deleteModButton);
        mouseOverTooltips.Add((deleteModButton, () => Language.GetTextValue("UI.Delete")));
        #endregion
        #region 重命名
        bottomRightRowOffset -= 24;
        renameButton = new(MTextures.ButtonRename) {
            Width = new(24, 0),
            Height = new(24, 0),
            Left = new(bottomRightRowOffset, 1),
            VAlign = .5f,
            ScaleToFit = true,
            AllowResizingDimensions = false,
            RemoveFloatingPointsFromDrawPosition = true,
        };
        OnInitialize_ProcessRenameButton(renameButton);
        Append(renameButton);
        mouseOverTooltips.Add((renameButton, () => ModFolder.Instance.GetLocalization("UI.Rename").Value));
        #endregion
        #region 重新订阅按钮
        bottomRightRowOffset -= 24;
        if (ModNode.PublishId != 0 && SteamedWraps.SteamAvailable) {
            subsribeButton = new(MTextures.ButtonSubscribe) {
                Width = new(24, 0),
                Height = new(24, 0),
                Left = new(bottomRightRowOffset, 1),
                VAlign = .5f,
                ScaleToFit = true,
                AllowResizingDimensions = false,
                RemoveFloatingPointsFromDrawPosition = true,
            };
            subsribeButton.OnLeftClick += TrySubscribeMod;
            Append(subsribeButton);
            mouseOverTooltips.Add((subsribeButton, GetSubscribeButtonTooltip));
        }
        #endregion
        // TODO: 显示 SteamId, 以及引导到 Steam 处
    }
    
    public override PassFilterResults PassFiltersInner() {
        var filter = UIModFolderMenu.Instance.Filter;
        if (filter.Length > 0) {
            if (UIModFolderMenu.Instance.searchFilterMode == SearchFilter.Author) {
                if (string.IsNullOrEmpty(filter))
                    goto NameFilterPassed;
            }
            else {
                if (ModDisplayNameClean.Contains(filter, StringComparison.OrdinalIgnoreCase)) {
                    goto NameFilterPassed;
                }
                if (ModName.Contains(filter, StringComparison.OrdinalIgnoreCase)) {
                    goto NameFilterPassed;
                }
                var alias = AliasClean;
                if (alias != null && alias.Contains(filter, StringComparison.OrdinalIgnoreCase)) {
                    goto NameFilterPassed;
                }
            }
            return PassFilterResults.FilteredBySearch;
        }
    NameFilterPassed:
        if (UIModFolderMenu.Instance.LoadedFilterMode == ModLoadedFilter.Loaded) {
            return PassFilterResults.FilteredByLoaded;
        }
        if (UIModFolderMenu.Instance.ModSideFilterMode != ModSideFilter.All) {
            return PassFilterResults.FilteredByModSide;
        }
        var passed = UIModFolderMenu.Instance.EnabledFilterMode switch {
            FolderEnabledFilter.All => true,
            FolderEnabledFilter.Disabled => true,
            FolderEnabledFilter.WouldBeDisabled => true,
            _ => false,
        };
        return passed ? PassFilterResults.NotFiltered : PassFilterResults.FilteredByEnabled;
    }

    #region 订阅 (下载)
    // TODO: 检查 SteamedWraps.SteamAvailable 以判断是否可以下载

    Task? SubscribeTask { get; set; }
    private void TrySubscribeMod(UIMouseEvent evt, UIElement listeningElement) {
        SoundEngine.PlaySound(SoundID.MenuTick);
        if (GetCantSubscribePopupInfo() is string popupInfo) {
            UIModFolderMenu.PopupInfo(popupInfo);
            return;
        }
        SubscribeTask = SubscribeModAsync().ContinueWith(t => SubscribeTask = null);

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
        Show(nameof(QueryInstance.QueryItemsSynchronously));
        #endregion
        #endregion
    }
    private async Task SubscribeModAsync() {
        #region 获取 ModDownloadItem
        await Task.Yield();
        QueryParameters queryParameters = new() {
            searchModIds = [new() { m_ModPubId = ModNode.PublishId.ToString() }]
        };
        QueryInstance aQueryInstance = new(queryParameters);
        ModDownloadItem? modDownloadItem = null;
        await foreach (var mod in QueryItemsAsync(aQueryInstance, [], CancellationToken.None)) {
            modDownloadItem = mod;
            break;
        }
        if (modDownloadItem == null) {
            UIModFolderMenu.PopupInfoByKey("UI.PopupInfos.CantFindModInWorkshop" ,ModDisplayNameClean, ModNode.ModName);
            return;
        }
        #endregion
        await DownloadHelper.DownloadMods([modDownloadItem]);
    }

    private static async IAsyncEnumerable<ModDownloadItem?> QueryItemsAsync(QueryInstance query, List<string> missingMods, [EnumeratorCancellation] CancellationToken token) {
        var numPages = Math.Ceiling(query.queryParameters.searchModIds.Length / (float)SteamworksConstances.kNumUGCResultsPerPage);

        for (int i = 0; i < numPages; i++) {
            var pageIds = query.queryParameters.searchModIds.Take(new Range(i * SteamworksConstances.kNumUGCResultsPerPage, SteamworksConstances.kNumUGCResultsPerPage * (i + 1)));
            var idArray = pageIds.Select(x => x.m_ModPubId).ToArray();

            try {
                // 这里有一个硬编码的 10s 超时, 希望网差的不会有事
                await query.WaitForQueryResultAsync(SteamedWraps.GenerateDirectItemsQuery(idArray), token);

                for (int j = 0; j < query._queryReturnCount; j++) {
                    var itemsIndex = j + i * SteamworksConstances.kNumUGCResultsPerPage;
                    var item = query.GenerateModDownloadItemFromQuery((uint)j);
                    if (item is null) {
                        // Currently, only known case is if a mod the user is subbed to is set to hidden & not deleted by the user
                        Logging.tML.Warn($"Unable to find Mod with ID {idArray[j]} on the Steam Workshop");
                        missingMods.Add(idArray[j]);
                        yield return null;
                        continue;
                    }
                    item.UpdateInstallState();
                    yield return item;
                }
            }
            finally {
                query.ReleaseWorkshopQuery();
            }
        }
    }
    [Conditional("NEVER")]
    private static void Show<T>(T any) {
        _ = any;
    }

    public enum SubscribeStatus {
        None,
        Loading,
        Subscribing,
        Downloading,
        NotExist,
    }
    public SubscribeStatus GetSubscribeStatus() {
        if (ModNode.PublishId == 0) {
            return SubscribeStatus.NotExist;
        }
        if (UIModFolderMenu.Instance.Downloads.ContainsKey(ModNode.ModName)) {
            return SubscribeStatus.Downloading;
        }
        if (SubscribeTask != null && !SubscribeTask.IsCompleted) {
            return SubscribeStatus.Subscribing;
        }
        if (UIModFolderMenu.Instance.Loading) {
            return SubscribeStatus.Loading;
        }
        return SubscribeStatus.None;
    }
    public string? GetCantSubscribePopupInfo() {
        if (ModNode.PublishId == 0) {
            return ModFolder.Instance.GetLocalizedValue("UI.PopupInfos.CantSubscribeForMissingPublishId");
        }
        if (UIModFolderMenu.Instance.Downloads.ContainsKey(ModNode.ModName)) {
            return ModFolder.Instance.GetLocalizedValue("UI.PopupInfos.CantSubscribeWhenDownloading");
        }
        if (SubscribeTask != null && !SubscribeTask.IsCompleted) {
            return ModFolder.Instance.GetLocalizedValue("UI.PopupInfos.CantSubscribeWhenSubscribing");
        }
        if (UIModFolderMenu.Instance.Loading) {
            return ModFolder.Instance.GetLocalizedValue("UI.PopupInfos.CantSubscribeWhenLoading");
        }
        return null;
    }
    public string GetSubscribeButtonTooltip() {
        if (UIModFolderMenu.Instance.Downloads.TryGetValue(ModNode.ModName, out var progressForTooltip)) {
            // return "下载中 6.66 MB / 88.88 MB";
            return ModFolder.Instance.GetLocalizedValue("UI.Buttons.Subscribe.Tooltips.Downloading").FormatWith(
                UIMemoryBar.SizeSuffix(progressForTooltip.BytesReceived, 2),
                UIMemoryBar.SizeSuffix(progressForTooltip.TotalBytesNeeded, 2));
        }
        else if (SubscribeTask != null && !SubscribeTask.IsCompleted) {
            // return "订阅中...";
            return ModFolder.Instance.GetLocalizedValue("UI.Buttons.Subscribe.Tooltips.Subscribing");
        }
        else if (UIModFolderMenu.Instance.Loading) {
            // return "加载中...";
            return ModFolder.Instance.GetLocalizedValue("UI.Buttons.Subscribe.Tooltips.Loading");
        }
        else {
            // return "订阅";
            return ModFolder.Instance.GetLocalizedValue("UI.Buttons.Subscribe.Tooltips.Subscribe");
        }
    }
    #endregion

    public override void Draw(SpriteBatch spriteBatch) {
        base.Draw(spriteBatch);
        #region 画订阅按钮上的阴影
        if (subsribeButton != null && GetSubscribeStatus() != SubscribeStatus.None) {
            spriteBatch.Draw(MTextures.White, subsribeButton.GetDimensions().ToRectangle(), Color.Black * 0.4f);
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
        UIModFolderMenu.Instance.RemoveConfirmPanel();
    }
    #endregion
}
