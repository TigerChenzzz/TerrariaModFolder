using Microsoft.CodeAnalysis;
using Microsoft.Xna.Framework.Input;
using ModFolder.Configs;
using ModFolder.Systems;
using ReLogic.Content;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Terraria.Audio;
using Terraria.GameContent.UI.Elements;
using Terraria.GameContent.UI.States;
using Terraria.ModLoader.Config;
using Terraria.ModLoader.Core;
using Terraria.ModLoader.UI;
using Terraria.ModLoader.UI.ModBrowser;
using Terraria.UI;
using Terraria.UI.Gamepad;
using FolderNode = ModFolder.Systems.FolderDataSystem.FolderNode;
using ModNode = ModFolder.Systems.FolderDataSystem.ModNode;
using Node = ModFolder.Systems.FolderDataSystem.Node;

namespace ModFolder.UI;

// TODO: Esc 返回时同样尝试回到上一级目录
// TODO: 在进入时即刻生成 UI
public class UIModFolderMenu : UIState, IHaveBackButtonCommand {
    public static UIModFolderMenu Instance { get; private set; } = new();
    public int Timer { get; private set; }
    public static void TotallyReload() {
        Instance = new();
    }
    public static void EnterFrom(UIWorkshopHub hub) {
        SoundEngine.PlaySound(SoundID.MenuOpen);
#if DEBUG
        // TODO: 做成选项
        TotallyReload();
        // TODO: 做成选项
        FolderDataSystem.Reload();
#endif
        Instance.PreviousUIState = hub;
        Main.MenuUI.SetState(Instance); // 如果没有初始化的话会在这里初始化
        Instance.ResetCategoryButtons();
        Instance.SetListViewPositionAfterGenerated(0);
    }
    public const int MyMenuMode = 47133;

    #region 拖动相关
    UIElement? _draggingTo;
    public UIElement? DraggingTo {
        get => DraggingTarget == null ? null : _draggingTo;
        private set => _draggingTo = value;
    }
    /// <summary>
    /// -1 为上面, 1 为下面, 0 为进入
    /// 只有当 DraggingTo 非空时有意义
    /// </summary>
    public int DraggingDirection { get; private set; }
    #endregion

    #region 所有模组 ModItems
    /// <summary>
    /// 当找完模组后, 这里会存有所有的模组
    /// </summary>
    public Dictionary<string, UIModItemInFolder> ModItemDict { get; set; } = [];
    #endregion

