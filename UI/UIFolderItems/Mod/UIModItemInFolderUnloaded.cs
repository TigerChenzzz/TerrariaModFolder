﻿using Humanizer;
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
    // TODO
    public override DateTime LastModified => base.LastModified;
    public override bool Favorite {
        get => ModNode.Favorite;
        set {
            ModNode.Favorite = value;
        }
    }

    public override void OnInitialize() {
        #region 名字与重命名输入框
        OnInitialize_ProcessName(new(GetModDisplayName()) {
            Left = { Pixels = 35 },
            Height = { Precent = 1 },
            TextOriginY = 0.5f,
        }, new(ModDisplayNameClean) {
            Left = { Pixels = 35 },
            Top = { Pixels = 5 },
            Height = { Pixels = -5, Percent = 1 },
            UnfocusOnTab = true,
        });
        #endregion
        #region 删除按钮
        int bottomRightRowOffset = -30;
        deleteModButton = new UIImage(MTextures.ButtonDelete) {
            Width = { Pixels = 24 },
            Height = { Pixels = 24 },
            Left = { Pixels = bottomRightRowOffset, Precent = 1 },
            Top = { Pixels = -12, Percent = 0.5f },
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
            Width = { Pixels = 24 },
            Height = { Pixels = 24 },
            Left = { Pixels = bottomRightRowOffset, Precent = 1 },
            Top = { Pixels = -12, Percent = 0.5f },
            ScaleToFit = true,
            AllowResizingDimensions = false,
            RemoveFloatingPointsFromDrawPosition = true,
        };
        OnInitialize_ProcessRenameButton(renameButton);
        mouseOverTooltips.Add((renameButton, () => ModFolder.Instance.GetLocalization("UI.Rename").Value));
        #endregion
        #region 重新订阅按钮
        bottomRightRowOffset -= 24;
        if (ModNode.PublishId != 0 && SteamedWraps.SteamAvailable) {
            subsribeButton = new(MTextures.ButtonSubscribe) {
                Width = { Pixels = 24 },
                Height = { Pixels = 24 },
                Left = { Pixels = bottomRightRowOffset, Precent = 1 },
                Top = { Pixels = -12, Percent = 0.5f },
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
    
    public override int PassFiltersInner() {
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
            return 1;
        }
    NameFilterPassed:
        if (UIModFolderMenu.Instance.ModSideFilterMode != ModSideFilter.All) {
            return 2;
        }
        var passed = UIModFolderMenu.Instance.EnabledFilterMode switch {
            FolderEnabledFilter.All => true,
            FolderEnabledFilter.Disabled => true,
            FolderEnabledFilter.WouldBeDisabled => true,
            _ => false,
        };
        return passed ? 0 : 3;
    }


    #region 订阅 (下载)
    Task? SubscribeTask { get; set; }
    private void TrySubscribeMod(UIMouseEvent evt, UIElement listeningElement) {
        SoundEngine.PlaySound(SoundID.MenuTick);
        if (GetCantSubscribePopupInfo() is string popupInfo) {
            UIModFolderMenu.Instance.PopupInfo(popupInfo);
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
            UIModFolderMenu.Instance.PopupInfo($"未在创意工坊找到模组: {ModDisplayNameClean} ({ModNode.ModName})");
            return;
        }
        #endregion
        #region 查找依赖
        // 取自 UIModBrowser
        HashSet<ModDownloadItem> set = [modDownloadItem];
        Interface.modBrowser.SocialBackend.GetDependenciesRecursive(set);

        var fullList = ModDownloadItem.NeedsInstallOrUpdate(set).ToArray();
        if (fullList.Length == 0)
            return;
        #endregion
        #region 下载模组
        // 取自 UIModBrowser
        try {
            foreach (var mod in fullList) {
                await Task.Yield();
                if (UIModFolderMenu.Instance.Downloads.ContainsKey(mod.ModName)) {
                    continue;
                }
                bool wasInstalled = mod.IsInstalled;

                if (ModLoader.TryGetMod(mod.ModName, out var loadedMod)) {
                    loadedMod.Close();
                    // We must clear the Installed reference in ModDownloadItem to facilitate downloading, in addition to disabling - Solxan
                    mod.Installed = null;
                    UIModFolderMenu.Instance.ForceRoadRequired();
                }

                #region 下载模组 (摘自 WorkshopBrowserModule.DownloadItem)
                mod.UpdateInstallState();
                UIModFolderMenu.Instance.AddDownload(mod.ModName, new(mod));
                #endregion
            }
        }
        catch (Exception e) {
            UIModFolderMenu.Instance.PopupInfo("下载模组时发生错误! 具体错误请查看日志");
            ModFolder.Instance.Logger.Error("Downloading mod error!", e);
        }
        #endregion
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
            return ModFolder.Instance.GetLocalization("UI.PopupInfos.CantSubscribeForMissingPublishId").Value;
        }
        if (UIModFolderMenu.Instance.Downloads.ContainsKey(ModNode.ModName)) {
            return ModFolder.Instance.GetLocalization("UI.PopupInfos.CantSubscribeWhenDownloading").Value;
        }
        if (SubscribeTask != null && !SubscribeTask.IsCompleted) {
            return ModFolder.Instance.GetLocalization("UI.PopupInfos.CantSubscribeWhenSubscribing").Value;
        }
        if (UIModFolderMenu.Instance.Loading) {
            return ModFolder.Instance.GetLocalization("UI.PopupInfos.CantSubscribeWhenLoading").Value;
        }
        return null;
    }
    public string GetSubscribeButtonTooltip() {
        if (UIModFolderMenu.Instance.Downloads.TryGetValue(ModNode.ModName, out var progressForTooltip)) {
            // return "下载中 6.66 MB / 88.88 MB";
            return ModFolder.Instance.GetLocalization("UI.Buttons.Subscribe.Tooltips.Downloading").Value.FormatWith(
                UIMemoryBar.SizeSuffix(progressForTooltip.BytesReceived, 2),
                UIMemoryBar.SizeSuffix(progressForTooltip.TotalBytesNeeded, 2));
        }
        else if (SubscribeTask != null && !SubscribeTask.IsCompleted) {
            // return "订阅中...";
            return ModFolder.Instance.GetLocalization("UI.Buttons.Subscribe.Tooltips.Subscribing").Value;
        }
        else if (UIModFolderMenu.Instance.Loading) {
            // return "加载中...";
            return ModFolder.Instance.GetLocalization("UI.Buttons.Subscribe.Tooltips.Loading").Value;
        }
        else {
            // return "订阅";
            return ModFolder.Instance.GetLocalization("UI.Buttons.Subscribe.Tooltips.Subscribe").Value;
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

    public override void DrawSelf(SpriteBatch spriteBatch) {
        base.DrawSelf(spriteBatch);
        if (UIModFolderMenu.Instance.Downloads.TryGetValue(ModNode.ModName, out var progress)) {
            DrawDownloadStatus(spriteBatch, progress);
        }
    }
    #region 画下载状态
    private void DrawDownloadStatus(SpriteBatch spriteBatch, DownloadProgressImpl progress) {
        Rectangle rectangle = GetDimensions().ToRectangle();
        Rectangle progressRectangle = new(rectangle.X + 1, rectangle.Y + 1, (int)((rectangle.Width - 2) * progress.Progress), rectangle.Height - 2);
        Rectangle progressRectangleOuter = new(rectangle.X, rectangle.Y, progressRectangle.Width + 2, rectangle.Height);

        spriteBatch.DrawBox(rectangle, Color.White * 0.5f, default);
        spriteBatch.Draw(MTextures.White, progressRectangle, Color.White * 0.2f);

        int timePassed = UIModFolderMenu.Instance.Timer - progress.CreateTimeRandomized;
        int realTimePassed = UIModFolderMenu.Instance.Timer - progress.CreateTime;
        int totalWidthToPass = rectangle.Width * 3;
        int goThroughWidth = rectangle.Width * 2 / 3;
        int passSpeed = 12;
        int end = timePassed * passSpeed % totalWidthToPass;
        if (end < 0) {
            end += totalWidthToPass;
        }
        if (end > realTimePassed * passSpeed) {
            return;
        }
        int start = end - goThroughWidth;

        DrawParallelogram(spriteBatch, rectangle, start, end, Color.White * 0.8f, default);
        DrawParallelogram(spriteBatch, progressRectangleOuter, start, end, default, Color.White * 0.3f);
    }
    #endregion

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
