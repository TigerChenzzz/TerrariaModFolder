using Humanizer;
using Microsoft.CodeAnalysis;
using ModFolder.Configs;
using ModFolder.Helpers;
using ModFolder.Systems;
using ModFolder.UI.Base;
using ModFolder.UI.Menu.Notification;
using ModFolder.UI.UIFolderItems;
using ModFolder.UI.UIFolderItems.Folder;
using ModFolder.UI.UIFolderItems.Mod;
using ReLogic.Content;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Terraria.Audio;
using Terraria.GameContent.UI.Elements;
using Terraria.GameContent.UI.States;
using Terraria.ModLoader.Config;
using Terraria.ModLoader.Core;
using Terraria.ModLoader.UI;
using Terraria.ModLoader.UI.ModBrowser;
using Terraria.Social.Steam;
using Terraria.UI;
using Terraria.UI.Gamepad;
using FolderNode = ModFolder.Systems.FolderDataSystem.FolderNode;
using ModNode = ModFolder.Systems.FolderDataSystem.ModNode;
using Node = ModFolder.Systems.FolderDataSystem.Node;

namespace ModFolder.UI.Menu;

// TODO: Esc 返回时同样尝试回到上一级目录
// TODO: 在进入时即刻生成 UI
public class UIModFolderMenu : UIState, IHaveBackButtonCommand {
    public static UIModFolderMenu Instance { get; private set; } = new();
    public int Timer { get; private set; }
    private static void Clear() {
        FolderDataSystem.Clear();
        Instance = new();
    }
    public static void TotallyReload() {
        Instance = new();
    }
    public static void EnterFrom(UIWorkshopHub hub) {
        SoundEngine.PlaySound(SoundID.MenuOpen);
        if (CommonConfig.Instance.TotallyReload) {
            TotallyReload();
            FolderDataSystem.Reload();
        }
        Instance.PreviousUIState = hub;
        Main.MenuUI.SetState(Instance); // 如果没有初始化的话会在这里初始化
        Instance.ResetCategoryButtons();
        Instance.SetListViewPositionAfterGenerated(0);
    }
    public const int MyMenuMode = 47133;

    #region 所有模组 ModItems
    /// <summary>
    /// <br/>当找完模组后, 这里会存有所有的模组
    /// <br/>对于当前显示在列表中的项见 <see cref="VisibleItems"/>
    /// </summary>
    public Dictionary<string, UIModItemInFolderLoaded> ModItemDict { get; set; } = [];
    public UIModItemInFolderLoaded? FindUIModItem(string modName) {
        return ModItemDict.GetValueOrDefault(modName);
    }
    #endregion

    #region 文件夹路径
    // Old: 若为空则代表处于根目录下
    /// <summary>
    /// 当前处于哪个文件夹下
    /// </summary>
    public FolderNode CurrentFolderNode => FolderPath[^1];
    public UIHorizontalList folderPathList = null!;
    private int folderPathListIndex;
    private readonly UIElement folderPathListPlaceHolder = new();

    private FolderPathClass? _folderPath;
    private FolderPathClass FolderPath {
        get {
            _folderPath ??= [];
            if (_folderPath.Count == 0) {
                _folderPath.Add(FolderDataSystem.Root);
            }
            return _folderPath;
        }
    }
    #region 文件夹跳转
    public void EnterFolder(FolderNode? folder) {
        folder ??= FolderDataSystem.Root;
        list.ViewPosition = 0;
        bool max = folderPathList.ViewPosition == folderPathList.MaxViewPosition;
        // TODO: 换成 Parent 检测
        if (folder.Parent == CurrentFolderNode) {
            FolderPath.Add(folder);
            goto ReadyToReturn;
        }
        _folderPath!.Clear();
        while (folder != null) {
            _folderPath.Add(folder);
            folder = folder.Parent;
        }
        _folderPath.Reverse();

    ReadyToReturn:
        ArrangeGenerate();
        if (max) {
            folderPathList.ViewPosition = folderPathList.MaxViewPosition;
        }
    }
    public void GotoUpperFolder() {
        if (FolderPath.Count <= 1) {
            return;
        }
        list.ViewPosition = 0;
        FolderPath.RemoveAt(FolderPath.Count - 1);
        ArrangeGenerate();
    }
    public void GotoUpperFolder(FolderNode folder) {
        for (int i = 0; i < FolderPath.Count - 1; ++i) {
            if (FolderPath[i] != folder) {
                continue;
            }
            list.ViewPosition = 0;
            FolderPath.RemoveRange(i + 1, FolderPath.Count - i - 1);
            ArrangeGenerate();
            break;
        }
    }
    #endregion
    #endregion

    #region 子元素
    private UIElement uiElement = null!;
    private UIPanel uiPanel = null!;
    private UIImagePro refreshButton = null!;
    private int refreshButtonIndex;
    private readonly UIElement refreshButtonPlaceHolder = new();
    private UIFolderItemList list = null!;
    private UIScrollbar uiScrollbar = null!;
    #region 下面的一堆按钮
    public class UIAutoScaleTextTextPanelWithFadedMouseOver<T> : UIAutoScaleTextTextPanel<T> {
        public UIAutoScaleTextTextPanelWithFadedMouseOver(T text) : base(text) {
            this.WithFadedMouseOver();
        }
    }
    private UIElement buttonsBg = null!;
    private UIAutoScaleTextTextPanelWithFadedMouseOver<LocalizedText> ButtonAllMods          { get => buttons[0]; set => buttons[0] = value; }
    private UIAutoScaleTextTextPanelWithFadedMouseOver<LocalizedText> ButtonOMF              { get => buttons[1]; set => buttons[1] = value; }
    private UIAutoScaleTextTextPanelWithFadedMouseOver<LocalizedText> ButtonCL               { get => buttons[2]; set => buttons[2] = value; }
    private UIAutoScaleTextTextPanelWithFadedMouseOver<LocalizedText> ButtonB                { get => buttons[3]; set => buttons[3] = value; }
    private UIAutoScaleTextTextPanelWithFadedMouseOver<LocalizedText> ButtonCreateFolder     { get => buttons[4]; set => buttons[4] = value; }
    private UIAutoScaleTextTextPanelWithFadedMouseOver<LocalizedText> ButtonMore             { get => buttons[5]; set => buttons[5] = value; }
    private UIAutoScaleTextTextPanelWithFadedMouseOver<LocalizedText> ButtonCopyEnabled      { get => buttons[6]; set => buttons[6] = value; }
    private UIAutoScaleTextTextPanelWithFadedMouseOver<LocalizedText> ButtonDisableRedundant { get => buttons[7]; set => buttons[7] = value; }
    private UIAutoScaleTextTextPanelWithFadedMouseOver<LocalizedText> ButtonUpdate           { get => buttons[8]; set => buttons[8] = value; }
    private UIAutoScaleTextTextPanelWithFadedMouseOver<LocalizedText> ButtonImport           { get => buttons[9]; set => buttons[9] = value; }

    private readonly UIAutoScaleTextTextPanelWithFadedMouseOver<LocalizedText>[] buttons = new UIAutoScaleTextTextPanelWithFadedMouseOver<LocalizedText>[10];
    private readonly UIAutoScaleTextTextPanel<string>[] buttonPlaceHolders = new UIAutoScaleTextTextPanel<string>[6];
    int buttonPage;
    readonly int buttonPageMax = 2;
    #endregion
    #endregion

    #region 首部菜单 (排序与过滤相关以及内存条)
    #region 公开属性
    public bool                ShowAllMods       { get => _topButtonData[0].ToBoolean(); set => _topButtonData[0] = value.ToInt(); }
    public bool                ShowFolderSystem  { get => !_topButtonData[0].ToBoolean(); set => _topButtonData[0] = (!value).ToInt(); }
    public FolderModSortMode   FmSortMode        { get => (FolderModSortMode  )_topButtonData[1]; set => _topButtonData[1] = (int)value; }
    public FolderMenuSortMode  SortMode          { get => (FolderMenuSortMode )_topButtonData[2]; set => _topButtonData[2] = (int)value; }
    public ModLoadedFilter     LoadedFilterMode  { get => (ModLoadedFilter    )_topButtonData[3]; set => _topButtonData[3] = (int)value; }
    public FolderEnabledFilter EnabledFilterMode { get => (FolderEnabledFilter)_topButtonData[4]; set => _topButtonData[4] = (int)value; }
    public ModSideFilter       ModSideFilterMode { get => (ModSideFilter      )_topButtonData[5]; set => _topButtonData[5] = (int)value; }
    public LayoutTypes         LayoutType        { get => (LayoutTypes        )_topButtonData[6]; set => _topButtonData[6] = (int)value; }
    public bool                ShowRamUsage      { get => _topButtonData[7].ToBoolean(); set => _topButtonData[7] = value.ToInt(); }
    #endregion
    #region 数据与常数
    // 0: 文件夹系统 / 显示全部模组
    // 1: 文件夹和模组的排序
    // 2: 排序
    // 3: 加载状态
    // 4: 启用状态
    // 5: 客户端 / 服务端
    // 6: <条状 / 块状>布局
    // 7: 内存条
    private const int IndexShowFolderSystem = 0;
    private const int IndexFmSortMode = 1;
    private const int IndexSortMode = 2;
    private const int IndexLayoutType = 6;
    private const int IndexShowRamUsage = 7;
    private const int TopMenuButtonsCount = 8;
    private readonly int[] _topButtonData = new int[8];
    private readonly int[] _topButtonLengths = [2, 3, 5, 3, 8, 5, 2, 2];
    private readonly Point[] _topButtonPositionsInTexture = [
        new(2, 6), // 0
        new(0, 5), // 1
        new(0, 0), // 2
        new(3, 4), // 3
        new(1, 0), // 4
        new(2, 0), // 5
        new(4, 0), // 6
        new(3, 2), // 7
    ];
    private readonly string[][] _topButtonLocalizedKeys = [
        /* 0 */[
            "Mods.ModFolder.UI.SortButtons.FolderSystem.Tooltip",
            "Mods.ModFolder.UI.SortButtons.AllMods.Tooltip",
        ],
        /* 1 */[
            "Mods.ModFolder.UI.SortButtons.CustomFM.Tooltip",
            "Mods.ModFolder.UI.SortButtons.FolderFirst.Tooltip",
            "Mods.ModFolder.UI.SortButtons.ModFirst.Tooltip",
        ],
        /* 2 */[
            "Mods.ModFolder.UI.SortButtons.Custom.Tooltip",
            "tModLoader.ModsSortRecently",
            "Mods.ModFolder.UI.SortButtons.ReverseRecently.Tooltip",
            "tModLoader.ModsSortNamesAlph",
            "tModLoader.ModsSortNamesReverseAlph",
        ],
        /* 3 */[
            "tModLoader.ModsShowAllMods",
            "Mods.ModFolder.UI.SortButtons.Loaded.Tooltip",
            "Mods.ModFolder.UI.SortButtons.Unloaded.Tooltip",
        ],
        /* 4 */[
            "tModLoader.ModsShowAllMods",
            "tModLoader.ModsShowEnabledMods",
            "tModLoader.ModsShowDisabledMods",
            "Mods.ModFolder.UI.SortButtons.ToBeEnabled.Tooltip",
            "Mods.ModFolder.UI.SortButtons.ToBeDisabled.Tooltip",
            "Mods.ModFolder.UI.SortButtons.ToBeToggled.Tooltip",
            "Mods.ModFolder.UI.SortButtons.WouldBeEnabled.Tooltip",
            "Mods.ModFolder.UI.SortButtons.WouldBeDisabled.Tooltip",
        ],
        /* 5 */[
            "tModLoader.MBShowMSAll",
            "tModLoader.MBShowMSBoth",
            "tModLoader.MBShowMSClient",
            "tModLoader.MBShowMSServer",
            "tModLoader.MBShowMSNoSync",
        ],
        /* 6 */[
            "Mods.ModFolder.UI.SortButtons.StripeLayout.Tooltip",
            "Mods.ModFolder.UI.SortButtons.BlockLayout.Tooltip",
        ],
        /* 7 */[
            "tModLoader.ShowMemoryEstimatesNo",
            "tModLoader.ShowMemoryEstimatesYes",
        ],
    ];
    #endregion
    #region UI 条
    private UIElement upperMenuContainer = null!;
    private void OnInitialize_UpperMenuContainer(ref float upperPixels) {
        upperMenuContainer = new UIElement {
            Width = { Percent = 1f },
            Height = { Pixels = 32 },
            Top = { Pixels = upperPixels }
        };
        upperPixels += 32;
        var texture = MTextures.UI("SortIcons");
        OnInitialize_SortButtons(texture);
        OnInitialize_SearchFilter(texture);
    }
    #endregion
    #region 内存占用
    private readonly UIMemoryBar ramUsage = new();
    private int ramUsageIndex;
    private readonly UIElement ramUsagePlaceHolder = new();
    #endregion

