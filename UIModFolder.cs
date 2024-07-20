using System.Threading;
using System.Threading.Tasks;
using Terraria.Audio;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader.Config;
using Terraria.ModLoader.Core;
using Terraria.ModLoader.UI;
using Terraria.ModLoader.UI.ModBrowser;
using Terraria.UI;
using Terraria.UI.Gamepad;

namespace ModFolder;
public class UIModFolder : UIState, IHaveBackButtonCommand {
    public static UIModFolder Instance { get; private set; } = new();
    public const int MyMenuMode = 47133;

    public UIState? PreviousUIState { get; set; }
    private UIElement uIElement = null!;
    private UIPanel uIPanel = null!;
    private UILoaderAnimatedImage uiLoader = null!;
    private bool needToRemoveLoading;
    private UIList modList = null!;
    private float modListViewPosition;
    private readonly List<UIModItemInFolder> items = [];
    private Task<List<UIModItemInFolder>>? modItemsTask;
    private bool updateNeeded;
    public bool loading;
    private UIInputTextField filterTextBox = null!;
    public UICycleImage SearchFilterToggle = null!;
    public ModsMenuSortMode sortMode = ModsMenuSortMode.RecentlyUpdated;
    public EnabledFilter enabledFilterMode = EnabledFilter.All;
    public ModSideFilter modSideFilterMode = ModSideFilter.All;
    public SearchFilter searchFilterMode = SearchFilter.Name;
    internal readonly List<UICycleImage> _categoryButtons = [];
    internal string filter = string.Empty;
    private UIAutoScaleTextTextPanel<LocalizedText> buttonEA = null!;
    private UIAutoScaleTextTextPanel<LocalizedText> buttonDA = null!;
    private UIAutoScaleTextTextPanel<LocalizedText> buttonRM = null!;
    private UIAutoScaleTextTextPanel<LocalizedText> buttonB = null!;
    private UIAutoScaleTextTextPanel<LocalizedText> buttonOMF = null!;
    private UIAutoScaleTextTextPanel<LocalizedText> buttonCL = null!;
    private CancellationTokenSource? _cts;
    private bool forceReloadHidden => ModLoader.autoReloadRequiredModsLeavingModsScreen && !ModCompile.DeveloperMode;

