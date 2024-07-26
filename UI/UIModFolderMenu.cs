using ModFolder.Systems;
using System.Collections;
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
using FolderNode = ModFolder.Systems.FolderDataSystem.FolderNode;
using ModNode = ModFolder.Systems.FolderDataSystem.ModNode;

namespace ModFolder.UI;

// TODO: 在进入时即刻生成 UI
public class UIModFolderMenu : UIState, IHaveBackButtonCommand {
    #region 字段与属性
    public static UIModFolderMenu Instance { get; private set; } = new();
    public static void TotallyReload() {
        Instance = new();
    }
    public const int MyMenuMode = 47133;

    #region 所有模组 ModItems
    /// <summary>
    /// 当找完模组后, 这里会存有所有的模组
    /// </summary>
    private Dictionary<string, UIModItemInFolder> ModItemDict { get; set; } = [];
    #endregion
    #region 根列表中的物品 Items
    /// <summary>
    /// 存有根列表中的物品, 包含模组与文件夹
    /// </summary>
    public List<UIFolderItem> Items { get; set; } = [];
    #endregion
    #region 文件夹路径
    /// <summary>
    /// 当前处于哪个文件夹下, 若为空则代表处于根目录下
    /// </summary>
    public FolderNode CurrentFolderNode => FolderPath[^1];

    private FolderPathClass FolderPath = null!;
    #region 当 FolderPath 改变时同步修改 folderPathList 中的元素
    private class FolderPathClass : IList<FolderNode> {
        private static UIElementCustom NewHListElement(FolderNode folder) {
            UIText text = new(folder.FolderName);
            text.Recalculate();
            var textDimensions = text.GetDimensions();
            UIElementCustom result = new();
            result.Width.Pixels = textDimensions.Width + 8;
            result.PaddingLeft = result.PaddingRight = 4;
            result.Height.Percent = 1;
            result.OnDraw += sb => {
                var dimensions = result.GetDimensions();
                var rectangle = dimensions.ToRectangle();
                if (result.IsMouseHovering) {
                    sb.DrawBox(dimensions.ToRectangle(), Color.White * 0.8f, Color.White * 0.2f);
                }
                //if (folder != Instance.CurrentFolderNode)
                sb.Draw(UICommon.DividerTexture.Value, new Rectangle(rectangle.X + rectangle.Width + 2, rectangle.Y, rectangle.Height, 2), null, Color.White,
                    MathF.PI / 2, Vector2.Zero, SpriteEffects.None, 0);
            };
            result.OnLeftDoubleClick += (_, _) => {
                Instance.GotoUpperFolder(folder);
            };
            text.VAlign = 0.5f;
            result.Append(text);
            return result;
        }
        private static UIHorizontalList HList => Instance.folderPathList;
        private readonly List<FolderNode> data = [];
        public FolderNode this[int index] {
            get => data[index];
            set {
                data[index] = value;
                HList.Items[index].Parent = null;
                HList.Items[index] = NewHListElement(value);
                HList.MarkItemsModified();
            }
        }
        public int Count => data.Count;
        public bool IsReadOnly => false;
        public void Add(FolderNode item) {
            data.Add(item);
            HList.Items.Add(NewHListElement(item));
            HList.MarkItemsModified();
        }
        public void Insert(int index, FolderNode item) {
            data.Insert(index, item);
            HList.Items.Insert(index, NewHListElement(item));
            HList.MarkItemsModified();
        }

        public bool Remove(FolderNode item) {
            for (int i = Count - 1; i >= 0; i--) {
                if (data[i] == item) {
                    RemoveAt(i);
                    return true;
                }
            }
            return false;
        }
        public void RemoveAt(int index) {
            data.RemoveAt(index);
            HList.Items[index].Parent = null;
            HList.Items.RemoveAt(index);
            HList.MarkItemsModified();
        }
        public void RemoveRange(int index, int count) {
            data.RemoveRange(index, count);
            for (int i = index; i < index + count; ++i) {
                HList.Items[i].Parent = null;
            }
            HList.Items.RemoveRange(index, count);
            HList.MarkItemsModified();
        }
        public void Clear() {
            data.Clear();
            foreach (var item in HList.Items) {
                item.Parent = null;
            }
            HList.Items.Clear();
            HList.MarkItemsModified();
        }