    #region 首部菜单的按钮
    private void OnInitialize_SortButtons(Asset<Texture2D> texture) {
        OnInitialize_ButtonToggleShowButtons(texture); // 总按钮
        OnInitialize_TopButtonsBg(); // 其余按钮的背景板
        OnInitialize_TopButtons(texture);
    }
    #region 总按钮
    private UIImagePro buttonToggleShowButtons = null!;
    private void OnInitialize_ButtonToggleShowButtons(Asset<Texture2D> texture) {
        buttonToggleShowButtons = new(texture) {
            Width = { Pixels = 32 },
            Height = { Pixels = 32 },
            SourceRectangle = new(2 * 34, 5 * 34, 32, 32),
        };
        // 左键切换开关
        buttonToggleShowButtons.OnLeftClick += (_, _) => ToggleShowButtons();
        // 中键重置
        buttonToggleShowButtons.OnMiddleClick += (_, _) => ResetCategoryButtons();
        // 点到其它位置时关闭
        OnLeftMouseDown += (_, _) => {
            if (!buttonToggleShowButtons.IsMouseHovering && !topButtonsBg.IsMouseHovering) {
                ArrangeCloseButtons();
            }
        };
        // 当鼠标放上去时高亮
        buttonToggleShowButtons.PreDrawSelf += spriteBatch => {
            buttonToggleShowButtons.Color = buttonToggleShowButtons.IsMouseHovering ? Color.White : Color.Silver;
        };
        // 当鼠标放上去时同时也有说明文字
        mouseOverTooltips.Add((buttonToggleShowButtons, () => _showButtons
            ? ModFolder.Instance.GetLocalization("UI.Buttons.ToggleShowButtons.TooltipOff").Value
            : ModFolder.Instance.GetLocalization("UI.Buttons.ToggleShowButtons.TooltipOn").Value));
        // 添加到搜索过滤条上
        upperMenuContainer.Append(buttonToggleShowButtons);
    }
    private void ResetCategoryButtons() {
        for (int i = 0; i < categoryButtons.Length; ++i) {
            var defaultValue = GetCategoryDefaultValue(i);
            _topButtonData[i] = defaultValue;
            var button = categoryButtons[i];
            button.SetCurrentState(defaultValue);
            button.Disabled = false;
        }
        Filter = string.Empty;
        GenerateTopButtons();
        ResettleVertical();
        ArrangeGenerate();
    }
    #endregion
    #region 其余按钮的背景板
    private UIElement topButtonsBg = null!;
    private void OnInitialize_TopButtonsBg() {
        topButtonsBg = new() {
            Left = { Pixels = 36 },
            Height = { Percent = 1 },
        };
        upperMenuContainer.Append(topButtonsBg);
    }
    #endregion
    #region 其余按钮
    private readonly UICycleImage[] topMenuButtons = new UICycleImage[TopMenuButtonsCount];
    private readonly int categoryButtonStartIndex = 0;
    private readonly UICycleImage[] categoryButtons = new UICycleImage[6];
    private void OnInitialize_TopButtons(Asset<Texture2D> texture) {
        UICycleImage toggleImage;
        for (int i = 0; i < _topButtonData.Length; i++) {
            topMenuButtons[i] = toggleImage = new(texture, _topButtonLengths[i], 32, 32, _topButtonPositionsInTexture[i].X * 34, _topButtonPositionsInTexture[i].Y * 34);
            ResetTopButton(i);
            int currentIndex = i;
            toggleImage.OnLeftClick += (_, _) => SwitchTopButtonNext(currentIndex);
            toggleImage.OnRightClick += (_, _) => SwitchTopButtonPrevious(currentIndex);
            toggleImage.OnMiddleClick += (_, _) => ResetTopButton(currentIndex);
            if ((i - categoryButtonStartIndex).IsBetween(0, categoryButtons.Length)) {
                categoryButtons[i - categoryButtonStartIndex] = toggleImage;
            }
            mouseOverTooltips.Add((toggleImage, () => {
                var result = Language.GetTextValue(_topButtonLocalizedKeys[currentIndex][_topButtonData[currentIndex]]);
                if (topMenuButtons[currentIndex].Disabled) {
                    return ModFolder.Instance.GetLocalizedValue("UI.CycleImageDisabled").FormatWith(result);
                }
                return result;
            }));
        }
    }
    #region 按钮切换
    // 当切换 ShowAllMods 时同时还要改变 FmSortMode 按钮的可用性
    // 同时 ShowAllMods 为真时 SortMode 不能为 Custom (0)

    private void SwitchTopButtonNext(int index) {
        var button = topMenuButtons[index];
        if (button.Disabled) {
            return;
        }
        if (index == IndexSortMode && ShowAllMods) {
            _topButtonData[index] = _topButtonData[index] % (_topButtonLengths[index] - 1) + 1;
            button.SetCurrentState(_topButtonData[index]);
        }
        else {
            _topButtonData[index] = (_topButtonData[index] + 1) % _topButtonLengths[index];
        }
        CheckTopButtonChanged(index);
        ArrangeGenerate();
    }
    private void SwitchTopButtonPrevious(int index) {
        var button = topMenuButtons[index];
        if (button.Disabled) {
            return;
        }
        if (index == IndexSortMode && ShowAllMods) {
            if (_topButtonData[index] <= 1) {
                _topButtonData[index] = _topButtonLengths[index] - 1;
                button.SetCurrentState(_topButtonData[index]);
            }
            else {
                _topButtonData[index] -= 1;
            }
        }
        else {
            if (_topButtonData[index] == 0) {
                _topButtonData[index] = _topButtonLengths[index] - 1;
            }
            else {
                _topButtonData[index] -= 1;
            }
        }
        CheckTopButtonChanged(index);
        ArrangeGenerate();
    }
    private void ResetTopButton(int index) {
        var button = topMenuButtons[index];
        if (button.Disabled) {
            return;
        }
        var defaultValue = GetCategoryDefaultValue(index);
        if (_topButtonData[index] == defaultValue) {
            return;
        }
        _topButtonData[index] = defaultValue;
        button.SetCurrentState(defaultValue);
        CheckTopButtonChanged(index);
        ArrangeGenerate();
    }
    private void CheckTopButtonChanged(int index) {
        if (index == IndexShowFolderSystem) {
            topMenuButtons[IndexFmSortMode].Disabled = ShowAllMods;
            if (SortMode == FolderMenuSortMode.Custom && ShowAllMods) {
                ResetTopButton(IndexSortMode);
            }
            ResettleVertical();
        }
        else if (index == IndexShowRamUsage) {
            ResettleVertical();
        }
    }
    private int GetCategoryDefaultValue(int index) {
        if (index == IndexSortMode) {
            return ShowAllMods ? 1 : 0;
        }
        if (index == IndexLayoutType) {
            return CommonConfig.Instance.UseBlockLayoutByDefault ? (int)LayoutTypes.Block : (int)LayoutTypes.Stripe;
        }
        return 0;
    }
    #endregion
    #endregion
    #region 打开与关闭
    private bool _showButtons;
    private bool _toCloseCategoryButtons;
    private void ArrangeCloseButtons() => _toCloseCategoryButtons = true;
    private void Draw_TryCloseButtons() {
        if (_toCloseCategoryButtons) {
            _toCloseCategoryButtons = false;
            CloseTopButtons();
        }
    }
    private void OpenTopButtons() {
        if (_showButtons) {
            return;
        }
        _showButtons = true;
        GenerateTopButtons();
    }
    private void CloseTopButtons() {
        if (!_showButtons) {
            return;
        }
        _showButtons = false;
        GenerateTopButtons();
    }
    private void ToggleShowButtons() {
        if (_showButtons) {
            CloseTopButtons();
        }
        else {
            OpenTopButtons();
        }
    }
    private void GenerateTopButtons() {
        // 清除所有按钮
        topButtonsBg.RemoveAllChildren();
        int width;
        // 如果不显示按钮, 则返回
        if (!_showButtons) {
            // 宽度 -4 用以抵消两边的边距
            width = -4;
            topButtonsBg.Width.Pixels = 0;
            goto ReadyToReturn;
        }

        width = (32 + 4) * topMenuButtons.Length - 4;
        topButtonsBg.Width.Pixels = width;
        for (int i = 0; i < topMenuButtons.Length; ++i) {
            var button = topMenuButtons[i];
            button.Left.Pixels = i * (32 + 4);
            topButtonsBg.Append(button);
        }
    ReadyToReturn:
        // 在返回前将按钮右边的搜索框弄好
        filterTextBoxBackground.Left.Pixels = 40 + width;
        filterTextBoxBackground.Width.Pixels = -width - 76;
        upperMenuContainer.RecalculateChildren();
    }
    #endregion
    #endregion
    #region 搜索栏
    private UIPanel filterTextBoxBackground = null!;
    private UIInputTextField filterTextBox = null!;
    private UICycleImage SearchFilterToggle = null!;
    private void OnInitialize_SearchFilter(Asset<Texture2D> texture) {
        filterTextBoxBackground = new UIPanel {
            Top = { Percent = 0f },
            Left = { Pixels = 36, },
            Width = { Pixels = -36 - 36, Percent = 1 },
            Height = { Percent = 1 }
        };
        filterTextBoxBackground.SetPadding(0);
        filterTextBoxBackground.OnRightClick += ClearSearchField;
        upperMenuContainer.Append(filterTextBoxBackground);
        filterTextBox = new UIInputTextField(Language.GetTextValue("tModLoader.ModsTypeToSearch")) {
            Height = { Percent = 1f },
            Width = { Percent = 1f },
            Left = { Pixels = 5 },
            Top = { Pixels = 5 },
        };
        filterTextBox.OnTextChange += (_, _) => ArrangeGenerate();
        filterTextBoxBackground.Append(filterTextBox);

        #region 取消搜索按钮
        UIImageButton clearSearchButton = new(Main.Assets.Request<Texture2D>("Images/UI/SearchCancel")) {
            HAlign = 1f,
            VAlign = 0.5f,
            Left = { Pixels = -2 },
        };

        //clearSearchButton.OnMouseOver += searchCancelButton_OnMouseOver;
        clearSearchButton.OnLeftClick += ClearSearchField;
        filterTextBoxBackground.Append(clearSearchButton);
        #endregion
        #region 搜索过滤器按钮
        // TODO: 添加一个是否过滤文件夹的按钮
        SearchFilterToggle = new(texture, 2, 32, 32, 34 * 3, 0) {
            Left = { Pixels = -32, Percent = 1 }
        };
        SearchFilterToggle.OnLeftClick += (_, _) => {
            searchFilterMode = searchFilterMode.NextEnum();
            if (Filter != string.Empty) {
                ArrangeGenerate();
            }
        };
        SearchFilterToggle.OnRightClick += (_, _) => {
            searchFilterMode = searchFilterMode.PreviousEnum();
            if (Filter != string.Empty) {
                ArrangeGenerate();
            }
        };
        upperMenuContainer.Append(SearchFilterToggle);
        mouseOverTooltips.Add((SearchFilterToggle, () => searchFilterMode.ToFriendlyString()));
        #endregion
    }
    public SearchFilter searchFilterMode = SearchFilter.Name;
    public string Filter { get => filterTextBox.Text; set => filterTextBox.Text = value; }
    private void ClearSearchField(UIMouseEvent evt, UIElement listeningElement) => Filter = string.Empty;
    #endregion