    public override void OnInitialize() {
        #region 全部元素的容器
        uIElement = new UIElement {
            Width = { Percent = 0.8f },
            MaxWidth = UICommon.MaxPanelWidth,
            Top = { Pixels = 220 },
            Height = { Pixels = -220, Percent = 1f },
            HAlign = 0.5f
        };
        #endregion
        #region 除开下面按钮之外的面板
        uIPanel = new UIPanel {
            Width = { Percent = 1f },
            Height = { Pixels = -110, Percent = 1f },
            BackgroundColor = UICommon.MainPanelBackground,
            PaddingTop = 0f
        };
        uIElement.Append(uIPanel);
        #endregion
        uiLoader = new UILoaderAnimatedImage(0.5f, 0.5f, 1f);
        #region 模组列表
        modList = new UIList {
            Width = { Pixels = -25, Percent = 1f },
            Height = { Pixels = ModLoader.showMemoryEstimates ? -72 : -50, Percent = 1f },
            Top = { Pixels = ModLoader.showMemoryEstimates ? 72 : 50 },
            ListPadding = 5f
        };
        uIPanel.Append(modList);
        #endregion
        #region 内存占用
        if (ModLoader.showMemoryEstimates) {
            var ramUsage = new UIMemoryBar() {
                Top = { Pixels = 45 },
            };
            ramUsage.Width.Pixels = -25;
            uIPanel.Append(ramUsage);
        }
        #endregion
        #region 滚条
        var uIScrollbar = new UIScrollbar {
            Height = { Pixels = ModLoader.showMemoryEstimates ? -72 : -50, Percent = 1f },
            Top = { Pixels = ModLoader.showMemoryEstimates ? 72 : 50 },
            HAlign = 1f
        }.WithView(100f, 1000f);
        uIPanel.Append(uIScrollbar);

        modList.SetScrollbar(uIScrollbar);
        #endregion
        #region 标题
        var uIHeaderTexTPanel = new UITextPanel<LocalizedText>(Language.GetText("tModLoader.ModsModsList"), 0.8f, true) {
            HAlign = 0.5f,
            Top = { Pixels = -35 },
            BackgroundColor = UICommon.DefaultUIBlue
        }.WithPadding(15f);
        uIElement.Append(uIHeaderTexTPanel);
        #endregion
        #region 启用全部按钮
        buttonEA = new UIAutoScaleTextTextPanel<LocalizedText>(Language.GetText("tModLoader.ModsEnableAll")) {
            TextColor = Color.Green,
            Width = new StyleDimension(-10f, 1f / 3f),
            Height = { Pixels = 40 },
            VAlign = 1f,
            Top = { Pixels = -65 }
        }.WithFadedMouseOver();
        buttonEA.OnLeftClick += EnableAll;
        uIElement.Append(buttonEA);
        #endregion
        #region 禁用全部按钮
        // TODO CopyStyle doesn't capture all the duplication here, consider an inner method
        buttonDA = new UIAutoScaleTextTextPanel<LocalizedText>(Language.GetText("tModLoader.ModsDisableAll"));
        buttonDA.CopyStyle(buttonEA);
        buttonDA.TextColor = Color.Red;
        buttonDA.HAlign = 0.5f;
        buttonDA.WithFadedMouseOver();
        buttonDA.OnLeftClick += DisableAll;
        uIElement.Append(buttonDA);
        #endregion
        #region 重新加载按钮
        buttonRM = new UIAutoScaleTextTextPanel<LocalizedText>(Language.GetText("tModLoader.ModsForceReload"));
        buttonRM.CopyStyle(buttonEA);
        buttonRM.Width = new StyleDimension(-10f, 1f / 3f);
        buttonRM.HAlign = 1f;
        buttonRM.WithFadedMouseOver();
        buttonRM.OnLeftClick += ReloadMods;
        uIElement.Append(buttonRM);
        #endregion
        UpdateTopRowButtons();
        #region 返回按钮
        buttonB = new UIAutoScaleTextTextPanel<LocalizedText>(Language.GetText("UI.Back")) {
            Width = new StyleDimension(-10f, 1f / 3f),
            Height = { Pixels = 40 },
            VAlign = 1f,
            Top = { Pixels = -20 }
        }.WithFadedMouseOver();
        buttonB.OnLeftClick += (_, _) => HandleBackButtonUsage();

        uIElement.Append(buttonB);
        #endregion
        #region 打开模组文件夹按钮
        buttonOMF = new UIAutoScaleTextTextPanel<LocalizedText>(Language.GetText("tModLoader.ModsOpenModsFolders"));
        buttonOMF.CopyStyle(buttonB);
        buttonOMF.HAlign = 0.5f;
        buttonOMF.WithFadedMouseOver();
        buttonOMF.OnLeftClick += OpenModsFolder;
        uIElement.Append(buttonOMF);
        #endregion
        #region 仨排序按钮
        var texture = UICommon.ModBrowserIconsTexture;
        var upperMenuContainer = new UIElement {
            Width = { Percent = 1f },
            Height = { Pixels = 32 },
            Top = { Pixels = 10 }
        };

        UICycleImage toggleImage;
        for (int j = 0; j < 3; j++) {
            if (j == 0) { //TODO: ouch, at least there's a loop but these click events look quite similar
                toggleImage = new(texture, 3, 32, 32, 34 * 3, 0);
                toggleImage.SetCurrentState((int)sortMode);
                toggleImage.OnLeftClick += (a, b) => {
                    sortMode = sortMode.NextEnum();
                    updateNeeded = true;
                };
                toggleImage.OnRightClick += (a, b) => {
                    sortMode = sortMode.PreviousEnum();
                    updateNeeded = true;
                };
            }
            else if (j == 1) {
                toggleImage = new UICycleImage(texture, 3, 32, 32, 34 * 4, 0);
                toggleImage.SetCurrentState((int)enabledFilterMode);
                toggleImage.OnLeftClick += (a, b) => {
                    enabledFilterMode = enabledFilterMode.NextEnum();
                    updateNeeded = true;
                };
                toggleImage.OnRightClick += (a, b) => {
                    enabledFilterMode = enabledFilterMode.PreviousEnum();
                    updateNeeded = true;
                };
            }
            else {
                toggleImage = new UICycleImage(texture, 5, 32, 32, 34 * 5, 0);
                toggleImage.SetCurrentState((int)modSideFilterMode);
                toggleImage.OnLeftClick += (a, b) => {
                    modSideFilterMode = modSideFilterMode.NextEnum();
                    updateNeeded = true;
                };
                toggleImage.OnRightClick += (a, b) => {
                    modSideFilterMode = modSideFilterMode.PreviousEnum();
                    updateNeeded = true;
                };
            }
            toggleImage.Left.Pixels = j * 36 + 8;
            _categoryButtons.Add(toggleImage);
            upperMenuContainer.Append(toggleImage);
        }

        var filterTextBoxBackground = new UIPanel {
            Top = { Percent = 0f },
            Left = { Pixels = -185, Percent = 1f },
            Width = { Pixels = 150 },
            Height = { Pixels = 40 }
        };
        filterTextBoxBackground.SetPadding(0);
        filterTextBoxBackground.OnRightClick += ClearSearchField;
        upperMenuContainer.Append(filterTextBoxBackground);
        #endregion
        #region 搜索栏
        filterTextBox = new UIInputTextField(Language.GetTextValue("tModLoader.ModsTypeToSearch")) {
            Top = { Pixels = 5 },
            Height = { Percent = 1f },
            Width = { Percent = 1f },
            Left = { Pixels = 5 },
            VAlign = 0.5f,
        };
        filterTextBox.OnTextChange += (a, b) => updateNeeded = true;
        filterTextBoxBackground.Append(filterTextBox);

        #region 取消搜索按钮
        UIImageButton clearSearchButton = new(Main.Assets.Request<Texture2D>("Images/UI/SearchCancel")) {
            HAlign = 1f,
            VAlign = 0.5f,
            Left = new StyleDimension(-2f, 0f)
        };

        //clearSearchButton.OnMouseOver += searchCancelButton_OnMouseOver;
        clearSearchButton.OnLeftClick += ClearSearchField;
        filterTextBoxBackground.Append(clearSearchButton);
        #endregion
        #region 搜索过滤器按钮
        SearchFilterToggle = new UICycleImage(texture, 2, 32, 32, 34 * 2, 0) {
            Left = { Pixels = 545 }
        };
        SearchFilterToggle.SetCurrentState((int)searchFilterMode);
        SearchFilterToggle.OnLeftClick += (a, b) => {
            searchFilterMode = searchFilterMode.NextEnum();
            updateNeeded = true;
        };
        SearchFilterToggle.OnRightClick += (a, b) => {
            searchFilterMode = searchFilterMode.PreviousEnum();
            updateNeeded = true;
        };
        _categoryButtons.Add(SearchFilterToggle);
        upperMenuContainer.Append(SearchFilterToggle);
        #endregion
        #endregion
        #region 模组配置按钮
        buttonCL = new UIAutoScaleTextTextPanel<LocalizedText>(Language.GetText("tModLoader.ModConfiguration"));
        buttonCL.CopyStyle(buttonOMF);
        buttonCL.HAlign = 1f;
        buttonCL.WithFadedMouseOver();
        buttonCL.OnLeftClick += GotoModConfigList;
        uIElement.Append(buttonCL);
        #endregion
        uIPanel.Append(upperMenuContainer);
        Append(uIElement);
    }

