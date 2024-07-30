using Microsoft.Xna.Framework.Input;
using ModFolder.Systems;
using ReLogic.Content;
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
        Instance.PreviousUIState = hub;
        Main.MenuUI.SetState(Instance);
        Instance.SetListViewPositionAfterGenerated(0);
    }
    public const int MyMenuMode = 47133;

    public UIModFolderMenu() {
        int enumCount = _sortEnumTypes.Length;
        _sortEnumLengths = new int[enumCount];
        for (int i = 0; i < enumCount; ++i) {
            _sortEnumLengths[i] = Enum.GetValues(_sortEnumTypes[i]).Length;
        }
    }

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
    private Dictionary<string, UIModItemInFolder> ModItemDict { get; set; } = [];
    #endregion
    #region 文件夹路径
    /// <summary>
    /// 当前处于哪个文件夹下, 若为空则代表处于根目录下
    /// </summary>
    public FolderNode CurrentFolderNode => FolderPath[^1];
    public UIHorizontalList folderPathList = null!;
    private UIElement folderPathListPlaceHolder = new();

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
    public void EnterFolder(FolderNode folder) {
        bool max = folderPathList.ViewPosition == folderPathList.MaxViewPosition;
        // TODO: 换成 Parent 检测
        if (!CurrentFolderNode.Children.Contains(folder)) {
            return;
        }
        FolderPath.Add(folder);
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
    private UIElement uIElement = null!;
    private UIPanel uiPanel = null!;
    private UIImagePro refreshButton = null!;
    private UIElement refreshButtonPlaceHolder = new();
    private UIFolderItemList list = null!;
    private UIScrollbar uiScrollbar = null!;
    #region 下面的一堆按钮
    private UIAutoScaleTextTextPanel<LocalizedText> ButtonAllMods { get => buttons[0]; set => buttons[0] = value; }
    private UIAutoScaleTextTextPanel<LocalizedText> ButtonRM { get => buttons[1]; set => buttons[1] = value; }
    private UIAutoScaleTextTextPanel<LocalizedText> ButtonOMF { get => buttons[2]; set => buttons[2] = value; }
    private UIAutoScaleTextTextPanel<LocalizedText> ButtonCL { get => buttons[3]; set => buttons[3] = value; }
    private UIAutoScaleTextTextPanel<LocalizedText> ButtonCreateFolder { get => buttons[4]; set => buttons[4] = value; }
    private UIAutoScaleTextTextPanel<LocalizedText> ButtonB { get => buttons[5]; set => buttons[5] = value; }
    private readonly UIAutoScaleTextTextPanel<LocalizedText>[] buttons = new UIAutoScaleTextTextPanel<LocalizedText>[6];
    #endregion
    #endregion

    #region 排序与过滤相关
    #region 公开属性
    public MenuShowType        ShowType          { get => (MenuShowType       )_sortEnumData[0]; set => _sortEnumData[0] = (int)value; }
    public FolderModSortMode   FmSortMode        { get => (FolderModSortMode  )_sortEnumData[1]; set => _sortEnumData[1] = (int)value; }
    public FolderMenuSortMode  SortMode          { get => (FolderMenuSortMode )_sortEnumData[2]; set => _sortEnumData[2] = (int)value; }
    public FolderEnabledFilter EnabledFilterMode { get => (FolderEnabledFilter)_sortEnumData[3]; set => _sortEnumData[3] = (int)value; }
    public ModSideFilter       ModSideFilterMode { get => (ModSideFilter      )_sortEnumData[4]; set => _sortEnumData[4] = (int)value; }
    #endregion
    #region 数据与常数
    private readonly int[] _sortEnumData = new int[5];
    private readonly Type[] _sortEnumTypes = [typeof(MenuShowType), typeof(FolderModSortMode), typeof(FolderMenuSortMode), typeof(FolderEnabledFilter), typeof(ModSideFilter)];
    private readonly int[] _sortEnumLengths;
    private readonly Point[] _sortEnumPositionInTexture = [
        new(2, 6),
        new(0, 5),
        new(0, 0),
        new(1, 0),
        new(2, 0),
    ];
    private readonly string[][] _sortEnumLocalizedKeys = [
        [
            "Mods.ModFolder.UI.SortButtons.FolderSystem.Tooltip",
            "Mods.ModFolder.UI.SortButtons.AllMods.Tooltip",
        ],
        [
            "Mods.ModFolder.UI.SortButtons.Custom.Tooltip",
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
        uiPanel.Append(upperMenuContainer);
    }
    #endregion
    #region 排序与过滤的按钮
    private void OnInitialize_SortButtons(Asset<Texture2D> texture) {
        OnInitialize_ButtonToggleCategoryButtons(texture); // 总按钮
        OnInitialize_CategoryButtonBg(); // 其余按钮的背景板
        OnInitialize_CategoryButtons(texture);
    }
    #region 总按钮
    private UIImagePro buttonToggleCategoryButtons = null!;
    private void OnInitialize_ButtonToggleCategoryButtons(Asset<Texture2D> texture) {
        buttonToggleCategoryButtons = new(texture) {
            Width = { Pixels = 32 },
            Height = { Pixels = 32 },
            SourceRectangle = new(2 * 34, 5 * 34, 32, 32),
        };
        // 左键切换开关
        buttonToggleCategoryButtons.OnLeftClick += (_, _) => ToggleCategoryButtons();
        // 中键重置
        buttonToggleCategoryButtons.OnMiddleClick += (_, _) => {
            for (int i = 0; i < _sortEnumData.Length; ++i) {
                _sortEnumData[i] = 0;
            }
            foreach (var button in categoryButtons) {
                button.SetCurrentState(0);
            }
            Filter = string.Empty;
            ResettleVertical();
            ArrangeGenerate();
        };
        // 点到其它位置时关闭
        OnLeftMouseDown += (_, _) => {
            if (!buttonToggleCategoryButtons.IsMouseHovering && !categoryButtonBg.IsMouseHovering) {
                ArrangeCloseCategoryButtons();
            }
        };
        // 当鼠标放上去时高亮
        buttonToggleCategoryButtons.PreDrawSelf += spriteBatch => {
            buttonToggleCategoryButtons.Color = buttonToggleCategoryButtons.IsMouseHovering ? Color.White : Color.Silver;
        };
        // 当鼠标放上去时同时也有说明文字
        mouseOverTooltips.Add((buttonToggleCategoryButtons, () => _categoryButtonsOpen
            ? ModFolder.Instance.GetLocalization("UI.Buttons.ToggleCategoryButtons.TooltipOff").Value
            : ModFolder.Instance.GetLocalization("UI.Buttons.ToggleCategoryButtons.TooltipOn").Value));
        // 添加到搜索过滤条上
        upperMenuContainer.Append(buttonToggleCategoryButtons);
    }
    #endregion
    #region 其余按钮的背景板
    private UIElement categoryButtonBg = null!;
    private void OnInitialize_CategoryButtonBg() {
        categoryButtonBg = new() {
            Left = { Pixels = 36 },
            Height = { Pixels = 36 },
        };
        upperMenuContainer.Append(categoryButtonBg);
    }
    #endregion
    #region 其余按钮
    private readonly UICycleImage[] categoryButtons = new UICycleImage[5];
    private void OnInitialize_CategoryButtons(Asset<Texture2D> texture) {
        UICycleImage toggleImage;
        for (int j = 0; j < _sortEnumData.Length; j++) {
            toggleImage = new(texture, _sortEnumLengths[j], 32, 32, _sortEnumPositionInTexture[j].X * 34, _sortEnumPositionInTexture[j].Y * 34);
            int currentIndex = j;
            toggleImage.OnLeftClick += (_, _) => SwitchCategoryNext(currentIndex);
            toggleImage.OnRightClick += (_, _) => SwitchCategoryPrevious(currentIndex);
            toggleImage.OnMiddleClick += (_, _) => ResetCategory(currentIndex);
            toggleImage.HAlign = (float)j / (_sortEnumData.Length - 1);
            categoryButtons[j] = toggleImage;
            mouseOverTooltips.Add((toggleImage, () => Language.GetTextValue(_sortEnumLocalizedKeys[currentIndex][_sortEnumData[currentIndex]])));
        }
    }
    #region
    #endregion 按钮切换
    // 当切换 ShowType (index = 0) 时同时还要改变 CategoryButtons
    // 同时 ShowType 为 AllMods (1) 时 SortMode (index = 2) 不能为 Custom (0)

    private void SwitchCategoryNext(int index) {
        if (index == 2 && ShowType == MenuShowType.AllMods) {
            _sortEnumData[index] = _sortEnumData[index] % (_sortEnumLengths[index] - 1) + 1;
            categoryButtons[index].SetCurrentState(_sortEnumData[index]);
        }
        else {
            _sortEnumData[index] = (_sortEnumData[index] + 1) % _sortEnumLengths[index];
        }
        CheckShowTypeChanged(index);
        ArrangeGenerate();
    }
    private void SwitchCategoryPrevious(int index) {
        if (index == 2 && ShowType == MenuShowType.AllMods) {
            if (_sortEnumData[index] <= 1) {
                _sortEnumData[index] = _sortEnumLengths[index] - 1;
                categoryButtons[index].SetCurrentState(_sortEnumData[index]);
            }
            else {
                _sortEnumData[index] -= 1;
            }
        }
        else {
            if (_sortEnumData[index] == 0) {
                _sortEnumData[index] = _sortEnumLengths[index] - 1;
            }
            else {
                _sortEnumData[index] -= 1;
            }
        }
        CheckShowTypeChanged(index);
        ArrangeGenerate();
    }
    private void ResetCategory(int index) {
        if (index == 2 && ShowType == MenuShowType.AllMods) {
            if (_sortEnumData[index] == 1) {
                return;
            }
            _sortEnumData[index] = 1;
            categoryButtons[index].SetCurrentState(1);
        }
        else {
            if (_sortEnumData[index] == 0) {
                return;
            }
            _sortEnumData[index] = 0;
            categoryButtons[index].SetCurrentState(0);
        }
        CheckShowTypeChanged(index);
        ArrangeGenerate();
    }
    private void CheckShowTypeChanged(int index) {
        if (index != 0) {
            return;
        }
        if (_sortEnumData[2] == 0 && _sortEnumData[0] == 1) {
            ResetCategory(2);
        }
        GenerateCategoryButtons();
        ResettleVertical();
    }
    #endregion
    #region 打开与关闭和按钮数量控制
    private bool _categoryButtonsOpen;
    private bool _toCloseCategoryButtons;
    private void ArrangeCloseCategoryButtons() => _toCloseCategoryButtons = true;
    private void OpenCategoryButtons() {
        if (_categoryButtonsOpen) {
            return;
        }
        _categoryButtonsOpen = true;
        GenerateCategoryButtons();
    }
    private void CloseCategoryButtons() {
        if (!_categoryButtonsOpen) {
            return;
        }
        _categoryButtonsOpen = false;
        GenerateCategoryButtons();
    }
    private void ToggleCategoryButtons() {
        if (_categoryButtonsOpen) {
            CloseCategoryButtons();
        }
        else {
            OpenCategoryButtons();
        }
    }
    private void GenerateCategoryButtons() {
        categoryButtonBg.RemoveAllChildren();
        int width;
        if (!_categoryButtonsOpen) {
            width = -4;
            categoryButtonBg.Width.Pixels = 0;
            goto ReadyToReturn;
        }
        if (ShowType == MenuShowType.FolderSystem) {
            width = (32 + 4) * categoryButtons.Length - 4;
            categoryButtonBg.Width.Pixels = width;
            for (int i = 0; i < categoryButtons.Length; ++i) {
                var button = categoryButtons[i];
                button.HAlign = (float)i / (categoryButtons.Length - 1);
                categoryButtonBg.Append(button);
            }
            goto ReadyToReturn;
        }
        width = (32 + 4) * (categoryButtons.Length - 1) - 4;
        categoryButtonBg.Width.Pixels = width;
        categoryButtonBg.Append(categoryButtons[0]);
        for (int i = 2; i < categoryButtons.Length; ++i) {
            var button = categoryButtons[i];
            button.HAlign = (float)(i - 1) / (categoryButtons.Length - 2);
            categoryButtonBg.Append(button);
        }
    ReadyToReturn:
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
        get => ShowType == MenuShowType.FolderSystem ? _draggingTarget : null;
        set {
            if (ShowType == MenuShowType.FolderSystem || value == null) {
                _draggingTarget = value;
            }
        }
    }
    private readonly List<(UIElement, Func<string>)> mouseOverTooltips = [];
    private FolderNode? nodeToRename;
    #endregion

    public override void OnInitialize() {
        #region 全部元素的容器
        // UICommon.MaxPanelWidth  // 600
        uIElement = new UIElement {
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
        uIElement.Append(uiPanel);
        #endregion
        OnInitialize_Loading(); // 正在加载时的循环图标
        float upperPixels = 10;
        OnInitialize_UpperMenuContainer(ref upperPixels);   // 排序与过滤的条
        #region 文件夹路径
        upperPixels += 2;
        folderPathList = new();
        folderPathList.Top.Pixels = upperPixels;
        folderPathList.Height.Pixels = 30;
        folderPathList.Width.Set(-40, 1);
        folderPathList.Left.Set(5, 0);
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
        #region 启用与禁用按钮
        ButtonAllMods = new(ModFolder.Instance.GetLocalization("UI.Buttons.AllMods.DisplayName"));
        ButtonAllMods.OnLeftClick += EnableMods;
        ButtonAllMods.OnRightClick += DisableMods;
        ButtonAllMods.OnMiddleClick += ResetMods;
        mouseOverTooltips.Add((ButtonAllMods, () => ModFolder.Instance.GetLocalization("UI.Buttons.AllMods.Tooltip").Value));
        #endregion
        #region 重新加载按钮
        // 被返回按钮替代了, 可以换成其它按钮
        ButtonRM = new UIAutoScaleTextTextPanel<LocalizedText>(Language.GetText("tModLoader.ModsForceReload"));
        ButtonRM.OnLeftClick += (_, _) => {
            SoundEngine.PlaySound(SoundID.MenuOpen);
            if (ModItemDict.Count > 0)
                ModLoader.Reload();
        };
        #endregion
        #region 打开模组文件夹按钮
        ButtonOMF = new UIAutoScaleTextTextPanel<LocalizedText>(Language.GetText("tModLoader.ModsOpenModsFolders"));
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
        ButtonCL = new UIAutoScaleTextTextPanel<LocalizedText>(Language.GetText("tModLoader.ModConfiguration"));
        ButtonCL.OnLeftClick += (_, _) => {
            SoundEngine.PlaySound(SoundID.MenuOpen);
            Main.menuMode = Interface.modConfigListID;
            IsPreviousUIStateOfConfigList = true;
        };
        #endregion
        #region 新建文件夹按钮
        ButtonCreateFolder = new(ModFolder.Instance.GetLocalization("UI.Buttons.CreateFolder.DisplayName"));
        ButtonCreateFolder.OnLeftClick += (_, _) => {
            SoundEngine.PlaySound(SoundID.MenuTick);
            FolderNode node = new(ModFolder.Instance.GetLocalization("UI.NewFolderDefaultName").Value);
            CurrentFolderNode.Children.Insert(0, node);
            nodeToRename = node;
            list.ViewPosition = 0;
            ArrangeGenerate();
        };
        #endregion
        #region 返回按钮
        ButtonB = new UIAutoScaleTextTextPanel<LocalizedText>(Language.GetText("UI.Back"));
        ButtonB.OnLeftClick += (_, _) => {
            if (!Main.keyState.PressingShift() && FolderPath.Count > 1) {
                GotoUpperFolder();
                return;
            }
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
        #region 按钮处理位置
        /*
        for (int i = 0; i < buttons.Length; ++i) {
            var button = buttons[i];
            button.Width.Pixels = 200;
            button.Height.Pixels = 40;
            button.Left.Pixels = 20;
            button.Top.Pixels = 220 + 45 * i;
            button.WithFadedMouseOver();
            Append(button);
        }
        */
        int row1 = buttons.Length / 2;
        for (int i = 0; i < row1; ++i) {
            var button = buttons[i];
            button.Width.Set(-10, 1f / row1);
            button.Height.Pixels = 40;
            button.HAlign = i * (1f / Math.Max(1, row1 - 1));
            button.VAlign = 1;
            button.Top.Pixels = -65;
            button.WithFadedMouseOver();
            uIElement.Append(button);
        }
        int row2 = buttons.Length - row1;
        for (int i = row1; i < buttons.Length; ++i) {
            var button = buttons[i];
            button.Width.Set(-10, 1f / row2);
            button.Height.Pixels = 40;
            button.HAlign = (buttons.Length - i - 1) * (1f / Math.Max(1, row2 - 1));
            button.VAlign = 1;
            button.Top.Pixels = -20;
            button.WithFadedMouseOver();
            uIElement.Append(button);
        }
        /*
        for (int i = 0; i < buttons.Length; ++i) {
            var button = buttons[i];
            button.Width.Pixels = 200;
            button.Height.Pixels = 40;
            button.Left.Set(-550, 0.5f);
            button.Left.Set(20, 0);
            button.Top.Pixels = 220 + 45 * i;
            button.WithFadedMouseOver();
            Append(button);
        }
        */
        #endregion
        #endregion
        #region 右键松开时尝试移动位置
        OnRightMouseUp += OnRightMouseUp_RightDrag;
        #endregion
        Append(uIElement);
    }

    private void ResettleVertical() {
        float upperPixels = folderPathList.Top.Pixels + 6;
        if (ShowType == MenuShowType.AllMods) {
            uiPanel.ReplaceChildren(folderPathList, folderPathListPlaceHolder, false);
            uiPanel.ReplaceChildren(refreshButton, refreshButtonPlaceHolder, false);
        }
        else {
            uiPanel.ReplaceChildren(folderPathListPlaceHolder, folderPathList, false);
            uiPanel.ReplaceChildren(refreshButtonPlaceHolder, refreshButton, false);
            upperPixels += 30;
        }
        list.Top.Pixels = upperPixels;
        list.Height.Pixels = -upperPixels;
        uiScrollbar.Top.Pixels = upperPixels;
        uiScrollbar.Height.Pixels = -upperPixels;
        uiPanel.RecalculateChildren();
    }

    #region 启用禁用与重置模组
    // 只启用或禁用本文件夹下的模组, 按住 shift 时才是所有模组
    // 按住 alt 同时包含子文件夹
    // 按住 ctrl 在禁用时同时禁用收藏
    // 使用悬浮文字以提示这些操作
    private IEnumerable<UIModItemInFolder> GetAffectedMods(bool tryIgnoreFavorite = false) {
        IEnumerable<UIModItemInFolder> result;
        if (Main.keyState.PressingShift()) {
            result = ModItemDict.Values;
        }
        else if (!Main.keyState.IsKeyDown(Keys.LeftAlt) && !Main.keyState.IsKeyDown(Keys.RightAlt)) {
            result = list._items.Select(i => i as UIModItemInFolder).WhereNotNull();
        }
        else if (CurrentFolderNode == FolderDataSystem.Root) {
            result = ModItemDict.Values;
        }
        else {
            result = CurrentFolderNode.ModNodesInTree.Select(m => ModItemDict.TryGetValue(m.ModName, out var mod) ? mod : null).WhereNotNull();
        }
        if (tryIgnoreFavorite && !Main.keyState.PressingControl()) {
            result = result.Where(i => !i.Favorite);
        }
        return result;
    }
    private void EnableMods(UIMouseEvent mouse, UIElement element) {
        SoundEngine.PlaySound(SoundID.MenuTick);
        // TODO: 未加载完成时给出(悬浮)提示
        if (!loaded) {
            return;
        }
        HashSet<string> enabled = [];
        foreach (var modItem in GetAffectedMods()) {
            if (modItem == null || modItem.tMLUpdateRequired != null)
                continue;
            if (ModLoader.EnabledMods.Add(modItem.ModName)) {
                enabled.Add(modItem.ModName);
            }
        }
        if (enabled.Count == 0)
            return;
        // Logging.tML.Info
        ModFolder.Instance.Logger.Info("Enabling mods: " + string.Join(", ", enabled));
        ModOrganizer.SaveEnabledMods();
    }
    private void DisableMods(UIMouseEvent mouse, UIElement element) {
        SoundEngine.PlaySound(SoundID.MenuTick);
        // TODO: 在同时取消收藏且全部取消时不用在乎是否加载完成, 可一键全部取消 (ModLoader.DisableAllMods();)
        // TODO: 未加载完成时给出(悬浮)提示
        if (!loaded) {
            return;
        }
        HashSet<string> disabled = [];
        foreach (var modItem in GetAffectedMods(true)) {
            if (modItem == null)
                continue;
            if (ModLoader.EnabledMods.Remove(modItem.ModName)) {
                disabled.Add(modItem.ModName);
            }
        }
        if (disabled.Count == 0) {
            return;
        }
        // Logging.tML.Info
        ModFolder.Instance.Logger.Info("Disabling mods: " + string.Join(", ", disabled));
        ModOrganizer.SaveEnabledMods();
    }
    private void ResetMods(UIMouseEvent mouse, UIElement element) {
        SoundEngine.PlaySound(SoundID.MenuTick);
        // TODO: 未加载完成时给出(悬浮)提示
        if (!loaded) {
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
        if (enabled.Count != 0 || disabled.Count != 0)
            ModOrganizer.SaveEnabledMods();
    }
    #endregion

    private void Refresh(UIMouseEvent mouse, UIElement element) {
        SoundEngine.PlaySound(SoundID.MenuTick);
        if (!loaded && modItemsTask != null) {
            return;
        }
        Populate();
    }

    public static bool IsPreviousUIStateOfConfigList { get; set; }

    public UIModItemInFolder? FindUIModItem(string modName) {
        return ModItemDict.Values.SingleOrDefault(m => m.ModName == modName);
    }

    public override void Update(GameTime gameTime) {
        Timer += 1;
        Update_RemoveChildrenToRemove();
        base.Update(gameTime);
        // 当加载完成时做一些事情
        if (modItemsTask is { IsCompleted: true, IsFaulted: true }) {
            // TODO: 检查报错
            ;
        }
        Update_TryRemoveLoading();  // 尝试移除加载动画
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
        if (ShowType == MenuShowType.FolderSystem && CurrentFolderNode != FolderDataSystem.Root) {
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
        if (ShowType == MenuShowType.AllMods) {
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
        foreach (var node in CurrentFolderNode.Children) {
            if (node is ModNode m) {
                if (ModItemDict.TryGetValue(m.ModName, out var uiMod)) {
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
        #region 在根目录下时将文件夹树未包含的 Mod 全部放进来
        if (CurrentFolderNode != FolderDataSystem.Root) {
            yield break;
        }
        // TODO: 缓存此结果, 且在文件夹树发生变化时做出修改
        HashSet<string> modsInFolder = [];
        foreach (var m in FolderDataSystem.Root.ModNodesInTree) {
            modsInFolder.Add(m.ModName);
        }
        foreach (var (key, value) in ModItemDict) {
            if (modsInFolder.Contains(key)) {
                continue;
            }
            ModNode m = new(value.TheLocalMod);
            FolderDataSystem.Root.Children.Add(m);
            if (value.PassFilters(filterResults)) {
                value.ModNode = m;
                yield return value;
            }
        }
        #endregion
    }
    private float? _listViewPositionToSetAfterGenerated;
    private void SetListViewPositionAfterGenerated(float value) => _listViewPositionToSetAfterGenerated = value;
    #endregion

    public override void Draw(SpriteBatch spriteBatch) {
        UILinkPointNavigator.Shortcuts.BackButtonCommand = 7;
        if (_toCloseCategoryButtons) {
            _toCloseCategoryButtons = false;
            CloseCategoryButtons();
        }
        SetDraggingPosition();
        base.Draw(spriteBatch);
        if (Main.mouseXButton1 && Main.mouseXButton1Release) {
            if (FolderPath.Count > 1) {
                GotoUpperFolder();
                Main.mouseXButton1Release = false;
            }
        }
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
        for (int i = 0; i < _sortEnumData.Length; ++i) {
            if (_sortEnumData[i] != 0) {
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
            var parent = CurrentFolderNode;
            if (Main.keyState.PressingControl() && node is ModNode mn) {
                node = new ModNode(mn.ModName) {
                    PublishId = mn.PublishId,
                    Favorite = mn.Favorite
                };
            }
            else {
                parent.Children.Remove(node);
            }

            if (node is ModNode modNode) {
                foreach (var child in folder.Children) {
                    if (child is ModNode m && m.ModName == modNode.ModName) {
                        ArrangeGenerate();
                        return;
                    }
                }
            }
            folder.Children.Add(node);
            ArrangeGenerate();
        }
        void MoveNodeBeforeNode(Node node, Node node2) {
            if (node == node2) {
                return;
            }
            var parent = CurrentFolderNode;
            for (int i = 0; i < parent.Children.Count; ++i) {
                if (parent.Children[i] == node2) {
                    if (i > 0 && parent.Children[i - 1] == node) {
                        return;
                    }
                    parent.Children.Remove(node);
                    if (i > 0 && parent.Children[i - 1] == node2) {
                        parent.Children.Insert(i - 1, node);
                    }
                    else {
                        parent.Children.Insert(i, node);
                    }
                    ArrangeGenerate();
                    return;
                }
            }
            MoveNodeToTheStart(node);
        }
        void MoveNodeAfterNode(Node node, Node node2) {
            if (node == node2) {
                return;
            }
            var parent = CurrentFolderNode;
            for (int i = 0; i < parent.Children.Count; ++i) {
                if (parent.Children[i] == node2) {
                    if (i + 1 < parent.Children.Count && parent.Children[i + 1] == node) {
                        return;
                    }
                    parent.Children.Remove(node);
                    if (i > 0 && parent.Children[i - 1] == node2) {
                        parent.Children.Insert(i, node);
                    }
                    else {
                        parent.Children.Insert(i + 1, node);
                    }
                    ArrangeGenerate();
                    return;
                }
            }
            MoveNodeToTheStart(node);
        }
        void MoveNodeToTheStart(Node node) {
            var parent = CurrentFolderNode;
            if (parent.Children.Count != 0 && parent.Children[0] == node) {
                return;
            }
            ArrangeGenerate();
            parent.Children.Remove(node);
            parent.Children.Insert(0, node);
        }
        #endregion
    }
    #endregion

    public override void OnActivate() {
        ArrangeGenerate();
        Main.clrInput();
        list.Clear();
        if (!loaded) {
            ConfigManager.LoadAll(); // Makes sure MP configs are cleared.
            Populate();
        }
    }

    public override void OnDeactivate() {
        if (_cts != null) {
            _cts.Cancel(false);
            _cts.Dispose();
            _cts = null;
        }
        modItemsTask = null;
        SetListViewPositionAfterGenerated(list.ViewPosition);
        FolderDataSystem.Save();
    }
    #region 加载相关
    #region loading
    private Task? modItemsTask;
    private CancellationTokenSource? _cts;
    private bool needToRemoveLoading;
    public bool loading;
    private UILoaderAnimatedImage uiLoader = null!;
    private void Update_TryRemoveLoading() {
        if (needToRemoveLoading) {
            needToRemoveLoading = false;
            RemoveChild(uiLoader);
        }
    }
    private void OnInitialize_Loading() {
        uiLoader = new(1, 1);
        uiLoader.Left.Pixels = -10;
        uiLoader.Top.Pixels = -10;
    }
    #endregion
    #region 异步寻找 Mod
    private bool loaded;
    private void FindModsTask() {
        if (loaded) {
            needToRemoveLoading = true;
            loading = false;
            modItemsTask = null;
            return;
        }
        var mods = ModOrganizer.FindMods(logDuplicates: true);
        Dictionary<string, UIModItemInFolder> tempModItemDict = [];
        foreach (var mod in mods) {
            UIModItemInFolder modItem = new(mod);
            tempModItemDict.Add(modItem.ModName, modItem);
            modItem.Activate();
        }
        ModItemDict = tempModItemDict;

        // TODO: 遍历一遍 Root 来做各种事情

        loaded = true;
        needToRemoveLoading = true;
        ArrangeGenerate();
        loading = false;
        modItemsTask = null;
    }
    public void Populate() {
        if (_cts != null) {
            _cts.Cancel(false);
            _cts.Dispose();
            _cts = null;
        }
        loaded = false;
        loading = true;
        Append(uiLoader);
        _cts = new();
        modItemsTask = Task.Run(FindModsTask, _cts.Token);
    }
    #endregion
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