    private bool CanCustomizeOrder() {
        var end = categoryButtonStartIndex + categoryButtons.Length;
        for (int i = categoryButtonStartIndex; i < _topButtonData.Length - 2; ++i) {
            if (_topButtonData[i] != 0) {
                return false;
            }
        }
        return Filter == string.Empty;
    }
    #endregion

    #region 检测配置修改
    private bool _showModVersion = CommonConfig.Instance.ShowModVersion;
    private bool _showModLocation = CommonConfig.Instance.ShowModLocation;
    private void Update_DetectConfigChange() {
        bool configChanged = false;
        if (CommonConfig.Instance.ShowModLocation != _showModLocation) {
            _showModLocation = CommonConfig.Instance.ShowModLocation;
            configChanged = true;
        }
        if (CommonConfig.Instance.ShowModVersion != _showModVersion) {
            _showModVersion = CommonConfig.Instance.ShowModVersion;
            configChanged = true;
        }
        if (configChanged) {
            Populate();
        }
    }
    #endregion
    #region 确认弹窗
    private readonly List<(UIElement panel, Action? onRemoved)> _confirmPanels = [];
    private UIImage? _confirmPanelCover;
    private UIImage ConfirmPanelCover {
        get {
            if (_confirmPanelCover != null) {
                return _confirmPanelCover;
            }
            _confirmPanelCover = new(MTextures.White) {
                Width = { Percent = 1 },
                Height = { Percent = 1 },
                Color = Color.Black * 0.2f,
                ScaleToFit = true
            };
            void RemoveConfirmPanelForMouseEvent(UIMouseEvent mouse, UIElement element) {
                RemoveConfirmPanel();
            }
            _confirmPanelCover.OnLeftMouseDown += RemoveConfirmPanelForMouseEvent;
            _confirmPanelCover.OnRightMouseDown += RemoveConfirmPanelForMouseEvent;
            _confirmPanelCover.OnMiddleMouseDown += RemoveConfirmPanelForMouseEvent;
            _confirmPanelCover.OnXButton1MouseDown += RemoveConfirmPanelForMouseEvent;
            _confirmPanelCover.OnXButton2MouseDown += RemoveConfirmPanelForMouseEvent;
            return _confirmPanelCover;
        }
    }
    public void AppendConfirmPanel(UIElement panel, Action? onRemoved = null) {
        _confirmPanels.Add((panel, onRemoved));
        Append(ConfirmPanelCover);
        Append(panel);
    }
    public void RemoveConfirmPanel(bool silence = false) {
        if (!silence) {
            SoundEngine.PlaySound(SoundID.MenuClose);
        }
        if (_confirmPanels.Count == 0) {
            return;
        }
        var (panel, onRemoved) = _confirmPanels[^1];
        panel.Remove();
        onRemoved?.Invoke();
        _confirmPanels.RemoveAt(_confirmPanels.Count - 1);
        for (int i = Elements.Count - 1; i >= 0; --i) {
            if (Elements[i] == ConfirmPanelCover) {
                Elements.RemoveAt(i);
            }
        }
        if (_confirmPanels.Count == 0) {
            ConfirmPanelCover.Parent = null;
        }
    }
    public void ClearConfirmPanels(bool silence = false) {
        if (_confirmPanels.Count == 0) {
            return;
        }
        if (!silence) {
            SoundEngine.PlaySound(SoundID.MenuClose);
        }
        for (int i = _confirmPanels.Count - 1; i >= 0; --i) {
            var (panel, onRemoved) = _confirmPanels[i];
            panel.Remove();
            onRemoved?.Invoke();
            ConfirmPanelCover.Remove();
        }
        _confirmPanels.Clear();
    }
    #endregion
    #region OnInitialize, OnActivate, OnDeactivate
    public override void OnInitialize() {
        #region 全部元素的容器
        // UICommon.MaxPanelWidth  // 600
        uiElement = new UIElement {
            Width = { Percent = 0.8f },
            MaxWidth = UICommon.MaxPanelWidth,
            Top = { Pixels = 220 },
            Height = { Pixels = -220, Percent = 1f },
            HAlign = 0.5f,
        };
        #endregion
        #region 除开下面按钮之外的面板
        uiPanel = new UIPanel {
            Width = { Percent = 1f },
            Height = { Pixels = -110, Percent = 1f }, // -110
            BackgroundColor = UICommon.MainPanelBackground,
            PaddingTop = 0f,
            HAlign = 0.5f,
        };
        uiElement.Append(uiPanel);
        #endregion
        OnInitialize_Loading(); // 正在加载时的循环图标
        float upperPixels = 10;
        OnInitialize_UpperMenuContainer(ref upperPixels);   // 排序与过滤的条
        #region 文件夹路径
        upperPixels += 2;
        folderPathList = new() {
            Top = { Pixels = upperPixels },
            Height = { Pixels = 30 },
            Left = { Pixels = 5 },
            Width = { Pixels = -40, Percent = 1 },
        };
        folderPathList.SetPadding(1);
        folderPathList.ListPadding = 2;
        folderPathList.OnDraw += sb => {
            sb.DrawBox(folderPathList.GetDimensions().ToRectangle(), Color.Black * 0.6f, UICommon.DefaultUIBlue * 0.2f);
        };
        folderPathListIndex = uiPanel.AppendAndGetIndex(folderPathList);
        #endregion
        #region 刷新按钮
        #region refresh3 版
        refreshButton = new(MTextures.UI("Refresh3")) {
            Width = { Pixels = 30 },
            Height = { Pixels = 30 },
            Left = { Pixels = -35, Precent = 1 },
            Top = { Pixels = upperPixels },
            ScaleToFit = true,
            AllowResizingDimensions = false,
        };
        upperPixels += 30;
        bool refreshButtonPressed = false;
        refreshButton.OnLeftClick += Refresh;
        refreshButton.SourceRectangle = new(0, 0, 30, 30);
        refreshButton.OnLeftMouseDown += (_, _) => {
            refreshButtonPressed = true;
        };
        OnLeftMouseUp += (_, _) => {
            refreshButtonPressed = false;
        };
        refreshButton.PreDrawSelf += spriteBatch => {
            if (!refreshButton.IsMouseHovering) {
                refreshButton.SourceRectangle = new(0, 0, 30, 30);
            }
            else if (refreshButtonPressed) {
                refreshButton.SourceRectangle = new(60, 0, 30, 30);
            }
            else {
                refreshButton.SourceRectangle = new(30, 0, 30, 30);
            }
        };
        refreshButtonIndex = uiPanel.AppendAndGetIndex(refreshButton);
        #endregion
        #region 单图标 + highlight 版
        /*
        refreshButton = new(Textures.UI("Refresh"));
        refreshButton.Width.Pixels = 30;
        refreshButton.Height.Pixels = 30;
        refreshButton.Left.Set(-35, 1);
        refreshButton.Top.Pixels = upperPixels;
        upperPixels += 30;
        refreshButton.ScaleToFit = true;
        refreshButton.AllowResizingDimensions = false;
        bool refreshButtonPressed = false;
        refreshButton.OnLeftClick += Refresh;
        refreshButton.OnLeftMouseDown += (_, _) => {
            refreshButtonPressed = true;
        };
        OnLeftMouseUp += (_, _) => {
            refreshButtonPressed = false;
        };
        refreshButton.PreDrawSelf += spriteBatch => {
            if (refreshButton.IsMouseHovering) {
                spriteBatch.DrawBox(refreshButton._dimensions.ToRectangle(), Color.White * 0.8f, Color.White * (refreshButtonPressed ? 0.2f : 0.1f));
            }
        };
        uIPanel.Append(refreshButton);
        */
        #endregion
        #endregion
        #region 模组列表
        upperPixels += 6;
        list = new UIFolderItemList {
            Width = { Pixels = -25, Percent = 1f },
            Height = { Pixels = -upperPixels, Percent = 1f },
            Top = { Pixels = upperPixels },
            ListPadding = 2f,
        };
        uiPanel.Append(list);
        #endregion
        #region 内存占用
        ramUsageIndex = uiPanel.AppendAndGetIndex(ramUsagePlaceHolder);
        #endregion
        #region 滚条
        // TODO: 点按这个滚条会产生一个偏移的 bug
        uiScrollbar = new UIScrollbar {
            Height = { Pixels = -upperPixels, Percent = 1f },
            Top = { Pixels = upperPixels },
            HAlign = 1f
        }.WithView(100f, 1000f);
        uiPanel.Append(uiScrollbar);

        list.SetScrollbar(uiScrollbar);
        #endregion
        #region 标题
        var uIHeaderTexTPanel = new UITextPanel<LocalizedText>(Language.GetText("tModLoader.ModsModsList"), 0.8f, true) {
            Left = { Pixels = 20 },
            Top = { Pixels = 20 },
            BackgroundColor = UICommon.DefaultUIBlue
        }.WithPadding(15f);
        Append(uIHeaderTexTPanel);
        #endregion
        #region 底下的按钮
        #region 背景
        buttonsBg = new() {
            Width = { Percent = 1 },
            Height = { Pixels = 85 },
            Top = { Pixels = -105, Percent = 1 },
        };
        uiElement.Append(buttonsBg);
        #endregion
        #region 启用与禁用按钮
        ButtonAllMods = new(ModFolder.Instance.GetLocalization("UI.Buttons.AllMods.DisplayName"));
        ButtonAllMods.OnLeftMouseDown += (_, _) => {
            suppressLeftMouseDownClearSelecting = true;
        };
        ButtonAllMods.OnRightMouseDown += (_, _) => {
            suppressRightMouseDownClearSelecting = true;
        };
        ButtonAllMods.OnLeftClick += (_, _) => {
            if (!Main.keyState.PressingControl()) {
                EnableMods();
            }
            else {
                DisableMods(true, true);
            }
        };
        ButtonAllMods.OnRightClick += (_, _) => DisableMods(false, !Main.keyState.PressingControl());
        ButtonAllMods.OnMiddleClick += (_, _) => {
            if (!Main.keyState.PressingControl()) {
                ResetMods();
            }
            else {
                DisableMods(true, false);
            }
        };
        mouseOverTooltips.Add((ButtonAllMods, () => ModFolder.Instance.GetLocalization("UI.Buttons.AllMods.Tooltip").Value));
        #endregion
        #region 打开模组文件夹按钮
        ButtonOMF = new(Language.GetText("tModLoader.ModsOpenModsFolders"));
        ButtonOMF.OnLeftClick += (_, _) => {
            SoundEngine.PlaySound(SoundID.MenuOpen);
            Directory.CreateDirectory(ModLoader.ModPath);
            Utils.OpenFolder(ModLoader.ModPath);

            if (ModOrganizer.WorkshopFileFinder.ModPaths.Count != 0) {
                string? workshopFolderPath = Directory.GetParent(ModOrganizer.WorkshopFileFinder.ModPaths[0])?.ToString();
                if (workshopFolderPath != null)
                    Utils.OpenFolder(workshopFolderPath);
            }
        };
        mouseOverTooltips.Add((ButtonOMF, () => Language.GetTextValue("tModLoader.ModsOpenModsFoldersTooltip")));
        #endregion
        #region 模组配置按钮
        ButtonCL = new(Language.GetText("tModLoader.ModConfiguration"));
        ButtonCL.OnLeftClick += (_, _) => {
            SoundEngine.PlaySound(SoundID.MenuOpen);
            Main.menuMode = Interface.modConfigListID;
            IsPreviousUIStateOfConfigList = true;
        };
        #endregion
        #region 更多按钮按钮
        ButtonMore = new(ModFolder.Instance.GetLocalization("UI.Buttons.More.DisplayName"));
        ButtonMore.OnLeftMouseDown += (_, _) => {
            suppressLeftMouseDownClearSelecting = true;
        };
        ButtonMore.OnLeftClick += (_, _) => {
            SoundEngine.PlaySound(SoundID.MenuTick);
            buttonPage = (buttonPage + 1) % buttonPageMax;
            SettleBottomButtons();
        };
        #endregion
        #region 新建文件夹按钮
        ButtonCreateFolder = new(ModFolder.Instance.GetLocalization("UI.Buttons.CreateFolder.DisplayName"));
        ButtonCreateFolder.OnLeftClick += (_, _) => {
            SoundEngine.PlaySound(SoundID.MenuTick);
            FolderNode node = new(ModFolder.Instance.GetLocalization("UI.NewFolderDefaultName").Value);
            CurrentFolderNode.SetChildAtTheTop(node);
            nodeToRename = node;
            list.ViewPosition = 0;
        };
        #endregion
        #region 返回按钮
        ButtonB = new(Language.GetText("UI.Back"));
        ButtonB.OnLeftClick += (_, _) => BackButtonClicked();
        mouseOverTooltips.Add((ButtonB, () => ModFolder.Instance.GetLocalization("UI.Buttons.Back.Tooltip").Value));
        #endregion
        #region 复制已启用模组到此处
        ButtonCopyEnabled = new(ModFolder.Instance.GetLocalization("UI.Buttons.CopyEnabled.DisplayName"));
        ButtonCopyEnabled.OnLeftClick += (_, _) => {
            if (CurrentFolderNode == FolderDataSystem.Root || ShowAllMods) {
                SoundEngine.PlaySound(SoundID.MenuClose);
                return;
            }
            SoundEngine.PlaySound(SoundID.MenuTick);
            if (Main.keyState.PressingControl()) {
                CurrentFolderNode.ClearChildrenF();
            }
            if (Main.keyState.PressingShift()) {
                foreach (var mod in ModItemDict.Values) {
                    if (mod.TheLocalMod.Enabled) {
                        _ = new ModNode(mod.ModName) {
                            ParentF = CurrentFolderNode
                        };
                    }
                }
            }
            else {
                foreach (var mod in ModItemDict.Values) {
                    if (mod.Loaded) {
                        _ = new ModNode(mod.ModName) {
                            ParentF = CurrentFolderNode
                        };
                    }
                }
            }
            FolderDataSystem.TreeChanged();
        };
        mouseOverTooltips.Add((ButtonCopyEnabled, () => ModFolder.Instance.GetLocalization("UI.Buttons.CopyEnabled.Tooltip").Value));
        #endregion
        #region 禁用冗余前置
        ButtonDisableRedundant = new(ModFolder.Instance.GetLocalization("UI.Buttons.DisableRedundant.DisplayName"));
        ButtonDisableRedundant.OnLeftClick += (_, _) => DisableRedundant(false);
        ButtonDisableRedundant.OnRightClick += (_, _) => DisableRedundant(true);
        mouseOverTooltips.Add((ButtonDisableRedundant, () => ModFolder.Instance.GetLocalization("UI.Buttons.DisableRedundant.Tooltip").Value));
        #endregion
        #region 更新模组
        ButtonUpdate = new(ModFolder.Instance.GetLocalization("UI.Buttons.Update.DisplayName"));
        ButtonUpdate.OnLeftClick += (_, _) => ButtonUpdateClicked();
        mouseOverTooltips.Add((ButtonDisableRedundant, () => ModFolder.Instance.GetLocalization("UI.Buttons.Update.Tooltip").Value));
        #endregion
        #region 导入文件夹
        ButtonImport = new(ModFolder.Instance.GetLocalization("UI.Buttons.Import.DisplayName"));
        ButtonImport.OnLeftClick += (_, _) => {
            SoundEngine.PlaySound(SoundID.MenuTick);
            if (ShowAllMods) {
                PopupInfoByKey("UI.PopupInfos.ImportNotAllowedWhenShowingAllMods");
                return;
            }
            var result = ShareHelper.Import(CurrentFolderNode, Main.keyState.PressingShift(), Main.keyState.PressingAlt());
            switch (result) {
            case ShareHelper.ImportResult.Success:
                PopupInfoByKey("UI.PopupInfos.Imported");
                break;
            case ShareHelper.ImportResult.InvalidClipboard:
                PopupInfoByKey("UI.PopupInfos.ImportClipboardInvalid");
                break;
            }
        };
        mouseOverTooltips.Add((ButtonDisableRedundant, () => ModFolder.Instance.GetLocalization("UI.Buttons.Import.Tooltip").Value));
        #endregion

        #region 按钮占位符
        for (int i = 0; i < buttonPlaceHolders.Length; ++i) {
            buttonPlaceHolders[i] = new(string.Empty);
            buttonPlaceHolders[i].BackgroundColor *= 0.5f;
            // buttonPlaceHolders[i].BackgroundColor.A *= 2;
        }
        #endregion
        #region 按钮处理位置
        SettleBottomButtons(false);
        OnLeftMouseDown += (_, _) => {
            if (!buttonsBg.ContainsPoint(Main.MouseScreen)) {
                buttonPage = 0;
                SettleBottomButtons();
            }
        };
        #endregion
        #endregion
        // 最后添加搜索过滤条, 防止输入框被完全占用 (如果在 list 之前那么就没法重命名了)
        uiPanel.Append(upperMenuContainer);
        Append(uiElement);
        OnInitialize_Debug();
    }
    [Conditional("DEBUG")]
    private void OnInitialize_Debug() {
        var debugTextPanel = new UIElementCustom {
            Width = { Percent = 1f },
            Height = { Percent = 1f },
            IgnoresMouseInteraction = true,
        };
        var debugTextUI = new UIText(string.Empty) {
            Left = { Pixels = 20 },
            Top = { Pixels = 100 },
            TextOriginX = 0,
        };
        debugTextPanel.Append(debugTextUI);
        debugTextPanel.OnUpdate += _ => {
            StringBuilder builder = new($"Task is null: {loadTask == null}");
            if (loadTask != null) {
                builder.AppendFormat(",\nTask.IsCompleted: {0}", loadTask.IsCompleted);
                builder.AppendFormat(",\nTask.IsCompletedSuccessfully: {0}", loadTask.IsCompletedSuccessfully);
                builder.AppendFormat(",\nTask.IsFaulted: {0}", loadTask.IsFaulted);
                builder.AppendFormat(",\nTask.IsCanceled: {0}", loadTask.IsCanceled);
                builder.AppendFormat(",\nloadingState: {0}", loadingState);
            }
            debugTextUI.SetText(builder.ToString());
        };
        Append(debugTextPanel);
    }