        public bool Contains(FolderNode item) {
            return data.Contains(item);
        }
        public int IndexOf(FolderNode item) {
            return data.IndexOf(item);
        }

        public void CopyTo(FolderNode[] array, int arrayIndex) {
            data.CopyTo(array, arrayIndex);
        }
        public IEnumerator<FolderNode> GetEnumerator() {
            return data.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator() {
            return data.GetEnumerator();
        }
    }
    #endregion
    #region 文件夹跳转
    public void EnterFolder(string folderName) {
        bool max = folderPathList.ViewPosition == folderPathList.MaxViewPosition;
        foreach (var child in CurrentFolderNode.Children) {
            if (child is FolderNode folder && folder.FolderName == folderName) {
                FolderPath.Add(folder);
                updateNeeded = true;
                break;
            }
        }
        if (max) {
            folderPathList.ViewPosition = folderPathList.MaxViewPosition;
        }
    }
    public void GotoUpperFolder() {
        if (FolderPath.Count <= 1) {
            return;
        }
        FolderPath.RemoveAt(FolderPath.Count - 1);
        updateNeeded = true;
    }
    public void GotoUpperFolder(FolderNode folder) {
        for (int i = 0; i < FolderPath.Count - 1; ++i) {
            if (FolderPath[i] != folder) {
                continue;
            }
            FolderPath.RemoveRange(i + 1, FolderPath.Count - i - 1);
            updateNeeded = true;
            break;
        }
    }
    #endregion
    #endregion

    #region 子元素
    private UIElement uIElement = null!;
    private UIPanel uIPanel = null!;
    private UILoaderAnimatedImage uiLoader = null!;
    private UIHorizontalList folderPathList = null!;
    private UIFolderItemList list = null!;
    private UIInputTextField filterTextBox = null!;
    public UICycleImage SearchFilterToggle = null!;
    internal readonly List<UICycleImage> _categoryButtons = [];
    private UIAutoScaleTextTextPanel<LocalizedText> buttonEA = null!;
    private UIAutoScaleTextTextPanel<LocalizedText> buttonDA = null!;
    private UIAutoScaleTextTextPanel<LocalizedText> buttonRM = null!;
    private UIAutoScaleTextTextPanel<LocalizedText> buttonB = null!;
    private UIAutoScaleTextTextPanel<LocalizedText> buttonOMF = null!;
    private UIAutoScaleTextTextPanel<LocalizedText> buttonCL = null!;
    private UIAutoScaleTextTextPanel<string> buttonCreateFolder = null!;
    #endregion
    #region 排序与过滤相关字段
    public ModsMenuSortMode sortMode = ModsMenuSortMode.RecentlyUpdated;
    public EnabledFilter enabledFilterMode = EnabledFilter.All;
    public ModSideFilter modSideFilterMode = ModSideFilter.All;
    public SearchFilter searchFilterMode = SearchFilter.Name;
    internal string filter = string.Empty;
    #endregion
    #region 杂项
    private Task? modItemsTask;
    private CancellationTokenSource? _cts;
    private bool updateNeeded;
    public void SetUpdateNeeded() => updateNeeded = true;
    private bool needToRemoveLoading;
    public bool loading;
    private float listViewPosition;