    #region 文件夹路径
    /// <summary>
    /// 当前处于哪个文件夹下, 若为空则代表处于根目录下
    /// </summary>
    public FolderNode CurrentFolderNode => FolderPath[^1];
    public UIHorizontalList folderPathList = null!;
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
        FolderPath.RemoveAt(FolderPath.Count - 1);
        ArrangeGenerate();
    }
    public void GotoUpperFolder(FolderNode folder) {
        for (int i = 0; i < FolderPath.Count - 1; ++i) {
            if (FolderPath[i] != folder) {
                continue;
            }
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
    private readonly UIAutoScaleTextTextPanelWithFadedMouseOver<LocalizedText>[] buttons = new UIAutoScaleTextTextPanelWithFadedMouseOver<LocalizedText>[8];
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
    public FolderEnabledFilter EnabledFilterMode { get => (FolderEnabledFilter)_topButtonData[3]; set => _topButtonData[3] = (int)value; }
    public ModSideFilter       ModSideFilterMode { get => (ModSideFilter      )_topButtonData[4]; set => _topButtonData[4] = (int)value; }
    public bool                ShowRamUsage      { get => _topButtonData[5].ToBoolean(); set => _topButtonData[5] = value.ToInt(); }
    #endregion
    #region 数据与常数
    private readonly int[] _topButtonData = new int[6];
    private readonly int[] _topButtonLengths = [2, 3, 5, 8, 5, 2];
    private readonly Point[] _topButtonPositionsInTexture = [
        new(2, 6),
        new(0, 5),
        new(0, 0),
        new(1, 0),
        new(2, 0),
        new(3, 2),
    ];
    private readonly string[][] _topButtonLocalizedKeys = [
        [
            "Mods.ModFolder.UI.SortButtons.FolderSystem.Tooltip",
            "Mods.ModFolder.UI.SortButtons.AllMods.Tooltip",
        ],
        [
            "Mods.ModFolder.UI.SortButtons.CustomFM.Tooltip",
            "Mods.ModFolder.UI.SortButtons.FolderFirst.Tooltip",
            "Mods.ModFolder.UI.SortButtons.ModFirst.Tooltip",
        ],
        [
            "Mods.ModFolder.UI.SortButtons.Custom.Tooltip",
            "tModLoader.ModsSortRecently",
            "Mods.ModFolder.UI.SortButtons.ReverseRecently.Tooltip",
            "tModLoader.ModsSortNamesAlph",
            "tModLoader.ModsSortNamesReverseAlph",
        ],
        [
            "tModLoader.ModsShowAllMods",
            "tModLoader.ModsShowEnabledMods",
            "tModLoader.ModsShowDisabledMods",
            "Mods.ModFolder.UI.SortButtons.ToBeEnabled.Tooltip",
            "Mods.ModFolder.UI.SortButtons.ToBeDisabled.Tooltip",
            "Mods.ModFolder.UI.SortButtons.ToBeToggled.Tooltip",
            "Mods.ModFolder.UI.SortButtons.WouldBeEnabled.Tooltip",
            "Mods.ModFolder.UI.SortButtons.WouldBeDisabled.Tooltip",
        ],
        [
            "tModLoader.MBShowMSAll",
            "tModLoader.MBShowMSBoth",
            "tModLoader.MBShowMSClient",
            "tModLoader.MBShowMSServer",
            "tModLoader.MBShowMSNoSync",
        ],
        [
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
        var texture = Textures.UI("SortIcons");
        OnInitialize_SortButtons(texture);
        OnInitialize_SearchFilter(texture);
    }
    #endregion
    #region 内存占用
    private readonly UIMemoryBar ramUsage = new();
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
            _topButtonData[i] = 0;
            categoryButtons[i].SetCurrentState(0);
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
    private readonly UICycleImage[] topMenuButtons = new UICycleImage[6];
    private readonly int categoryButtonStartIndex = 0;
    private readonly UICycleImage[] categoryButtons = new UICycleImage[5];
    private void OnInitialize_TopButtons(Asset<Texture2D> texture) {
        UICycleImage toggleImage;
        for (int i = 0; i < _topButtonData.Length; i++) {
            toggleImage = new(texture, _topButtonLengths[i], 32, 32, _topButtonPositionsInTexture[i].X * 34, _topButtonPositionsInTexture[i].Y * 34);
            int currentIndex = i;
            toggleImage.OnLeftClick += (_, _) => SwitchTopButtonNext(currentIndex);
            toggleImage.OnRightClick += (_, _) => SwitchTopButtonPrevious(currentIndex);
            toggleImage.OnMiddleClick += (_, _) => ResetTopButton(currentIndex);
            topMenuButtons[i] = toggleImage;
            if (i - categoryButtonStartIndex < categoryButtons.Length) {
                categoryButtons[i - categoryButtonStartIndex] = toggleImage;
            }
            mouseOverTooltips.Add((toggleImage, () => Language.GetTextValue(_topButtonLocalizedKeys[currentIndex][_topButtonData[currentIndex]])));
        }
    }
    #region 按钮切换
    // 当切换 ShowAllMods (index == 0) 时同时还要改变 CategoryButtons
    // 同时 ShowAllMods 为真 (1) 时 SortMode (index = 2) 不能为 Custom (0)

    private void SwitchTopButtonNext(int index) {
        if (index == 2 && ShowAllMods) {
            _topButtonData[index] = _topButtonData[index] % (_topButtonLengths[index] - 1) + 1;
            topMenuButtons[index].SetCurrentState(_topButtonData[index]);
        }
        else {
            _topButtonData[index] = (_topButtonData[index] + 1) % _topButtonLengths[index];
        }
        CheckTopButtonChanged(index);
        ArrangeGenerate();
    }
    private void SwitchTopButtonPrevious(int index) {
        if (index == 2 && ShowAllMods) {
            if (_topButtonData[index] <= 1) {
                _topButtonData[index] = _topButtonLengths[index] - 1;
                topMenuButtons[index].SetCurrentState(_topButtonData[index]);
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
        if (index == 2 && ShowAllMods) {
            if (_topButtonData[index] == 1) {
                return;
            }
            _topButtonData[index] = 1;
            topMenuButtons[index].SetCurrentState(1);
        }
        else {
            if (_topButtonData[index] == 0) {
                return;
            }
            _topButtonData[index] = 0;
            topMenuButtons[index].SetCurrentState(0);
        }
        CheckTopButtonChanged(index);
        ArrangeGenerate();
    }
    private void CheckTopButtonChanged(int index) {
        if (index == 0) {
            if (_topButtonData[2] == 0 && _topButtonData[0] == 1) {
                ResetTopButton(2);
            }
            GenerateTopButtons();
            ResettleVertical();
        }
        else if (index == 5) {
            ResettleVertical();
        }
    }
    #endregion
    #endregion
    #region 打开与关闭和按钮数量控制
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
        // 在文件夹系统下显示所有按钮
        if (ShowFolderSystem) {
            width = (32 + 4) * topMenuButtons.Length - 4;
            topButtonsBg.Width.Pixels = width;
            for (int i = 0; i < topMenuButtons.Length; ++i) {
                var button = topMenuButtons[i];
                button.Left.Pixels = i * (32 + 4);
                topButtonsBg.Append(button);
            }
            goto ReadyToReturn;
        }
        // 否则剔除掉文件夹和模组之间的排序按钮
        width = (32 + 4) * (topMenuButtons.Length - 1) - 4;
        topButtonsBg.Width.Pixels = width;
        topButtonsBg.Append(topMenuButtons[0]);
        for (int i = 2; i < topMenuButtons.Length; ++i) {
            var button = topMenuButtons[i];
            button.Left.Pixels = (i - 1) * (32 + 4);
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
        #endregion
    }
    public SearchFilter searchFilterMode = SearchFilter.Name;
    public string Filter { get => filterTextBox.Text; set => filterTextBox.Text = value; }
    private void ClearSearchField(UIMouseEvent evt, UIElement listeningElement) => Filter = string.Empty;
    #endregion
    #endregion

    #region ShowModLocation
    private bool _showModLocation = true;
    private void Update_ShowModLocation() {
        if (CommonConfig.Instance.ShowModLocation != _showModLocation) {
            _showModLocation = CommonConfig.Instance.ShowModLocation;
            Populate();
        }
    }
    #endregion
    #region 杂项
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
    private UIFolderItem? _draggingTarget;
    public UIFolderItem? DraggingTarget {
        get => ShowFolderSystem ? _draggingTarget : null;
        set {
            if (ShowFolderSystem || value == null) {
                _draggingTarget = value;
            }
        }
    }
    private readonly List<(UIElement, Func<string>)> mouseOverTooltips = [];
    private FolderNode? nodeToRename;
    private readonly List<UIElement> _confirmPanels = [];
    private UIImage? _confirmPanelCover;
    private UIImage ConfirmPanelCover {
        get {
            if (_confirmPanelCover != null) {
                return _confirmPanelCover;
            }
            _confirmPanelCover = new(Textures.White) {
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
    public void AppendConfirmPanel(UIElement panel) {
        _confirmPanels.Add(panel);
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
        _confirmPanels[^1].Remove();
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
            _confirmPanels[i].Remove();
            ConfirmPanelCover.Remove();
        }
        _confirmPanels.Clear();
    }
    #endregion

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
        uiPanel.Append(folderPathList);
        #endregion
        #region 刷新按钮
        #region refresh3 版
        refreshButton = new(Textures.UI("Refresh3")) {
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
        uiPanel.Append(refreshButton);
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
        uiPanel.Append(ramUsagePlaceHolder);
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
            ArrangeGenerate();
            FolderDataSystem.TrySaveWhenChanged();
        };
        #endregion
        #region 返回按钮
        ButtonB = new(Language.GetText("UI.Back"));
        ButtonB.OnLeftClick += (_, _) => {
            if (ShowFolderSystem && FolderPath.Count > 1 && !Main.keyState.PressingShift()) {
                SoundEngine.PlaySound(SoundID.MenuClose);
                GotoUpperFolder();
                return;
            }
            // TODO: 在 Deactivate 中也有, 检查是否冗余
            FolderDataSystem.Save();
            FolderPath.Clear();
            list.ViewPosition = 0;

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

            ConfigManager.OnChangedAll();

            IHaveBackButtonCommand.GoBackTo(PreviousUIState);
        };
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
                CurrentFolderNode.ClearChildren();
            }
            if (Main.keyState.PressingShift()) {
                foreach (var mod in ModItemDict.Values) {
                    if (mod.TheLocalMod.Enabled) {
                        _ = new ModNode(mod.ModName) {
                            Parent = CurrentFolderNode
                        };
                    }
                }
            }
            else {
                foreach (var mod in ModItemDict.Values) {
                    if (mod.Loaded) {
                        _ = new ModNode(mod.ModName) {
                            Parent = CurrentFolderNode
                        };
                    }
                }
            }
            ArrangeGenerate();
            FolderDataSystem.TrySaveWhenChanged();
        };
        mouseOverTooltips.Add((ButtonCopyEnabled, () => ModFolder.Instance.GetLocalization("UI.Buttons.CopyEnabled.Tooltip").Value));
        #endregion
        #region 禁用冗余前置
        ButtonDisableRedundant = new(ModFolder.Instance.GetLocalization("UI.Buttons.DisableRedundant.DisplayName"));
        ButtonDisableRedundant.OnLeftClick += (_, _) => DisableRedundant(false);
        ButtonDisableRedundant.OnRightClick += (_, _) => DisableRedundant(true);
        mouseOverTooltips.Add((ButtonDisableRedundant, () => ModFolder.Instance.GetLocalization("UI.Buttons.DisableRedundant.Tooltip").Value));
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
        #region 右键松开时尝试移动位置
        OnRightMouseUp += OnRightMouseUp_RightDrag;
        #endregion
        #region 鼠标 4 键返回
        OnXButton1MouseDown += (_, _) => GotoUpperFolder();
        #endregion
        // 最后添加搜索过滤条, 防止输入框被完全占用 (如果在 list 之前那么就没法重命名了)
        uiPanel.Append(upperMenuContainer);
        Append(uiElement);
        OnInitialize_Debug();
    }

    private void ResettleVertical() {
        float upperPixels = upperMenuContainer.Top.Pixels + upperMenuContainer.Height.Pixels;
        if (ShowRamUsage) {
            upperPixels += 2;
            ramUsage.Top.Pixels = upperPixels;
            uiPanel.ReplaceChildren(ramUsagePlaceHolder, ramUsage, false, onReplace: ramUsage.Show);
            upperPixels += ramUsage.Height.Pixels; // 20
        }
        else {
            uiPanel.ReplaceChildren(ramUsage, ramUsagePlaceHolder, false);
        }
        if (!ShowAllMods) {
            upperPixels += 2;
            folderPathList.Top.Pixels = upperPixels;
            refreshButton.Top.Pixels = upperPixels;
            uiPanel.ReplaceChildren(folderPathListPlaceHolder, folderPathList, false);
            uiPanel.ReplaceChildren(refreshButtonPlaceHolder, refreshButton, false);
            upperPixels += folderPathList.Height.Pixels; // 30
        }
        else {
            uiPanel.ReplaceChildren(folderPathList, folderPathListPlaceHolder, false);
            uiPanel.ReplaceChildren(refreshButton, refreshButtonPlaceHolder, false);
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

    #region 启用禁用与重置模组
    // 只启用或禁用本文件夹下的模组, 按住 shift 时才是所有模组
    // 按住 alt 同时包含子文件夹
    // 按住 ctrl 在禁用时同时禁用收藏
    // 使用悬浮文字以提示这些操作
    private IEnumerable<UIModItemInFolder> GetAffectedMods(bool ignoreFavorite = false) {
        IEnumerable<UIModItemInFolder> result;
        if (Main.keyState.PressingShift()) {
            result = ModItemDict.Values;
        }
        else if (!Main.keyState.PressingAlt()) {
            result = list._items.Select(i => i as UIModItemInFolder).WhereNotNull();
        }
        else if (CurrentFolderNode == FolderDataSystem.Root) {
            result = ModItemDict.Values;
        }
        else {
            result = CurrentFolderNode.ModNodesInTree.ToHashSet().Select(m => ModItemDict.TryGetValue(m.ModName, out var mod) ? mod : null).WhereNotNull();
        }
        if (ignoreFavorite) {
            result = result.Where(i => !i.Favorite);
        }
        return result;
    }
    private void EnableMods() {
        SoundEngine.PlaySound(SoundID.MenuTick);
        // TODO: 未加载完成时给出(悬浮)提示
        if (!Loaded) {
            return;
        }
        HashSet<string> enabled = [];
        HashSet<string> missingRefs = [];
        foreach (var modItem in GetAffectedMods()) {
            if (modItem == null || modItem.tMLUpdateRequired != null)
                continue;
            modItem.EnableQuick(enabled, missingRefs);
        }
        if (enabled.Count == 0)
            return;
        if (missingRefs.Count != 0) {
            Interface.infoMessage.Show(Language.GetTextValue("tModLoader.ModDependencyModsNotFound", string.Join(", ", missingRefs)), MyMenuMode);
        }
        if (EnabledFilterMode != FolderEnabledFilter.All) {
            ArrangeGenerate();
        }
        // Logging.tML.Info
        ModFolder.Instance.Logger.Info("Enabling mods: " + string.Join(", ", enabled));
        ModOrganizer.SaveEnabledMods();
    }
    private void DisableMods(bool disableRedundantDependencies, bool ignoreFavorite) {
        SoundEngine.PlaySound(SoundID.MenuTick);
        // TODO: 在同时取消收藏且全部取消时不用在乎是否加载完成, 可一键全部取消 (ModLoader.DisableAllMods();)
        // TODO: 未加载完成时给出(悬浮)提示
        if (!Loaded) {
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
        ModOrganizer.SaveEnabledMods();
    }
    private void ResetMods() {
        SoundEngine.PlaySound(SoundID.MenuTick);
        // TODO: 未加载完成时给出(悬浮)提示
        if (!Loaded) {
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
            ModOrganizer.SaveEnabledMods();
        }
    }
    private void DisableRedundant(bool rightclick) {
        SoundEngine.PlaySound(SoundID.MenuTick);
        // TODO: 未加载完成时给出(悬浮)提示
        if (!Loaded) {
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
        ModOrganizer.SaveEnabledMods();
    }
    #endregion

    private void Refresh(UIMouseEvent mouse, UIElement element) {
        SoundEngine.PlaySound(SoundID.MenuTick);
        if (Loading) {
            return;
        }
        Populate();
    }

    public static bool IsPreviousUIStateOfConfigList { get; set; }

    public UIModItemInFolder? FindUIModItem(string modName) {
        return ModItemDict.GetValueOrDefault(modName);
    }

    public override void Update(GameTime gameTime) {
        Timer += 1;
        Update_RemoveChildrenToRemove();
        base.Update(gameTime);
        Update_ShowModLocation();
        Update_HandleTask(); // 处理任务在添加或移除加载动画前
        Update_AppendOrRemoveUILoader();  // 尝试移除加载动画
        Update_DeleteMods(); // 尝试删除模组要在尝试生成之前
        Update_Generate();
    }

    #region 生成 Generate
    private bool _generateNeeded;
    public void ArrangeGenerate() => _generateNeeded = true;
    private void Update_Generate() {
        if (_generateNeeded) {
            _generateNeeded = false;
            Generate();
        }
    }
    private void Generate() {
        list.Clear();
        list.StopMoving();
        DraggingTarget = null;
        ClearConfirmPanels(true);
        var filterResults = new UIModsFilterResults();
        var visibleItems = GetVisibleItems(filterResults);
        #region 若有任何被过滤的, 则在列表中添加一个元素提示过滤了多少东西
        if (filterResults.AnyFiltered) {
            var panel = new UIPanel();
            panel.Width.Set(0, 1f);
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
            list.Add(panel);
        }
        #endregion
        #region 若不在根目录, 则添加一个返回上级的文件夹
        if (ShowFolderSystem && CurrentFolderNode != FolderDataSystem.Root) {
            UIFolder upperFolder = new("..");
            list.Add(upperFolder);
            upperFolder.RightDraggable = false;
            upperFolder.Activate();
        }
        #endregion
        visibleItems = visibleItems.HeapSort((u1, u2) => u1.CompareTo(u2));
        list.AddRange(visibleItems);
        foreach (var item in visibleItems) {
            item.Activate();
        }
        Recalculate();
        if (_listViewPositionToSetAfterGenerated != null) {
            list.ViewPosition = _listViewPositionToSetAfterGenerated.Value;
            _listViewPositionToSetAfterGenerated = null;
        }
    }
    private List<UIFolderItem> GetVisibleItems(UIModsFilterResults filterResults) {
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
    private IEnumerable<UIFolderItem> GetVisibleItems_AllMods(UIModsFilterResults filterResults) {
        foreach (var item in ModItemDict.Values) {
            if (item.PassFilters(filterResults)) {
                yield return item;
            }
        }
    }
    private IEnumerable<UIFolderItem> GetVisibleItems_InFolderSystem(UIModsFilterResults filterResults) {
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
                    m.ReceiveDataFrom(uiMod);
                    if (uiMod.PassFilters(filterResults)) {
                        uiMod.ModNode = m;
                        yield return uiMod;
                    }
                    continue;
                }
                yield return new UIModItemInFolderUnloaded(m);
            }
            else if (node is FolderNode f) {
                var uf = new UIFolder(f);
                if (f == nodeToRename) {
                    uf.DirectlyReplaceToRenameText();
                    yield return uf;
                }
                else if (uf.PassFilters(filterResults)) {
                    yield return uf;
                }
            }
        }
        if (nodesToRemove.Count != 0) {
            foreach (var nodeToRemove in nodesToRemove) {
                nodeToRemove.Parent = null;
            }
            FolderDataSystem.TrySaveWhenChanged();
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
                Parent = FolderDataSystem.Root
            };
            if (value.PassFilters(filterResults)) {
                value.ModNode = m;
                yield return value;
            }
        }
        if (created) {
            FolderDataSystem.TrySaveWhenChanged();
        }
        #endregion
    }
    private float? _listViewPositionToSetAfterGenerated;
    private void SetListViewPositionAfterGenerated(float value) => _listViewPositionToSetAfterGenerated = value;
    #endregion

    public override void Draw(SpriteBatch spriteBatch) {
        UILinkPointNavigator.Shortcuts.BackButtonCommand = 7;
        Draw_TryCloseButtons();
        SetDraggingPosition();
        base.Draw(spriteBatch);
        /*
        // 鼠标 4 键返回
        if (Main.mouseXButton1 && Main.mouseXButton1Release) {
            if (FolderPath.Count > 1) {
                GotoUpperFolder();
                Main.mouseXButton1Release = false;
            }
        }
        */
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
    #region 右键拖动
    // 在 Draw 之前执行
    private void SetDraggingPosition() {
        if (DraggingTarget == null) {
            return;
        }
        #region 将要拖动的元素转为 Node
        Node? node = DraggingTarget.Node;
        if (node == null) {
            return;
        }
        #endregion
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
    private bool CanCustomizeOrder() {
        for (int i = 0; i < _topButtonData.Length; ++i) {
            if (_topButtonData[i] != 0) {
                return false;
            }
        }
        return Filter == string.Empty;
    }

    private void OnRightMouseUp_RightDrag(UIMouseEvent mouse, UIElement element) {
        if (DraggingTarget == null) {
            return;
        }
        var draggingTarget = DraggingTarget;
        DraggingTarget = null;
        #region 将要拖动的元素转为 Node
        Node? node = draggingTarget.Node;
        if (node == null) {
            return;
        }
        #endregion
        if (_draggingTo == null) {
            return;
        }
        Node? aimNode = null;
        if (_draggingTo is UIFolderItem fi) {
            aimNode = fi.Node;
        }
        else if (_draggingTo is UIFolderPathItem pi) {
            aimNode = pi.FolderNode;
        }
        if (aimNode == null) {
            if (_draggingTo is UIFolder folder && folder.Name == "..") {
                if (DraggingDirection == 0) {
                    // 保险起见判断一下, 理应总是符合该条件的
                    if (FolderPath.Count > 1)
                        MoveNodeIntoFolder(node, FolderPath[^2]);
                }
                else {
                    MoveNodeToTheStart(node);
                }
            }
            return;
        }
        if (DraggingDirection < 0) {
            MoveNodeBeforeNode(node, aimNode);
        }
        else if (DraggingDirection > 0) {
            MoveNodeAfterNode(node, aimNode);
        }
        else {
            if (aimNode is FolderNode aimFolder) {
                MoveNodeIntoFolder(node, aimFolder);
            }
        }
        #region 辅助方法
        void MoveNodeIntoFolder(Node node, FolderNode folder) {
            if (node == folder) {
                return;
            }
            if (node.Parent != CurrentFolderNode) {
                ModFolder.Instance.Logger.Error("node's parent should be the current folder at MoveNodeIntoFolder");
                // return;
            }
            if (Main.keyState.PressingControl() && node is ModNode mn) {
                node = new ModNode(mn);
            }
            else {
                node.Parent = null;
            }

            if (node is ModNode modNode) {
                foreach (var child in folder.Children) {
                    if (child is ModNode m && m.ModName == modNode.ModName) {
                        ArrangeGenerate();
                        return;
                    }
                }
            }
            node.Parent = folder;
            ArrangeGenerate();
            FolderDataSystem.TrySaveWhenChanged();
        }
        void MoveNodeBeforeNode(Node node, Node target) {
            if (CurrentFolderNode.MoveChildBeforeChild(node, target)) {
                ArrangeGenerate();
                FolderDataSystem.TrySaveWhenChanged();
            }
        }
        void MoveNodeAfterNode(Node node, Node target) {
            if (CurrentFolderNode.MoveChildAfterChild(node, target)) {
                ArrangeGenerate();
                FolderDataSystem.TrySaveWhenChanged();
            }
        }
        void MoveNodeToTheStart(Node node) {
            if (node.MoveToTheTop()) {
                ArrangeGenerate();
                FolderDataSystem.TrySaveWhenChanged();
            }
        }
        #endregion
    }
    #endregion

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
        SetListViewPositionAfterGenerated(list.ViewPosition);
        FolderDataSystem.Save();
    }

    #region 加载相关
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
    private void Update_HandleTask() {
        if (loadTask == null || !loadTask.IsCompleted) {
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
        await YieldWithToken(token);
        var mods = ModOrganizer.FindMods(CommonConfig.Instance.LogModLoading);
        SetLoadingState("Loading Mods");
        await YieldWithToken(token);
        Dictionary<string, UIModItemInFolder> tempModItemDict = [];
        foreach (var mod in mods) {
            UIModItemInFolder modItem = new(mod);
            tempModItemDict.Add(modItem.ModName, modItem);
            modItem.Activate();
            await YieldWithToken(token);
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
        await YieldWithToken(token);
        var availableMods = ModOrganizer.RecheckVersionsToLoad().ToArray();
        // TODO: 算法优化 (虽然不是很有必要)
        foreach (var modItem in ModItemDict.Values) {
            modItem.SetModReferences(availableMods);
        }

        FolderDataSystem.RemoveRedundantData();
        // TODO: 遍历一遍 Root 来做各种事情
        ;
    }
    private static YieldAwaitable YieldWithToken(CancellationToken token) {
        token.ThrowIfCancellationRequested();
        return Task.Yield();
    }
    #endregion
    #endregion

    #region ArrangeDeleteMod
    private readonly HashSet<LocalMod> _modsToDelete = [];
    public void ArrangeDeleteMod(UIModItemInFolder uiMod) {
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
            string text = $"Task is null: {loadTask == null}";
            if (loadTask != null) {
                text += $"""
                ,
                Task.IsCompleted: {loadTask.IsCompleted},
                Task.IsCompletedSuccessfully: {loadTask.IsCompletedSuccessfully},
                Task.IsFaulted: {loadTask.IsFaulted},
                Task.IsCanceled: {loadTask.IsCanceled}
                loadingState: {loadingState}
                """;
            }
            debugTextUI.SetText(text);
        };
        Append(debugTextPanel);
    }
}