    public override void OnActivate() {
        // 在 OnInitialize 之后执行
        ArrangeGenerate();
        Main.clrInput();
        list.Clear();
        if (!_loaded) {
            ConfigManager.LoadAll(); // Makes sure MP configs are cleared.
            Populate();
        }
    }
    public override void OnDeactivate() {
        OnDeactivate_Loading();
        OnDeactivate_Update();
        MenuNotificationsTracker.Clear();
        SetListViewPositionAfterGenerated(list.ViewPosition);
        FolderDataSystem.Save();
    }
    #endregion
    #region 注册按键
    public override void LeftMouseDown(UIMouseEvent evt) {
        LeftMouseDown_SelectAndDrag(); // 左键按下时尝试清空选中
        base.LeftMouseDown(evt);
    }
    public override void RightMouseDown(UIMouseEvent evt) {
        RightMoseDown_SelectAndDrag(); // 右键按下的选择拖拽相关逻辑
        base.RightMouseDown(evt);
    }
    public override void LeftMouseUp(UIMouseEvent evt) {
        LeftMouseUp_SelectAndDrag();
        base.LeftMouseUp(evt);
    }
    public override void RightMouseUp(UIMouseEvent evt) {
        RightMouseUp_SelectAndDrag();
        base.RightMouseUp(evt);
    }
    public override void XButton1MouseDown(UIMouseEvent evt) {
        GotoUpperFolder(); // 鼠标 4 键返回
    }
    private void MouseMove() {
        MouseMove_SelectAndDrag();
    }
    public override void ScrollWheel(UIScrollWheelEvent evt) {
        ScrollWheel_SelectAndDrag();
        base.ScrollWheel(evt);
    }

    public void LeftMouseDownOnFolderItem(UIFolderItem item) {
        LeftMouseDownOnFolderItem_SelectAndDrag(item);
    }
    public void RightMouseDownOnFolderItem(UIFolderItem item) {
        RightMouseDownOnFolderItem_SelectAndDrag(item);
    }

    #region 检测 MouseMove 的实现
    private void Update_MonitorMouseMovation() {
        if (Main.mouseX != Main.lastMouseX || Main.mouseY != Main.lastMouseY) {
            MouseMove();
        }
    }
    #endregion
    #endregion

    private void ResettleVertical() {
        float upperPixels = upperMenuContainer.Top.Pixels + upperMenuContainer.Height.Pixels;
        if (ShowRamUsage) {
            upperPixels += 2;
            ramUsage.Top.Pixels = upperPixels;
            uiPanel.ReplaceChildrenByIndex(ramUsageIndex, ramUsage);
            ramUsage.Show();
            upperPixels += ramUsage.Height.Pixels; // 20
        }
        else {
            uiPanel.ReplaceChildrenByIndex(ramUsageIndex, ramUsagePlaceHolder);
        }
        if (!ShowAllMods) {
            upperPixels += 2;
            folderPathList.Top.Pixels = upperPixels;
            refreshButton.Top.Pixels = upperPixels;
            uiPanel.ReplaceChildrenByIndex(folderPathListIndex, folderPathList);
            uiPanel.ReplaceChildrenByIndex(refreshButtonIndex, refreshButton);
            upperPixels += folderPathList.Height.Pixels; // 30
        }
        else {
            uiPanel.ReplaceChildrenByIndex(folderPathListIndex, folderPathListPlaceHolder);
            uiPanel.ReplaceChildrenByIndex(refreshButtonIndex, refreshButtonPlaceHolder);
        }
        upperPixels += 6;
        list.Top.Pixels = upperPixels;
        list.Height.Pixels = -upperPixels;
        uiScrollbar.Top.Pixels = upperPixels;
        uiScrollbar.Height.Pixels = -upperPixels;
        uiPanel.RecalculateChildren();
    }
    private void SettleBottomButtons(bool recalculate = true) {
        buttonPage = Math.Clamp(buttonPage, 0, buttonPageMax - 1);
        buttonsBg.RemoveAllChildren();
        for (int i = buttonPage * 6; i < (buttonPage + 1) * 6; ++i) {
            UIElement button;
            if (i < buttons.Length) {
                button = buttons[i];
            }
            else {
                button = buttonPlaceHolders[i % 6];
            }
            button.Width.Set(-10, 1f / 3);
            button.Height.Pixels = 40;
            button.HAlign = i % 3 * 0.5f;
            button.VAlign = i % 6 / 3;
            buttonsBg.Append(button);
        }
        if (recalculate) {
            buttonsBg.RecalculateChildren();
        }
    }