    public static bool IsPreviousUIStateOfConfigList { get; set; }

    private void ClearSearchField(UIMouseEvent evt, UIElement listeningElement) => filterTextBox.Text = "";

    // Adjusts sizing and placement of top row buttons according to whether or not
    // the Force Reload button is being shown.
    private void UpdateTopRowButtons() {
        var buttonWidth = new StyleDimension(-10f, 1f / (forceReloadHidden ? 2f : 3f));

        buttonEA.Width = buttonWidth;

        buttonDA.Width = buttonWidth;
        buttonDA.HAlign = forceReloadHidden ? 1f : 0.5f;

        uIElement.AddOrRemoveChild(buttonRM, ModCompile.DeveloperMode || !forceReloadHidden);
    }

    public void HandleBackButtonUsage() {
        // To prevent entering the game with Configs that violate ReloadRequired
        if (ConfigManager.AnyModNeedsReload()) {
            Main.menuMode = Interface.reloadModsID;
            return;
        }

        // If auto reloading required mods is enabled, check if any mods need reloading and reload as required
        if (ModLoader.autoReloadRequiredModsLeavingModsScreen && items.Any(i => i.NeedsReload)) {
            Main.menuMode = Interface.reloadModsID;
            return;
        }

        ConfigManager.OnChangedAll();

        IHaveBackButtonCommand.GoBackTo(PreviousUIState);
    }

    private void ReloadMods(UIMouseEvent evt, UIElement listeningElement) {
        SoundEngine.PlaySound(SoundID.MenuOpen);
        if (items.Count > 0)
            ModLoader.Reload();
    }

    private static void OpenModsFolder(UIMouseEvent evt, UIElement listeningElement) {
        SoundEngine.PlaySound(SoundID.MenuOpen);
        Directory.CreateDirectory(ModLoader.ModPath);
        Utils.OpenFolder(ModLoader.ModPath);

        if (ModOrganizer.WorkshopFileFinder.ModPaths.Count != 0) {
            string? workshopFolderPath = Directory.GetParent(ModOrganizer.WorkshopFileFinder.ModPaths[0])?.ToString();
            if (workshopFolderPath != null)
                Utils.OpenFolder(workshopFolderPath);
        }
    }