    public UIState? PreviousUIState { get; set; }
    private Texture2D? _mouseTexture;
    private int _mouseTextureWidth;
    private int _mouseTextureHeight;
    private int _mouseTextureOffsetX;
    private int _mouseTextureOffsetY;
    private Color _mouseTextureColor;
    public void SetMouseTexture(Texture2D mouseTexture, int width = 0, int height = 0, int offsetX = 0, int offsetY = 0, Color color = default) {
        _mouseTexture = mouseTexture;
        _mouseTextureWidth = width == 0 ? mouseTexture.Width : width;
        _mouseTextureHeight = height == 0 ? mouseTexture.Height : height;
        _mouseTextureOffsetX = offsetX;
        _mouseTextureOffsetY = offsetY;
        _mouseTextureColor = color == default ? Color.White : color;
    }
    #endregion
    #endregion

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
            Height = { Pixels = -20, Percent = 1f },
            BackgroundColor = UICommon.MainPanelBackground,
            PaddingTop = 0f
        };
        uIElement.Append(uIPanel);
        #endregion
        #region 正在加载时的循环图标
        uiLoader = new UILoaderAnimatedImage(1, 1);
        uiLoader.Left.Pixels = -10;
        uiLoader.Top.Pixels = -10;
        #endregion
        #region 仨排序按钮
        int upperPixels = 10;
        int upperRowLeft = 8;
        var texture = UICommon.ModBrowserIconsTexture;
        var upperMenuContainer = new UIElement {
            Width = { Percent = 1f },
            Height = { Pixels = 32 },
            Top = { Pixels = 10 }
        };
        upperPixels += 32;
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
                toggleImage = new(texture, 3, 32, 32, 34 * 4, 0);
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
                toggleImage = new(texture, 5, 32, 32, 34 * 5, 0);
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
            toggleImage.Left.Pixels = upperRowLeft;
            upperRowLeft += 36;
            _categoryButtons.Add(toggleImage);
            upperMenuContainer.Append(toggleImage);
        }
        #endregion
        #region 搜索栏
        var filterTextBoxBackground = new UIPanel {
            Top = { Percent = 0f },
            Left = { Pixels = upperRowLeft, },
            Width = { Pixels = -upperRowLeft - 36, Percent = 1 },
            Height = { Pixels = 40 }
        };
        filterTextBoxBackground.SetPadding(0);
        filterTextBoxBackground.OnRightClick += ClearSearchField;
        upperMenuContainer.Append(filterTextBoxBackground);
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
            Left = { Pixels = -32, Percent = 1 }
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
        #region 文件夹路径
        upperPixels += 2;
        folderPathList = new();
        folderPathList.Top.Pixels = upperPixels;
        folderPathList.Height.Pixels = 30;
        folderPathList.HAlign = 0.5f;
        upperPixels += 30;
        folderPathList.Width.Set(-10, 1);
        folderPathList.SetPadding(1);
        folderPathList.ListPadding = 2;
        folderPathList.OnDraw += sb => {
            sb.DrawBox(folderPathList.GetDimensions().ToRectangle(), Color.Black * 0.6f, UICommon.DefaultUIBlue * 0.2f);
        };
        uIElement.Append(folderPathList);

        FolderPath = [FolderDataSystem.Root];
        #endregion
        #region 模组列表
        upperPixels += 6;
        list = new UIFolderItemList {
            Width = { Pixels = -25, Percent = 1f },
            Height = { Pixels = -upperPixels, Percent = 1f },
            Top = { Pixels = upperPixels },
            ListPadding = 2f,
        };
        uIPanel.Append(list);
        #endregion
        #region 内存占用 (禁用)
        /*
        if (ModLoader.showMemoryEstimates) {
            var ramUsage = new UIMemoryBar() {
                Top = { Pixels = 45 },
            };
            ramUsage.Width.Pixels = -25;
            uIPanel.Append(ramUsage);
        }
        */
        #endregion
        #region 滚条
        var uIScrollbar = new UIScrollbar {
            Height = { Pixels = -upperPixels, Percent = 1f },
            Top = { Pixels = upperPixels },
            HAlign = 1f
        }.WithView(100f, 1000f);
        uIPanel.Append(uIScrollbar);

        list.SetScrollbar(uIScrollbar);
        #endregion
        #region 标题
        var uIHeaderTexTPanel = new UITextPanel<LocalizedText>(Language.GetText("tModLoader.ModsModsList"), 0.8f, true) {
            Left = { Pixels = 20 },
            Top = { Pixels = 20 },
            BackgroundColor = UICommon.DefaultUIBlue
        }.WithPadding(15f);
        Append(uIHeaderTexTPanel);
        #endregion
        #region 启用全部按钮
        // TODO: 只启用或禁用本文件夹下的模组, 按住 shift 时才是所有模组
        // TODO: 按住 alt 同时包含子文件夹
        // TODO: 按住 ctrl 在禁用时同时禁用收藏
        // TODO: 使用悬浮文字以提示这些操作
        int buttonTopPixels = 220;
        buttonEA = new UIAutoScaleTextTextPanel<LocalizedText>(Language.GetText("tModLoader.ModsEnableAll")) {
            TextColor = Color.Green,
            Width = { Pixels = 200 },
            Height = { Pixels = 40 },
            Left = { Pixels = 20 },
            Top = { Pixels = buttonTopPixels },
        }.WithFadedMouseOver();
        buttonTopPixels += 40;
        buttonEA.OnLeftClick += EnableAllMods;
        Append(buttonEA);
        #endregion
        #region 禁用全部按钮
        // TODO CopyStyle doesn't capture all the duplication here, consider an inner method
        buttonDA = new UIAutoScaleTextTextPanel<LocalizedText>(Language.GetText("tModLoader.ModsDisableAll"));
        buttonDA.CopyStyle(buttonEA);
        buttonDA.Top.Pixels = buttonTopPixels + 5;
        buttonTopPixels += 45;
        buttonDA.TextColor = Color.Red;
        buttonDA.WithFadedMouseOver();
        buttonDA.OnLeftClick += DisableAllMods;
        Append(buttonDA);
        #endregion
        #region 重新加载按钮
        buttonRM = new UIAutoScaleTextTextPanel<LocalizedText>(Language.GetText("tModLoader.ModsForceReload"));
        buttonRM.CopyStyle(buttonEA);
        buttonRM.Top.Pixels = buttonTopPixels + 5;
        buttonTopPixels += 45;
        buttonRM.WithFadedMouseOver();
        buttonRM.OnLeftClick += (_, _) => {
            SoundEngine.PlaySound(SoundID.MenuOpen);
            if (ModItemDict.Count > 0)
                ModLoader.Reload();
        };
        Append(buttonRM);
        #endregion
        #region 打开模组文件夹按钮
        buttonOMF = new UIAutoScaleTextTextPanel<LocalizedText>(Language.GetText("tModLoader.ModsOpenModsFolders"));
        buttonOMF.CopyStyle(buttonEA);
        buttonOMF.Top.Pixels = buttonTopPixels + 5;
        buttonTopPixels += 45;
        buttonOMF.WithFadedMouseOver();
        buttonOMF.OnLeftClick += (_, _) => {
            SoundEngine.PlaySound(SoundID.MenuOpen);
            Directory.CreateDirectory(ModLoader.ModPath);
            Utils.OpenFolder(ModLoader.ModPath);

            if (ModOrganizer.WorkshopFileFinder.ModPaths.Count != 0) {
                string? workshopFolderPath = Directory.GetParent(ModOrganizer.WorkshopFileFinder.ModPaths[0])?.ToString();
                if (workshopFolderPath != null)
                    Utils.OpenFolder(workshopFolderPath);
            }
        };
        Append(buttonOMF);
        #endregion
        #region 模组配置按钮
        buttonCL = new UIAutoScaleTextTextPanel<LocalizedText>(Language.GetText("tModLoader.ModConfiguration"));
        buttonCL.CopyStyle(buttonEA);
        buttonCL.Top.Pixels = buttonTopPixels + 5;
        buttonTopPixels += 45;
        buttonCL.WithFadedMouseOver();
        buttonCL.OnLeftClick += (_, _) => {
            SoundEngine.PlaySound(SoundID.MenuOpen);
            Main.menuMode = Interface.modConfigListID;
            IsPreviousUIStateOfConfigList = true;
        };
        Append(buttonCL);
        #endregion
        #region 新建文件夹按钮
        // TODO: 本地化
        buttonCreateFolder = new UIAutoScaleTextTextPanel<string>("新建文件夹");
        buttonCreateFolder.CopyStyle(buttonEA);
        buttonCreateFolder.Top.Pixels = buttonTopPixels + 5;
        buttonTopPixels += 45;
        buttonCreateFolder.WithFadedMouseOver();
        buttonCreateFolder.OnLeftClick += (_, _) => {
            SoundEngine.PlaySound(SoundID.MenuTick);
            // TODO: 本地化
            FolderNode node = new("New Folder");
            CurrentFolderNode.Children.Add(node);
            nodeToRename = node;
            updateNeeded = true;
            // TODO: 自动设置输入重命名文件夹的名字
        };
        Append(buttonCreateFolder);
        #endregion
        #region 返回按钮
        // TODO: 在它之前添加一个重置模组启用状态的按钮 (使用直接操纵 ModLoader.EnabledMods 的方式修改, 最后再 ModOrganizer.SaveEnabledMods(), 还需要日志打印)
        buttonB = new UIAutoScaleTextTextPanel<LocalizedText>(Language.GetText("UI.Back"));
        buttonB.CopyStyle(buttonEA);
        buttonB.Top.Pixels = buttonTopPixels + 5;
        buttonTopPixels += 45;
        buttonB.WithFadedMouseOver();
        buttonB.OnLeftClick += (_, _) => {
            FolderDataSystem.Save();
            // To prevent entering the game with Configs that violate ReloadRequired
            if (ConfigManager.AnyModNeedsReload()) {
                Main.menuMode = Interface.reloadModsID;
                return;
            }

            // If auto reloading required mods is enabled, check if any mods need reloading and reload as required
            if (ModLoader.autoReloadRequiredModsLeavingModsScreen && ModItemDict.Values.Any(i => i.NeedsReload)) {
                Main.menuMode = Interface.reloadModsID;
                return;
            }

            ConfigManager.OnChangedAll();

            IHaveBackButtonCommand.GoBackTo(PreviousUIState);
        };

        Append(buttonB);
        #endregion
        uIPanel.Append(upperMenuContainer);
        Append(uIElement);
    }
    private FolderNode? nodeToRename;
    private void EnableAllMods(UIMouseEvent mouse, UIElement element) {
        SoundEngine.PlaySound(SoundID.MenuTick);
        // TODO: 未加载完成时给出(悬浮)提示
        if (!loaded) {
            return;
        }
        foreach (var modItem in ModItemDict.Values) {
            if (modItem.tMLUpdateRequired != null)
                continue;
            ModLoader.EnabledMods.Add(modItem.ModName);
        }
        // TODO: 已经启用的模组不再在这里提示?
        // Logging.tML.Info
        ModFolder.Instance.Logger.Info("Enabling All Mods: " + string.Join(", ", ModLoader.EnabledMods));
        ModOrganizer.SaveEnabledMods();
    }
    private void DisableAllMods(UIMouseEvent mouse, UIElement element) {
        SoundEngine.PlaySound(SoundID.MenuTick);
        // 不用在乎是否加载完成, 可一键全部取消
        // TODO: 只有在同时取消收藏且全部取消时这么做, 否则要判断是否加载完成
        ModLoader.DisableAllMods();
    }

    public static bool IsPreviousUIStateOfConfigList { get; set; }

    private void ClearSearchField(UIMouseEvent evt, UIElement listeningElement) => filterTextBox.Text = "";

    public UIModItemInFolder? FindUIModItem(string modName) {
        return ModItemDict.Values.SingleOrDefault(m => m.ModName == modName);
    }

    public override void Update(GameTime gameTime) {
        Update_RemoveChildrenToRemove();
        base.Update(gameTime);
        #region 当加载完成时做一些事情
        if (modItemsTask is { IsCompleted: true }) {
            foreach (var item in ModItemDict.Values) {
                item.Activate();
            }
            needToRemoveLoading = true;
            updateNeeded = true;
            loading = false;
            modItemsTask = null;
        }
        #endregion
        #region 尝试移除加载动画
        if (needToRemoveLoading) {
            needToRemoveLoading = false;
            RemoveChild(uiLoader);
        }
        #endregion

        #region 如果不需要更新就直接返回
        if (!updateNeeded)
            return;
        updateNeeded = false;
        #endregion

        filter = filterTextBox.Text;
        list.Clear();
        var filterResults = new UIModsFilterResults();
        var visibleItems = CurrentFolderNode.Children.Select(UIElement? (n) => {
            if (n is ModNode m) {
                return ModItemDict.TryGetValue(m.ModName, out var uiMod)
                    ? uiMod.PassFilters(filterResults) ? uiMod : (UIElement?)null
                    : new UIModItemInFolderUnloaded(m);
            }
            else if (n is FolderNode f) {
                var uf = new UIFolder(f);
                if (f == nodeToRename){
                    uf.SetReplaceToRenameText();
                    // TODO: 保证能够看见它
                }
                return uf;
            }
            return null;
        }).Where(e => e != null).ToList();
        nodeToRename = null;
        if (CurrentFolderNode == FolderDataSystem.Root) {
            // TODO: 缓存此结果, 且在文件夹树发生变化时做出修改
            HashSet<string> modsInFolder = [];
            foreach (var m in FolderDataSystem.Root.ModNodesInTree) {
                modsInFolder.Add(m.ModName);
            }
            foreach (var (key, value) in ModItemDict) {
                if (!modsInFolder.Contains(key) && value.PassFilters(filterResults)) {
                    visibleItems.Add(value);
                }
            }
        }
        else {
            UIFolder upperFolder = new("..");
            list.Add(upperFolder);
            upperFolder.Activate();
        }
        #region 若有任何被过滤的, 则在列表中添加一个元素提示过滤了多少东西
        if (filterResults.AnyFiltered) {
            var panel = new UIPanel();
            panel.Width.Set(0, 1f);
            list.Add(panel);
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
        #endregion
        list.AddRange(visibleItems);
        foreach (var item in visibleItems) {
            item?.Activate();
        }
        Recalculate();
        list.ViewPosition = listViewPosition;
        listViewPosition = 0;
    }

    public override void Draw(SpriteBatch spriteBatch) {
        UILinkPointNavigator.Shortcuts.BackButtonCommand = 7;
        base.Draw(spriteBatch);
        for (int i = 0; i < _categoryButtons.Count; i++) {
            if (_categoryButtons[i].IsMouseHovering) {
                string text = i switch
                {
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

        #region DrawMouseTexture
        if (_mouseTexture != null) {

            spriteBatch.Draw(_mouseTexture, new Rectangle(
                Math.Min(Main.mouseX + _mouseTextureOffsetX, Main.screenWidth - _mouseTextureWidth),
                Math.Min(Main.mouseY + _mouseTextureOffsetY, Main.screenHeight - _mouseTextureHeight),
                _mouseTextureWidth,
                _mouseTextureHeight), _mouseTextureColor);

            _mouseTexture = null;
        }
        #endregion
    }

    public override void OnActivate() {
        Main.clrInput();
        list.Clear();
        loading = true;
        if (!loaded) {
            Append(uiLoader);
        }
        ConfigManager.LoadAll(); // Makes sure MP configs are cleared.
        Populate();
    }

    public override void OnDeactivate() {
        if (_cts != null) {
            _cts.Cancel(false);
            _cts.Dispose();
            _cts = null;
        }
        listViewPosition = list.ViewPosition;
    }
    #region 异步寻找 Mod
    private bool loaded;
    private void FindModsTask() {
        if (loaded) {
            return;
        }
        var mods = ModOrganizer.FindMods(logDuplicates: true);
        ModItemDict.Clear();
        foreach (var mod in mods) {
            UIModItemInFolder modItem = new(mod);
            ModItemDict.Add(modItem.ModName, modItem);
        }
        loaded = true;
    }
    public void Populate() {
        _cts = new();
        modItemsTask = Task.Run(FindModsTask, _cts.Token);
    }
    public void Repopulate() {
        if (_cts != null) {
            _cts.Cancel(false);
            _cts.Dispose();
            _cts = null;
        }
        _cts = new();
        modItemsTask = Task.Run(FindModsTask, _cts.Token);
    }
    #endregion
    #region Arrange Remove
    private readonly List<UIElement> toRemove = [];
    private void Update_RemoveChildrenToRemove() {
        if (toRemove.Count == 0) {
            return;
        }
        foreach (var r in toRemove) {
            list.RemoveChild(r);
        }
    }
    public void ArrangeRemove(UIElement child) => toRemove.Add(child);
    #endregion
}