    #region 返回
    private void BackButtonClicked() {
        if (_confirmPanels.Count != 0) {
            RemoveConfirmPanel();
            return;
        }
        if (ShowFolderSystem && FolderPath.Count > 1 && !Main.keyState.PressingShift()) {
            SoundEngine.PlaySound(SoundID.MenuClose);
            GotoUpperFolder();
            return;
        }
        // 在 OnDeactivate 中有了
        // FolderDataSystem.Save();
        FolderPath.Clear();
        list.ViewPosition = 0;
        ClearSelectingItems();

        #region 强制重载
        if (_forceReloadRequired) {
            _forceReloadRequired = false;
            ModLoader.Reload();
            return;
        }

        if (Main.keyState.PressingControl()) {
            ModLoader.Reload();
            return;
        }

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
        #endregion

        ConfigManager.OnChangedAll();
        if (CommonConfig.Instance.TotallyReload) {
            Clear();
        }
        IHaveBackButtonCommand.GoBackTo(PreviousUIState);
    }
    void IHaveBackButtonCommand.HandleBackButtonUsage() {
        BackButtonClicked();
    }
    public static bool IsPreviousUIStateOfConfigList { get; set; }
    public UIState? PreviousUIState { get; set; }
    #endregion
    #region 强制重载
    private bool _forceReloadRequired;
    public bool ForceRoadRequired() => _forceReloadRequired = true;
    #endregion
    #region 启用禁用与重置模组
    // 只启用或禁用本文件夹下的模组, 按住 shift 时才是所有模组
    // 按住 alt 同时包含子文件夹
    // 按住 ctrl 在禁用时同时禁用收藏
    // 使用悬浮文字以提示这些操作
    private IEnumerable<UIModItemInFolderLoaded> GetAffectedMods(bool ignoreFavorite = false) {
        IEnumerable<UIModItemInFolderLoaded> result;
        if (Main.keyState.PressingShift()) {
            result = ModItemDict.Values;
        }
        else if (!Main.keyState.PressingAlt()) {
            if (SelectingItems.Count == 0) {
                result = VisibleItems.OfType<UIModItemInFolderLoaded>();
            }
            else {
                result = GetAffectedMods_Selected();
            }
        }
        else if (CurrentFolderNode == FolderDataSystem.Root) {
            result = ModItemDict.Values;
        }
        else {
            result = CurrentFolderNode.ModNodesInTree.Select(m => m.ModName).ToHashSet().Filter(n => ModItemDict.TryGetValue(n, out var mod) ? NewExistable(mod) : default);
        }
        if (ignoreFavorite) {
            result = result.Where(i => !i.Favorite);
        }
        return result;
    }
    private IEnumerable<UIModItemInFolderLoaded> GetAffectedMods_Selected(bool ignoreFavorite = false) {
        HashSet<string> affectedModNames = [];
        foreach (var item in SelectingItems) {
            if (item is UIModItemInFolderLoaded loaded) {
                affectedModNames.Add(loaded.ModName);
            }
            else if (item is UIFolder folder && folder.FolderNode is { } folderNode) {
                foreach (ModNode node in folderNode.ModNodesInTree) {
                    affectedModNames.Add(node.ModName);
                }
            }
        }
        return affectedModNames.Filter(n => ModItemDict.TryGetValue(n, out var mod) ? NewExistable(mod) : default);
    }
    private void EnableMods() {
        SoundEngine.PlaySound(SoundID.MenuTick);
        if (!Loaded) {
            PopupInfoByKey("UI.PopupInfos.EnableNotAllowedWhenLoading");
            return;
        }
        HashSet<string> enabled = [];
        HashSet<string> missingRefs = [];
        foreach (var modItem in GetAffectedMods()) {
            if (modItem == null || modItem.tMLUpdateRequired != null)
                continue;
            modItem.EnableQuick(enabled, missingRefs);
        }
        if (missingRefs.Count != 0) {
            Interface.infoMessage.Show(Language.GetTextValue("tModLoader.ModDependencyModsNotFound", string.Join(", ", missingRefs)), MyMenuMode);
        }
        if (enabled.Count == 0)
            return;
        if (EnabledFilterMode != FolderEnabledFilter.All) {
            ArrangeGenerate();
        }
        // Logging.tML.Info
        ModFolder.Instance.Logger.Info("Enabling mods: " + string.Join(", ", enabled));
        CurrentFolderNode.TryRefreshCountsInThisFolder();
        ModOrganizer.SaveEnabledMods();
    }
    private void DisableMods(bool disableRedundantDependencies, bool ignoreFavorite) {
        SoundEngine.PlaySound(SoundID.MenuTick);
        // TODO: 在同时取消收藏且全部取消时不用在乎是否加载完成, 可一键全部取消 (ModLoader.DisableAllMods();)
        if (!Loaded) {
            PopupInfoByKey("UI.PopupInfos.DisableNotAllowedWhenLoading");
            return;
        }
        HashSet<string> disabled = [];
        foreach (var modItem in GetAffectedMods(ignoreFavorite)) {
            modItem?.DisableQuick(disabled, disableRedundantDependencies);
        }
        if (disabled.Count == 0) {
            return;
        }
        if (EnabledFilterMode != FolderEnabledFilter.All) {
            ArrangeGenerate();
        }
        // Logging.tML.Info
        ModFolder.Instance.Logger.Info("Disabling mods: " + string.Join(", ", disabled));
        CurrentFolderNode.TryRefreshCountsInThisFolder();
        ModOrganizer.SaveEnabledMods();
    }
    private void ResetMods() {
        SoundEngine.PlaySound(SoundID.MenuTick);
        if (!Loaded) {
            PopupInfoByKey("UI.PopupInfos.ResetNotAllowedWhenLoading");
            return;
        }
        HashSet<string> enabled = [];
        HashSet<string> disabled = [];
        foreach (var modItem in GetAffectedMods()) {
            if (modItem == null || modItem.tMLUpdateRequired != null)
                continue;
            if (modItem.Loaded && ModLoader.EnabledMods.Add(modItem.ModName)) {
                enabled.Add(modItem.ModName);
            }
            if (!modItem.Loaded && ModLoader.EnabledMods.Remove(modItem.ModName)) {
                disabled.Add(modItem.ModName);
            }
        }
        if (enabled.Count != 0)
            ModFolder.Instance.Logger.Info("Enabling mods: " + string.Join(", ", enabled));
        if (disabled.Count != 0)
            ModFolder.Instance.Logger.Info("Disabling mods: " + string.Join(", ", disabled));
        if (enabled.Count != 0 || disabled.Count != 0) {
            if (EnabledFilterMode != FolderEnabledFilter.All) {
                ArrangeGenerate();
            }
            CurrentFolderNode.TryRefreshCountsInThisFolder();
            ModOrganizer.SaveEnabledMods();
        }
    }
    private void DisableRedundant(bool rightclick) {
        SoundEngine.PlaySound(SoundID.MenuTick);
        if (!Loaded) {
            PopupInfoByKey("UI.PopupInfos.DisableNotAllowedWhenLoading");
            return;
        }
        HashSet<string> disabled = [];
        HashSet<string> affectedMods = [];
        foreach (var mod in GetAffectedMods(!Main.keyState.PressingControl())) {
            affectedMods.Add(mod.ModName);
        }
        foreach (var mod in affectedMods) {
            FindUIModItem(mod)?.DisableWhenRedundant(disabled, affectedMods, rightclick);
        }
        if (disabled.Count == 0) {
            return;
        }
        if (EnabledFilterMode != FolderEnabledFilter.All) {
            ArrangeGenerate();
        }
        // Logging.tML.Info
        ModFolder.Instance.Logger.Info("Disabling mods: " + string.Join(", ", disabled));
        CurrentFolderNode.TryRefreshCountsInThisFolder();
        ModOrganizer.SaveEnabledMods();
    }
    #endregion

    public override void Update(GameTime gameTime) {
        Timer += 1;
        Update_RemoveChildrenToRemove();
        base.Update(gameTime);
        Update_MonitorMouseMovation();
        MenuNotificationsTracker.Update();
        Update_DetectConfigChange();
        Update_HandleDownloads();
        Update_HandleTask(); // 处理任务在添加或移除加载动画前
        Update_AppendOrRemoveUILoader();  // 尝试移除加载动画
        Update_DeleteMods(); // 尝试删除模组要在尝试生成之前
        Update_Generate();
    }

    #region 生成 Generate
    private void Generate() {
        list.Clear();
        list.StopMoving();
        StashSelectingItems(true);
        ClearConfirmPanels(true);
        UIFolderItemFilterResults filterResults = new();
        var visibleItems = GetVisibleItems(filterResults);
        #region 若有任何被过滤的, 则在列表中添加一个元素提示过滤了多少东西
        if (filterResults.AnyFiltered) {
            var panel = new UIPanel();
            panel.Width.Set(0, 1f);
            var filterMessages = new List<string>();
            if (filterResults.FilteredByLoaded > 0)
                filterMessages.Add(ModFolder.Instance.GetLocalizedValue("UI.FilteredByLoaded").FormatWith(filterResults.FilteredByLoaded));
            if (filterResults.FilteredByEnabled > 0)
                filterMessages.Add(Language.GetTextValue("tModLoader.ModsXModsFilteredByEnabled", filterResults.FilteredByEnabled));
            if (filterResults.FilteredByModSide > 0)
                filterMessages.Add(Language.GetTextValue("tModLoader.ModsXModsFilteredByModSide", filterResults.FilteredByModSide));
            if (filterResults.FilteredBySearch > 0)
                filterMessages.Add(Language.GetTextValue("tModLoader.ModsXModsFilteredBySearch", filterResults.FilteredBySearch));
            string filterMessage = string.Join("\n", filterMessages);
            var text = new UIText(filterMessage);
            text.Width.Set(0, 1f);
            text.IsWrapped = true;
            text.WrappedTextBottomPadding = 0;
            text.TextOriginX = 0f;
            text.Recalculate();
            panel.Append(text);
            panel.Height.Set(text.MinHeight.Pixels + panel.PaddingTop, 0f);
            list.Add(panel);
        }
        #endregion
        #region 若不在根目录, 则添加一个返回上级的文件夹
        if (ShowFolderSystem && CurrentFolderNode != FolderDataSystem.Root) {
            UIFolder upperFolder = new("..");
            list.Add(upperFolder);
            upperFolder.Selectable = false;
            upperFolder.Activate();
        }
        #endregion
        visibleItems = visibleItems.HeapSort((u1, u2) => u1.CompareTo(u2));
        list.AddRange(visibleItems);
        int i = 0;
        foreach (var item in visibleItems) {
            item.IndexCache = i++;
            item.Activate();
        }
        VisibleItems = visibleItems;
        Recalculate();
        UnstashSelectingItems();
        if (_listViewPositionToSetAfterGenerated != null) {
            list.ViewPosition = _listViewPositionToSetAfterGenerated.Value;
            _listViewPositionToSetAfterGenerated = null;
        }
    }
    private bool _generateNeeded;
    public void ArrangeGenerate() => _generateNeeded = true;
    private void Update_Generate() {
        if (_generateNeeded) {
            _generateNeeded = false;
            Generate();
        }
    }
    private FolderNode? nodeToRename;
    #region VisibleItems
    /// <summary>
    /// <br/>当前列表中的项
    /// <br/>不包含返回上一级的文件夹
    /// </summary>
    public IReadOnlyList<UIFolderItem> VisibleItems { get; private set; } = [];
    private List<UIFolderItem> GetVisibleItems(UIFolderItemFilterResults filterResults) {
        List<UIFolderItem> result;
        if (ShowAllMods) {
            result = [.. GetVisibleItems_AllMods(filterResults)];
        }
        else {
            result = [.. GetVisibleItems_InFolderSystem(filterResults)];
        }
        nodeToRename = null;
        return result;
    }
    private IEnumerable<UIFolderItem> GetVisibleItems_AllMods(UIFolderItemFilterResults filterResults) {
        HashSet<string> modsCurrent = [];
        foreach (var item in ModItemDict.Values) {
            modsCurrent.Add(item.ModName);
            if (item.PassFilters(filterResults)) {
                yield return item;
            }
        }
        foreach (var mod in FolderDataSystem.Root.ModNodesInTree) {
            if (modsCurrent.Contains(mod.ModName)) {
                continue;
            }
            modsCurrent.Add(mod.ModName);
            UIModItemInFolderUnloaded uiModUnloaded = new(mod);
            if (uiModUnloaded.PassFilters(filterResults)) {
                yield return uiModUnloaded;
            }
        }
    }
    private IEnumerable<UIFolderItem> GetVisibleItems_InFolderSystem(UIFolderItemFilterResults filterResults) {
        HashSet<string> modsCurrent = [];
        List<ModNode> nodesToRemove = [];
        foreach (var node in CurrentFolderNode.Children) {
            if (node is ModNode m) {
                if (modsCurrent.Contains(m.ModName)) {
                    nodesToRemove.Add(m);
                    continue;
                }
                modsCurrent.Add(m.ModName);
                if (ModItemDict.TryGetValue(m.ModName, out var uiMod)) {
                    m.ReceiveDataFromF(uiMod);
                    if (uiMod.PassFilters(filterResults)) {
                        uiMod.ModNode = m;
                        yield return uiMod;
                    }
                    continue;
                }
                UIModItemInFolderUnloaded uiModUnloaded = new(m);
                if (uiModUnloaded.PassFilters(filterResults)) {
                    yield return uiModUnloaded;
                }
            }
            else if (node is FolderNode f) {
                var uf = new UIFolder(f);
                if (f == nodeToRename) {
                    uf.DirectlyReplaceToRenameText();
                    uf.FolderNode?.TryRefreshCounts();
                    yield return uf;
                }
                else if (uf.PassFilters(filterResults)) {
                    uf.FolderNode?.TryRefreshCounts();
                    yield return uf;
                }
            }
        }
        if (nodesToRemove.Count != 0) {
            foreach (var nodeToRemove in nodesToRemove) {
                nodeToRemove.ParentF = null;
            }
            FolderDataSystem.DataChanged();
        }
        #region 在根目录下时将文件夹树未包含的 Mod 全部放进来
        if (CurrentFolderNode != FolderDataSystem.Root) {
            yield break;
        }
        HashSet<string> modsInFolder = [];
        foreach (var m in FolderDataSystem.Root.ModNodesInTree) {
            modsInFolder.Add(m.ModName);
        }
        bool created = false;
        foreach (var (key, value) in ModItemDict) {
            if (modsInFolder.Contains(key)) {
                continue;
            }
            created = true;
            ModNode m = new(value.TheLocalMod) {
                ParentF = FolderDataSystem.Root
            };
            if (value.PassFilters(filterResults)) {
                value.ModNode = m;
                yield return value;
            }
        }
        if (created) {
            FolderDataSystem.DataChanged();
        }
        #endregion
    }
    private float? _listViewPositionToSetAfterGenerated;
    private void SetListViewPositionAfterGenerated(float value) => _listViewPositionToSetAfterGenerated = value;
    #endregion
    #endregion
    #region Draw
    public override void Draw(SpriteBatch spriteBatch) {
        UILinkPointNavigator.Shortcuts.BackButtonCommand = 7;
        Draw_TryCloseButtons();
        Draw_UpdateDraggingTo();
        base.Draw(spriteBatch);
        MenuNotificationsTracker.Draw(spriteBatch);
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
        ShowTooltips();
    }
    #region 鼠标的悬浮提示和悬浮图片
    private readonly List<(UIElement, Func<string>)> mouseOverTooltips = [];
    /// <summary>
    /// 当鼠标在一些东西上时显示悬浮提示
    /// </summary>
    private void ShowTooltips() {
        foreach (var (ui, f) in mouseOverTooltips) {
            if (ui.IsMouseHovering) {
                UICommon.TooltipMouseText(f());
                break;
            }
        }
    }
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
    #region 拖动和选择相关
    #region 判断是否真的在拖拽
    private bool _isLeftDragging;
    private bool _isRightDragging;
    private bool movedForDragging;
    public bool IsLeftDragging {
        get => _isLeftDragging;
        private set {
            if (_isLeftDragging == value) {
                return;
            }
            if (value) {
                movedForDragging = false;
            }
            _isLeftDragging = value;
        }
    }
    public bool IsRightDragging {
        get => _isRightDragging;
        private set {
            if (_isRightDragging == value) {
                return;
            }
            if (value) {
                movedForDragging = false;
            }
            _isRightDragging = value;
        }
    }
    public bool IsReadyToDrag {
        get {
            if (!IsLeftDragging && !_isRightDragging) {
                return false;
            }
            if (!movedForDragging) {
                return false;
            }
            if (InnerDraggingTo == null) {
                return false;
            }
            if (lastDownedFolderItem != InnerDraggingTo) {
                return true;
            }
            return LastDownedTimeEnoughCheck;
        }
    }
    private bool IsInnerDragging {
        get {
            if (!IsLeftDragging && !_isRightDragging) {
                return false;
            }
            if (!movedForDragging) {
                return false;
            }
            if (_selectingItems.Count == 0) {
                return false;
            }
            return true;
        }
    }
    #endregion
    #region 拖拽目标
    UIElement? _draggingTo;
    private UIElement? InnerDraggingTo => _draggingTo;
    public UIElement? DraggingTo {
        get => IsReadyToDrag ? _draggingTo : null;
        private set => _draggingTo = value;
    }
    /// <summary>
    /// -1 为前面, 1 为后面, 0 为进入
    /// 只有当 DraggingTo 非空时有意义
    /// </summary>
    public int DraggingDirection { get; private set; }
    #endregion
    #region SelectingItems
    public IReadOnlySet<UIFolderItem> SelectingItems => _selectingItems;
    private readonly HashSet<UIFolderItem> _selectingItems = [];
    public UIFolderItem? LastSelectingItem => _lastSelectingItem;
    private UIFolderItem? _lastSelectingItem;
    public bool AddSelectingItems(UIFolderItem item) {
        if (_selectingItems.Add(item)) {
            _lastSelectingItem = item;
            return true;
        }
        return false;
    }
    public void AddOrRemoveSelectingItems(UIFolderItem item) {
        if (_selectingItems.Add(item)) {
            _lastSelectingItem = item;
            return;
        }
        _selectingItems.Remove(item);
        if (_lastSelectingItem == item) {
            _lastSelectingItem = null;
        }
    }
    public void RemoveSelectingItems(UIFolderItem item) {
        _selectingItems.Remove(item);
        if (_lastSelectingItem == item) {
            _lastSelectingItem = null;
        }
    }
    public void ClearSelectingItems() {
        _selectingItems.Clear();
        _lastSelectingItem = null;
    }