    private void EnableAll(UIMouseEvent evt, UIElement listeningElement) {
        SoundEngine.PlaySound(SoundID.MenuTick);
        foreach (var modItem in items) {
            if (modItem.tMLUpdateRequired != null)
                continue;
            modItem.Enable();
        }
    }

    private void DisableAll(UIMouseEvent evt, UIElement listeningElement) {
        SoundEngine.PlaySound(SoundID.MenuTick);
        foreach (var modItem in items) {
            modItem.Disable();
        }
    }

    private void GotoModConfigList(UIMouseEvent evt, UIElement listeningElement) {
        SoundEngine.PlaySound(SoundID.MenuOpen);
        Main.menuMode = Interface.modConfigListID;
        IsPreviousUIStateOfConfigList = true;
    }

    public UIModItemInFolder? FindUIModItem(string modName) {
        return items.SingleOrDefault(m => m.ModName == modName);
    }

    public override void Update(GameTime gameTime) {
        base.Update(gameTime);
        if (modItemsTask is { IsCompleted: true }) {
            var result = modItemsTask.Result;
            items.AddRange(result);
            foreach (var item in items) {
                item.Activate(); // Activate must happen after all UIModItem are in `items`
            }
            needToRemoveLoading = true;
            updateNeeded = true;
            loading = false;
            modItemsTask = null;
        }
        if (needToRemoveLoading) {
            needToRemoveLoading = false;
            uIPanel.RemoveChild(uiLoader);
        }
        if (!updateNeeded)
            return;
        updateNeeded = false;
        filter = filterTextBox.Text;
        modList.Clear();
        var filterResults = new UIModsFilterResults();
        var visibleItems = items.Where(item => item.PassFilters(filterResults)).ToList();
        if (filterResults.AnyFiltered) {
            var panel = new UIPanel();
            panel.Width.Set(0, 1f);
            modList.Add(panel);
            var filterMessages = new List<string>();
            if (filterResults.filteredByEnabled > 0)
                filterMessages.Add(Language.GetTextValue("tModLoader.ModsXModsFilteredByEnabled", filterResults.filteredByEnabled));
            if (filterResults.filteredByModSide > 0)
                filterMessages.Add(Language.GetTextValue("tModLoader.ModsXModsFilteredByModSide", filterResults.filteredByModSide));
            if (filterResults.filteredBySearch > 0)
                filterMessages.Add(Language.GetTextValue("tModLoader.ModsXModsFilteredBySearch", filterResults.filteredBySearch));
            string filterMessage = string.Join("\n", filterMessages);
            var text = new UIText(filterMessage);
            text.Width.Set(0, 1f);
            text.IsWrapped = true;
            text.WrappedTextBottomPadding = 0;
            text.TextOriginX = 0f;
            text.Recalculate();
            panel.Append(text);
            panel.Height.Set(text.MinHeight.Pixels + panel.PaddingTop, 0f);
        }
        modList.AddRange(visibleItems);
        Recalculate();
        modList.ViewPosition = modListViewPosition;
    }

    public override void Draw(SpriteBatch spriteBatch) {
        UILinkPointNavigator.Shortcuts.BackButtonCommand = 7;
        base.Draw(spriteBatch);
        for (int i = 0; i < _categoryButtons.Count; i++) {
            if (_categoryButtons[i].IsMouseHovering) {
                string text = i switch {
                    0 => sortMode.ToFriendlyString(),
                    1 => enabledFilterMode.ToFriendlyString(),
                    2 => modSideFilterMode.ToFriendlyString(),
                    3 => searchFilterMode.ToFriendlyString(),
                    _ => "None",
                };
                UICommon.TooltipMouseText(text);
                return;
            }
        }
        if (buttonOMF.IsMouseHovering)
            UICommon.TooltipMouseText(Language.GetTextValue("tModLoader.ModsOpenModsFoldersTooltip"));
    }

    public override void OnActivate() {
        Main.clrInput();
        modList.Clear();
        items.Clear();
        loading = true;
        uIPanel.Append(uiLoader);
        ConfigManager.LoadAll(); // Makes sure MP configs are cleared.
        Populate();
        UpdateTopRowButtons();
    }

    public override void OnDeactivate() {
        _cts?.Cancel(false);
        _cts?.Dispose();
        _cts = null;
        modListViewPosition = modList.ViewPosition;
    }

    internal void Populate() {
        _cts = new CancellationTokenSource();
        modItemsTask = Task.Run(() => {
            var mods = ModOrganizer.FindMods(logDuplicates: true);
            List<UIModItemInFolder> pendingUIModItems = [];
            foreach (var mod in mods) {
                UIModItemInFolder modItem = new(mod);
                pendingUIModItems.Add(modItem);
            }
            return pendingUIModItems;
        }, _cts.Token);
    }
}