    private readonly HashSet<Node> stashSelectingItems = [];
    private Node? stashLastSelectingItem;
    private void StashSelectingItems(bool clear = false) {
        stashSelectingItems.Clear();
        foreach (var item in _selectingItems) {
            if (item.Node != null) {
                stashSelectingItems.Add(item.Node);
            }
        }
        stashLastSelectingItem = _lastSelectingItem?.Node;
        if (clear) {
            ClearSelectingItems();
        }
    }
    private void UnstashSelectingItems(bool clear = false) {
        if (clear) {
            ClearSelectingItems();
        }
        foreach (var item in VisibleItems) {
            if (item.Node != null && stashSelectingItems.Contains(item.Node)) {
                _selectingItems.Add(item);
            }
            if (stashLastSelectingItem != null && item.Node == stashLastSelectingItem) {
                _lastSelectingItem = item;
            }
        }
        stashSelectingItems.Clear();
        stashLastSelectingItem = null;
    }

    private UIFolderItem? FindClosest(UIFolderItem item) {
        var selecting = SelectingItems;
        int selectingCount = selecting.Count;
        // 若为空, 则返回空
        if (selectingCount == 0) {
            return null;
        }
        // 若只有一个, 则返回这个
        if (selectingCount == 1) {
            return selecting.First();
        }
        // 若包含在选择中, 则返回自己
        if (SelectingItems.Contains(item)) {
            return item;
        }
        var index = item.IndexCache;
        // 若选择数量不多, 则遍历所有选择, 寻找距离最小的返回
        if (selectingCount <= 5) {
            using var e = selecting.GetEnumerator();
            if (!e.MoveNext()) {
                return null; // 理应不应出现
            }
            var closest = e.Current;
            int minDistance = Math.Abs(closest.IndexCache - index);
            while (e.MoveNext()) {
                int distance = Math.Abs(e.Current.IndexCache - index);
                if (distance > minDistance) {
                    continue;
                }
                // 若距离更小, 或距离相等但在前边则更新 (前边优先于后边)
                if (distance < minDistance || e.Current.IndexCache < index) {
                    closest = e.Current;
                    minDistance = distance;
                }
            }
            return closest;
        }
        var visibles = VisibleItems;
        var visiblesCount = visibles.Count;
        // 向两侧查找
        foreach (var i in SpreadForeach(visiblesCount, index, 1)) {
            var v = visibles[i];
            if (selecting.Contains(v)) {
                return v;
            }
        }
        return null; // 不该出现
    }
    private static IEnumerable<int> SpreadForeach(int length, int index, int spread) {
        if (index - spread < 0) {
            for (index = (index + spread).WithMin(0); index < length; ++index) {
                yield return index;
            }
            yield break;
        }
        if (index + spread >= length) {
            for (index = (index - spread).WithMax(length - 1); index >= 0; --index) {
                yield return index;
            }
            yield break;
        }
        for (; ; ++spread) {
            if (index < spread) {
                for (index += spread; index < length; ++index) {
                    yield return index;
                }
                yield break;
            }
            yield return index - spread;
            if (index + spread >= length) {
                for (index -= spread + 1; index >= 0; --index) {
                    yield return index;
                }
                yield break;
            }
            yield return index + spread;
        }
    }
    #endregion

    private UIFolderItem? _draggingTarget;
    [Obsolete("Use Selecting Items Instead")]
    public UIFolderItem? DraggingTarget {
        get => ShowFolderSystem ? _draggingTarget : null;
        set {
            if (ShowFolderSystem || value == null) {
                _draggingTarget = value;
            }
        }
    }
    // 在 Draw 之前执行
    private void Draw_UpdateDraggingTo() {
        if (!IsInnerDragging) {
            return;
        }
        #region 根据现在的排序和过滤方式判断是否可以自定义排序
        bool canCustomizeOrder = CanCustomizeOrder();
        #endregion
        #region 如果在文件夹路径的 UI 中, 尝试判断是否拖到了某个非本文件夹的 PathItem 中
        int folderPathListCountM1 = folderPathList.Count - 1;
        // TODO: 算法优化
        for (int i = 0; i < folderPathListCountM1; ++i) {
            var item = folderPathList.Items[i];
            if (item.IsMouseHovering) {
                DraggingTo = item;
                DraggingDirection = 0;
                return;
            }
        }
        #endregion
        #region 搜索目标元素
        // TODO: 算法优化
        UIFolderItem? aim = null;
        foreach (var listItem in list._items) {
            if (listItem is not UIFolderItem fi) {
                continue;
            }
            aim = fi;
            if (listItem._dimensions.Y + listItem._dimensions.Height >= Main.mouseY) {
                break;
            }
        }
        if (aim == null) {
            DraggingTo = null;
            return;
        }
        #endregion
        #region 设置拖动目的地
        // 若是文件夹...
        if (aim is UIFolder folder) {
            // 当不能自定义顺序时只有当鼠标在上面时才能拖入, 否则不能移动
            if (!canCustomizeOrder) {
                if (folder.IsMouseHovering) {
                    DraggingTo = folder;
                    DraggingDirection = 0;
                    return;
                }
                DraggingTo = null;
                return;
            }
            // 当可以自定义顺序时判断移到上面还是下面; 当鼠标在上面且较中间的位置时才能拖入
            DraggingTo = folder;
            DraggingDirection = DierctionUpDownOrInner(aim);
            return;
        }
        // 若非文件夹...
        // 不能自定义排序的话直接不能移动
        if (!canCustomizeOrder) {
            DraggingTo = null;
            return;
        }
        // 可以的话则移上或者移下
        DraggingTo = aim;
        DraggingDirection = DirectionUpOrDown(aim);
        #endregion
        #region 辅助方法
        // 在一半以上则移上, 否则移下
        static int DirectionUpOrDown(UIFolderItem aim) {
            if (Main.mouseY < aim._dimensions.Y + aim._dimensions.Height / 2) {
                return -1;
            }
            return 1;
        }
        // 鼠标不在上面时同上面那个函数, 否则 1 / 8 上则移上, 7 / 8 下则移下, 否则移入
        static int DierctionUpDownOrInner(UIFolderItem aim) {
            if (!aim.IsMouseHovering) {
                return DirectionUpOrDown(aim);
            }
            if (Main.mouseY < aim._dimensions.Y + aim._dimensions.Height / 8) {
                return -1;
            }
            if (Main.mouseY > aim._dimensions.Y + aim._dimensions.Height * 7 / 8) {
                return 1;
            }
            return 0;
        }
        #endregion
    }
    #region 刚刚按下的项和按下的时间
    private UIFolderItem? lastDownedFolderItem;
    private double lastDownedOnFolderItemTime;
    private static double TimeNowForDrag => Main.gameTimeCache.TotalGameTime.TotalMicroseconds;
    private static readonly double TimeToleranceForDrag = 500;
    private bool LastDownedTimeEnoughCheck => TimeNowForDrag - lastDownedOnFolderItemTime > TimeToleranceForDrag;
    #endregion

    private enum WaitingForSelectTypes {
        NotWaiting,
        LeftNormal,
        LeftCtrl,
    }
    private WaitingForSelectTypes waitingForSelectType;
    public void ClearWaitingForSelect() => waitingForSelectType = WaitingForSelectTypes.NotWaiting;
    public void LeftMouseDownOnFolderItem_SelectAndDrag(UIFolderItem item) {
        if (!item.Selectable) {
            return;
        }
        if (ShowAllMods) {
            return;
        }
        lastDownedFolderItem = item;
        lastDownedOnFolderItemTime = TimeNowForDrag;
        suppressLeftMouseDownClearSelecting = true;
        IsLeftDragging = true;
        var selecting = SelectingItems;
        bool shift = Main.keyState.PressingShift();
        bool ctrl = Main.keyState.PressingControl();
        switch ((shift, ctrl)) {
        case (false, false):
            if (selecting.Contains(item)) {
                waitingForSelectType = WaitingForSelectTypes.LeftNormal;
                break;
            }
            // 什么都不按, 清空所选, 转为选择这一项
            ClearSelectingItems();
            AddSelectingItems(item);
            break;
        case (false, true):
            // TODO: 此情况下不能直接清空. 需要等待松开或移动
            // 按住 Ctrl 反选
            waitingForSelectType = WaitingForSelectTypes.LeftCtrl;
            break;
        case (true, false): { // only shift
            // 按住 Shift 直接框选
            var from = LastSelectingItem ?? FindClosest(item);
            if (from == null) { // 此时应该没有选项, 直接添加此项
                AddSelectingItems(item);
                break;
            }
            int indexFrom = from.IndexCache, indexTo = item.IndexCache;
            var visibles = VisibleItems;
            _lastSelectingItem = visibles[indexTo];
            if (indexFrom > indexTo) {
                (indexFrom, indexTo) = (indexTo, indexFrom);
            }
            indexTo.ClampMaxTo(visibles.Count - 1);
            for (int i = indexFrom; i <= indexTo; ++i) {
                _selectingItems.Add(visibles[i]);
            }
            break;
        }
        case (true, true): {
            // 按住 Ctrl 和 Shift, 框选反选
            var from = LastSelectingItem ?? FindClosest(item);
            if (from == null) { // 此时应该没有选项, 直接添加此项
                AddSelectingItems(item);
                break;
            }
            int indexFrom = from.IndexCache, indexTo = item.IndexCache;
            var visibles = VisibleItems;
            _lastSelectingItem = SelectingItems.Contains(visibles[indexTo]) ? null : visibles[indexTo];
            if (indexFrom <= indexTo) {
                indexFrom += 1;
            }
            else {
                indexFrom -= 1;
                (indexFrom, indexTo) = (indexTo, indexFrom);
            }
            indexTo.ClampMaxTo(visibles.Count - 1);
            for (int i = indexFrom; i <= indexTo; ++i) {
                _selectingItems.AddOrRemove(visibles[i]);
            }
            break;
        }
        }
    }
    public void RightMouseDownOnFolderItem_SelectAndDrag(UIFolderItem item) {
        if (!item.Selectable) {
            return;
        }
        if (ShowAllMods) {
            return;
        }
        lastDownedFolderItem = item;
        lastDownedOnFolderItemTime = TimeNowForDrag;
        suppressRightMouseDownClearSelecting = true;
        // IsRightDragging = true;
    }

    public bool RealDraggingTo(UIFolderItem item) {
        if (item == lastDownedFolderItem) {
            if (!LastDownedTimeEnoughCheck) {
                return false;
            }
        }
        return true;
    }

    private bool suppressLeftMouseDownClearSelecting;
    private bool suppressRightMouseDownClearSelecting;
    private void LeftMouseDown_SelectAndDrag() {
        IsRightDragging = false;
        if (suppressLeftMouseDownClearSelecting) {
            suppressLeftMouseDownClearSelecting = false;
            return;
        }
        ClearSelectingItems();
    }
    private void RightMoseDown_SelectAndDrag() {
        IsLeftDragging = false;
        if (suppressRightMouseDownClearSelecting) {
            suppressRightMouseDownClearSelecting = false;
            return;
        }
        ClearSelectingItems();
    }
    private void LeftMouseUp_SelectAndDrag() {
        if (!IsLeftDragging) {
            return;
        }
        var draggingTo = DraggingTo;
        IsLeftDragging = false;
        var selecting = SelectingItems;
        bool readyToDrag = draggingTo != null;
        LeftMouseUpForSelecting(readyToDrag);
        ClearWaitingForSelect();
        if (!readyToDrag || selecting.Count == 0) {
            return;
        }
        #region 将要拖动的元素转为 Node
        List<Node> nodes = selecting.OrderBy(i => i.IndexCache).Filter(i => i.Node != null ? NewExistable(i.Node) : default).ToList();
        if (nodes.Count == 0) {
            return;
        }
        #endregion
        Node? aimNode = null;
        if (draggingTo is UIFolderItem fi) {
            aimNode = fi.Node;
        }
        else if (draggingTo is UIFolderPathItem pi) {
            aimNode = pi.FolderNode;
        }
        if (aimNode == null) {
            if (draggingTo is UIFolder folder && folder.Name == "..") {
                if (DraggingDirection == 0) {
                    // 保险起见判断一下, 理应总是符合该条件的
                    if (FolderPath.Count > 1)
                        MoveNodesIntoFolder(nodes, FolderPath[^2]);
                }
                else {
                    MoveNodesToTheStart(nodes);
                }
            }
            return;
        }
        if (DraggingDirection < 0) {
            MoveNodesBeforeNode(nodes, aimNode);
        }
        else if (DraggingDirection > 0) {
            MoveNodesAfterNode(nodes, aimNode);
        }
        else {
            if (aimNode is FolderNode aimFolder) {
                MoveNodesIntoFolder(nodes, aimFolder);
            }
        }
        #region 辅助方法
        void MoveNodesIntoFolder(List<Node> nodes, FolderNode folder) {
            int length = nodes.Count;
            var ctrl = Main.keyState.PressingControl();
            for (int i = 0; i < length; ++i) {
                var node = nodes[i];
                if (node == folder) {
                    continue;
                }
                if (node.Parent != CurrentFolderNode) {
                    ModFolder.Instance.Logger.Error("node's parent should be the current folder at MoveNodeIntoFolder");
                    // return;
                }
                if (ctrl && node is ModNode mn) {
                    node = new ModNode(mn);
                }
                else {
                    node.ParentF = null;
                }

                if (node is ModNode modNode) {
                    foreach (var child in folder.Children) {
                        if (child is ModNode m && m.ModName == modNode.ModName) {
                            continue;
                        }
                    }
                }
                node.ParentF = folder;
            }
            FolderDataSystem.TreeChanged();
        }
        void MoveNodesBeforeNode(List<Node> nodes, Node target) {
            FolderDataSystem.MoveNodesAroundNode(nodes, target);
        }
        void MoveNodesAfterNode(List<Node> nodes, Node target) {
            FolderDataSystem.MoveNodesAroundNode(nodes, target, true);
        }
        void MoveNodesToTheStart(List<Node> nodes) {
            FolderDataSystem.MoveNodesToTop(nodes);
        }
        #endregion
    }
    private void RightMouseUp_SelectAndDrag() {
        if (!IsRightDragging) {
            return;
        }
        IsRightDragging = false;
        // TODO
    }
    private void MouseMove_SelectAndDrag() {
        MoveForDraggingDetected();
    }
    private void ScrollWheel_SelectAndDrag() {
        MoveForDraggingDetected();
    }
    private void MoveForDraggingDetected() {
        movedForDragging = true;
        if (waitingForSelectType == WaitingForSelectTypes.NotWaiting) {
            return;
        }
        if (lastDownedFolderItem == null) {
            return;
        }
        if (!IsReadyToDrag) {
            return;
        }
        switch (waitingForSelectType) {
        case WaitingForSelectTypes.LeftNormal:
            ClearWaitingForSelect();
            break;
        case WaitingForSelectTypes.LeftCtrl:
            ClearWaitingForSelect();
            AddSelectingItems(lastDownedFolderItem);
            break;
        }
    }
    private void LeftMouseUpForSelecting(bool readyToDrag) {
        if (waitingForSelectType == WaitingForSelectTypes.NotWaiting) {
            return;
        }
        if (lastDownedFolderItem == null) {
            return;
        }
        switch (waitingForSelectType) {
        case WaitingForSelectTypes.LeftNormal:
            // 如果此项没有入选不会等待

            // 如果是左键单击以拖动则保持选择
            if (readyToDrag) {
                break;
            }
            // 如果不拖动则单选
            ClearSelectingItems();
            AddSelectingItems(lastDownedFolderItem);
            break;
        case WaitingForSelectTypes.LeftCtrl:
            // 如果要拖动则同时入选
            if (readyToDrag) {
                AddSelectingItems(lastDownedFolderItem);
                break;
            }
            // 否则反选
            AddOrRemoveSelectingItems(lastDownedFolderItem);
            break;
        }
    }
    #endregion
    
    #region 加载相关 (包含删除模组)
    #region 加载动画
    private UILoaderAnimatedImage uiLoader = null!;
    private bool _uiLoaderAppended;
    private bool _needToRemoveLoading;
    private bool _needToAppendLoading;
    private void ArrangeRemoveLoading() => _needToRemoveLoading = true;
    private void ArrangeAppendLoading() => _needToAppendLoading = true;
    private void OnInitialize_Loading() {
        uiLoader = new(1, 1);
        uiLoader.Left.Pixels = -10;
        uiLoader.Top.Pixels = -10;
        uiLoader.Activate();
    }
    private void Update_AppendOrRemoveUILoader() {
        if (_needToAppendLoading) {
            _needToAppendLoading = false;
            if (!_uiLoaderAppended) {
                Append(uiLoader);
                _uiLoaderAppended = true;
            }
        }
        if (_needToRemoveLoading) {
            _needToRemoveLoading = false;
            if (_uiLoaderAppended) {
                RemoveChild(uiLoader);
                _uiLoaderAppended = false;
            }
        }
    }
    #endregion
    private void Refresh(UIMouseEvent mouse, UIElement element) {
        SoundEngine.PlaySound(SoundID.MenuTick);
        if (Loading) {
            return;
        }
        Populate();
    }
    private Task? loadTask;
    private CancellationTokenSource? _cts;
    private bool _loaded;
    public bool Loaded => _loaded;
    public bool Loading => loadTask != null;
    [Conditional("DEBUG")]
    private void SetLoadingState(string state) => loadingState = state;
    private string loadingState = string.Empty;
    public void Populate() {
        TryClearCts();
        _cts = new();
        loadTask = FindModsTask(_cts.Token);
    }
    private bool needRepopulate;
    public void ArrrangeRepopulate() => needRepopulate = true;
    private void Update_HandleTask() {
        if (loadTask == null) {
            if (needRepopulate) {
                needRepopulate = false;
                Populate();
                return;
            }
            return;
        }
        if (!loadTask.IsCompleted) {
            return;
        }
        if (needRepopulate) {
            needRepopulate = false;
            Populate();
            return;
        }
        _loaded = true;
        loadTask = null;
        _cts = null;
        ArrangeRemoveLoading();
        ArrangeGenerate();
    }
    private void OnDeactivate_Loading() {
        TryClearCts();
        loadTask = null;
    }
    private void TryClearCts() {
        if (_cts == null) {
            return;
        }
        _cts.Cancel(false);
        _cts.Dispose();
        _cts = null;
    }
    #region 异步寻找 Mod
    private async Task FindModsTask(CancellationToken token) {
        _loaded = false;
        ArrangeAppendLoading();
        SetLoadingState("Start");
        await Task.Yield();
        if (_modsToDelete.Count != 0) {
            SetLoadingState("Deleting Mod");
            while (_modsToDelete.Count != 0) {
                var modToDelete = _modsToDelete.First();
                _modsToDelete.Remove(modToDelete);
                ModOrganizer.DeleteMod(modToDelete);
                await Task.Yield();
            }
        }
        SetLoadingState("Ready To Find Mods");
        // 删除模组的操作不能取消, 所以从这里开始才使用 token
        await Task.Delay(0, token);
        var mods = ModOrganizer.FindMods(CommonConfig.Instance.LogModLoading);
        SetLoadingState("Loading Mods");
        await Task.Delay(0, token);
        Dictionary<string, UIModItemInFolderLoaded> tempModItemDict = [];
        foreach (var mod in mods) {
            UIModItemInFolderLoaded modItem = new(mod);
            tempModItemDict.Add(modItem.ModName, modItem);
            modItem.Activate();
            await Task.Delay(0, token);
        }
        // 以防在加载过程中安排了删除模组但实际上还没删除的情况, 从中排除待删除的模组
        lock (_modsToDelete) {
            foreach (var modToDelete in _modsToDelete) {
                tempModItemDict.Remove(modToDelete.Name);
            }
        }
        ModItemDict = tempModItemDict;
        SetLoadingState("Final Clean");
        ArrangeGenerate();
        await Task.Delay(0, token);
        var availableMods = ModOrganizer.RecheckVersionsToLoad().ToArray();
        // TODO: 算法优化 (虽然不是很有必要)
        foreach (var modItem in ModItemDict.Values) {
            modItem.SetModReferences(availableMods);
        }

        if (CommonConfig.Instance.RemoveRedundantData) {
            FolderDataSystem.RemoveRedundantData();
        }
        #region 遍历一遍 Root 来做各种事情
        bool modified = false;
        foreach (var mod in FolderDataSystem.Root.ModNodesInTree) {
            if (ModItemDict.TryGetValue(mod.ModName, out var uiMod)) {
                modified = true;
                mod.ReceiveDataFromF(uiMod);
            }
        }
        if (modified) {
            FolderDataSystem.DataChanged();
        }
        FolderDataSystem.UpdateLastModified();
        // TODO: 更多...
        #endregion
        ArrangeGenerate();
    }
    #endregion
    #endregion

    #region ArrangeDeleteMod
    private readonly HashSet<LocalMod> _modsToDelete = [];
    public void ArrangeDeleteMod(UIModItemInFolderLoaded uiMod) {
        _modsToDelete.Add(uiMod.TheLocalMod);
        ModItemDict.Remove(uiMod.ModName);
        ArrangeGenerate();
    }
    private void Update_DeleteMods() {
        if (_modsToDelete.Count != 0 && !Loading) {
            Populate();
        }
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
    #region 订阅模组
    public void AddDownload(string key, DownloadProgressImpl progress) {
        _downloads.Add(key, progress);
        downloadProgressQueue.Enqueue(progress);
    }
    public Dictionary<string, DownloadProgressImpl> Downloads => _downloads;
    private readonly Dictionary<string, DownloadProgressImpl> _downloads = [];
    private readonly Queue<DownloadProgressImpl> downloadProgressQueue = [];
    private void Update_HandleDownloads() {
        if (!downloadProgressQueue.TryPeek(out var progress)) {
            return;
        }
        progress.TryStart();
        if (progress.Completed) {
            _downloads.Remove(progress.ModDownloadItem.ModName);
            downloadProgressQueue.Dequeue();
        }
    }
    #endregion
    #region 更新模组
    private Task? updateTask;
    private CancellationTokenSource? updateCts;
    [MemberNotNull(nameof(updateCts))]
    private void RefreshUpdateCts() {
        if (updateCts != null) {
            updateCts.Cancel();
            updateCts.Dispose();
        }
        updateCts = new();
    }
    private void ClearUpdateCts() {
        if (updateCts == null) {
            return;
        }
        updateCts.Cancel();
        updateCts.Dispose();
        updateCts = null;
    }
    private void ButtonUpdateClicked() {
        SoundEngine.PlaySound(SoundID.MenuTick);
        if (!SteamedWraps.SteamAvailable) {
            PopupInfoByKey("UI.PopupInfos.SteamNotAvailable");
            return;
        }
        ModDownloadItem[]? modsToUpdate = null;
        #region 弹出提示窗
        #region 窗口
        UIPanel updatePanel = new() {
            Width = { Pixels = 500 },
            Height = { Pixels = 500 },
            HAlign = .5f,
            VAlign = .7f,
            BackgroundColor = new Color(63, 82, 151),
            BorderColor = Color.Black
        };
        updatePanel.SetPadding(6);
        AppendConfirmPanel(updatePanel, ClearUpdateCts);
        #endregion
        #region 文本
        var dialogText = new UIText(ModFolder.Instance.GetLocalization("UI.Buttons.Update.SearchingForMods")) {
            Width = { Percent = .85f },
            HAlign = .5f,
            Top = new(10, 0),
            IsWrapped = true,
        };
        updatePanel.Append(dialogText);
        #endregion
        #region 列表
        UpdateListItem[]? updateListItems = null;
        bool updateListItemsAdded = false;
        UIList updateModsList = new() {
            Left = new(0, 0.05f),
            Width = new(-20, 0.9f),
            Top = new(50, 0),
            Height = new(-120, 1),
        };
        UIScrollbar scrollbar = new() {
            Width = new(20, 0),
            HAlign = 1,
            Top = new(50, 0),
            Height = new(-120, 1),
        };
        updateModsList.SetScrollbar(scrollbar);
        updateModsList.OnUpdate += _ => {
            if (updateListItems != null && !updateListItemsAdded) {
                updateListItemsAdded = true;
                updateModsList.AddRange(updateListItems);
            }
        };
        updatePanel.Append(updateModsList);
        updatePanel.Append(scrollbar);
        #endregion
        #region 按钮是
        var yesButton = new UIAutoScaleTextTextPanel<string>(Language.GetTextValue("LegacyMenu.104")) {
            TextColor = Color.Gray,
            Width = new(-10f, 1f / 3),
            HAlign = .15f,
            Height = { Pixels = 40 },
            Top = new(-60, 1),
            IgnoresMouseInteraction = true,
        }.WithFadedMouseOver();
        yesButton.OnLeftClick += (_, _) => {
            if (updateTask != null || modsToUpdate == null) {
                return;
            }
            if (modsToUpdate.Length <= 0) {
                RemoveConfirmPanel();
                return;
            }
            var realUpdateListItems = updateListItems?.Where(i => i.Selecting).ToArray();
            if (realUpdateListItems == null || realUpdateListItems.Length == 0) {
                Task.Run(() => DownloadHelper.DownloadMods([.. modsToUpdate]));
            }
            else {
                Task.Run(() => DownloadHelper.DownloadMods([.. realUpdateListItems.Select(i => i.Mod)]));
            }
            RemoveConfirmPanel();
        };
        updatePanel.Append(yesButton);
        #endregion
        #region 按钮否
        var noButton = new UIAutoScaleTextTextPanel<string>(Language.GetTextValue("LegacyMenu.105")) {
            Width = new(-10f, 1f / 3),
            HAlign = .85f,
            Height = { Pixels = 40 },
            Top = new(-60, 1),
        }.WithFadedMouseOver();
        noButton.OnLeftClick += (_, _) => RemoveConfirmPanel();
        updatePanel.Append(noButton);
        #endregion
        updatePanel.Recalculate();
        #endregion
        RefreshUpdateCts();
        var token = updateCts.Token;
        updateTask = Task.Run(async () => {
            modsToUpdate = DownloadHelper.GetFullDownloadList([.. Interface.modBrowser.SocialBackend.GetInstalledModDownloadItems().Where(item => item.NeedUpdate)]);
            updateListItems = modsToUpdate.Select(ToUpdateListItem).ToArray();
            foreach (var item in updateListItems) {
                item.OnSelected += () => {
                    foreach (var i in updateListItems) {
                        if (item.ConnectedMods.Contains(i.Mod)) {
                            i.SetSelectingQuick(true);
                        }
                    }
                };
                item.OnDeselected += () => {
                    foreach (var i in updateListItems) {
                        if (i.ConnectedMods.Contains(item.Mod)) {
                            i.SetSelectingQuick(false);
                        }
                    }
                };
            }
            while (!updateListItemsAdded) {
                await Task.Yield();
                if (token.IsCancellationRequested) {
                    updateTask = null;
                    return;
                }
            }
            Thread.MemoryBarrier();
            if (updateListItems.Length > 0) {
                dialogText.SetText(ModFolder.Instance.GetLocalization("UI.Buttons.Update.DialogText"));
                yesButton.TextColor = Color.White;
                yesButton.IgnoresMouseInteraction = false;
            }
            else {
                dialogText.SetText(ModFolder.Instance.GetLocalization("UI.Buttons.Update.NoModsToUpdate"));
            }
            updateTask = null;
        });
    }

    private class UpdateListItem : UIPanel {
        private bool _selecting;
        public bool Selecting {
            get => _selecting;
            set {
                if (_selecting == value) {
                    return;
                }
                _selecting = value;
                if (value) {
                    Text.TextColor = new(255, 231, 69);
                    BorderColor = IsMouseHovering ? BorderMouseOverAndSelecting : BorderSelecting;
                    OnSelected?.Invoke();
                }
                else {
                    Text.TextColor = Color.White;
                    BorderColor = IsMouseHovering ? BorderMouseOver : BorderDefault;
                    OnDeselected?.Invoke();
                }
            }
        }
        public void SetSelectingQuick(bool value) {
            if (_selecting == value) {
                return;
            }
            _selecting = value;
            if (value) {
                Text.TextColor = new(255, 231, 69);
                BorderColor = IsMouseHovering ? BorderMouseOverAndSelecting : BorderSelecting;
            }
            else {
                Text.TextColor = Color.White;
                BorderColor = IsMouseHovering ? BorderMouseOver : BorderDefault;
            }
        }
        public ModDownloadItem Mod { get; }
        public HashSet<ModDownloadItem> ConnectedMods { get; }
        public UIText Text { get; }
        public event Action? OnSelected;
        public event Action? OnDeselected;

        public static Color BorderDefault => UICommon.DefaultUIBorder;
        public static Color BorderMouseOver => new(255, 231, 69);
        public static Color BorderSelecting => new(191, 173, 69);
        public static Color BorderMouseOverAndSelecting => new(255, 231, 69);
        public UpdateListItem(ModDownloadItem mod) {
            Mod = mod;
            ConnectedMods = [.. DownloadHelper.GetFullDownloadEnumerable([mod])];
            ConnectedMods.Remove(mod);
            SetPadding(4);
            PaddingTop = PaddingBottom = 8;
            string str = mod.Installed != null
                ? ModFolder.Instance.GetLocalizedValue("UI.Buttons.Update.UpdateItemPattern").FormatWith(mod.ModName, mod.Installed.Version, mod.Version)
                : ModFolder.Instance.GetLocalizedValue("UI.Buttons.Update.NewItemPattern").FormatWith(mod.ModName, mod.Version);
            
            Text = new(str) {
                Width = new(-4, 1),
                Height = new(10, 0),
                TextOriginY = 0.5f,
                IsWrapped = true,
                WrappedTextBottomPadding = -12,
            };
            Append(Text);
        }
        public override void Recalculate() {
            base.Recalculate();
            Height.Pixels = (Text.MinHeight.Pixels + PaddingTop + PaddingBottom).WithMin(30);
            Text.Height.Pixels = Height.Pixels - PaddingTop - PaddingBottom;
            this.RecalculateSelf();
        }
        public override void MouseOver(UIMouseEvent evt) {
            base.MouseOver(evt);
            BorderColor = Selecting ? BorderMouseOverAndSelecting : BorderMouseOver;
        }
        public override void MouseOut(UIMouseEvent evt) {
            base.MouseOut(evt);
            BorderColor = Selecting ? BorderSelecting : BorderDefault;
        }
        public override void LeftClick(UIMouseEvent evt) {
            base.LeftClick(evt);
            Selecting = !Selecting;
        }
    }

    private static UpdateListItem ToUpdateListItem(ModDownloadItem mod) {
        return new(mod) {
            Width = new(0, 1),
            // panel 的 Height 在 Recalculate 时计算
        };
    }
    private void OnDeactivate_Update() {
        updateTask = null;
        ClearUpdateCts();
    }
    #endregion
    #region PopupInfo
    public static void PopupInfo(string message) {
        MenuNotificationsTracker.AddNotification(new TextNotification(message, new(0, 0.25f)));
    }
    public static void PopupInfoByKey(string keyFromMod) {
        PopupInfo(ModFolder.Instance.GetLocalizedValue(keyFromMod));
    }
    public static void PopupInfoByKey(string keyFromMod, params object?[] args) {
        PopupInfo(ModFolder.Instance.GetLocalizedValue(keyFromMod).FormatWith(args));
    }
    #endregion
}
