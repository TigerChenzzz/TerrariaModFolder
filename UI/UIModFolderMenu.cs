using Microsoft.Xna.Framework.Input;
using ModFolder.Systems;
using System.Collections;
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
    #region 字段与属性
    public static UIModFolderMenu Instance { get; private set; } = new();
    public int Timer { get; private set; }
    public static void TotallyReload() {
        Instance = new();
    }
    public static void EnterFrom(UIWorkshopHub hub) {
        Instance.PreviousUIState = hub;
        Main.MenuUI.SetState(Instance);
        Instance.SetListViewPositionAfterUpdateNeeded(0);
    }
    public const int MyMenuMode = 47133;
    #region 拖动相关
    UIFolderItem? _draggingTo;
    public UIFolderItem? DraggingTo {
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
    private UIImagePro refreshButton = null!;
    private UIFolderItemList list = null!;
    private UIInputTextField filterTextBox = null!;
    public UICycleImage SearchFilterToggle = null!;
    internal readonly List<UICycleImage> _categoryButtons = [];
    private UIAutoScaleTextTextPanel<LocalizedText> ButtonAllMods { get => buttons[0]; set => buttons[0] = value; }
    private UIAutoScaleTextTextPanel<LocalizedText> ButtonRM { get => buttons[1]; set => buttons[1] = value; }
    private UIAutoScaleTextTextPanel<LocalizedText> ButtonOMF { get => buttons[2]; set => buttons[2] = value; }
    private UIAutoScaleTextTextPanel<LocalizedText> ButtonCL { get => buttons[3]; set => buttons[3] = value; }
    private UIAutoScaleTextTextPanel<LocalizedText> ButtonCreateFolder { get => buttons[4]; set => buttons[4] = value; }
    private UIAutoScaleTextTextPanel<LocalizedText> ButtonB { get => buttons[5]; set => buttons[5] = value; }
    private readonly UIAutoScaleTextTextPanel<LocalizedText>[] buttons = new UIAutoScaleTextTextPanel<LocalizedText>[6];
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
    private float? _listViewPositionToSetAfterUpdateNeeded;
    private void SetListViewPositionAfterUpdateNeeded(float value) => _listViewPositionToSetAfterUpdateNeeded = value;

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
    public UIFolderItem? DraggingTarget { get; set; }
    #endregion
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
        uIPanel = new UIPanel {
            Width = { Percent = 1f },
            Height = { Pixels = -110, Percent = 1f }, // -110
            BackgroundColor = UICommon.MainPanelBackground,
            PaddingTop = 0f,
            HAlign = 0.5f,
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
        folderPathList.Width.Set(-40, 1);
        folderPathList.Left.Set(5, 0);
        folderPathList.SetPadding(1);
        folderPathList.ListPadding = 2;
        folderPathList.OnDraw += sb => {
            sb.DrawBox(folderPathList.GetDimensions().ToRectangle(), Color.Black * 0.6f, UICommon.DefaultUIBlue * 0.2f);
        };
        uIPanel.Append(folderPathList);
        #endregion
        #region 刷新按钮
        refreshButton = new(Textures.UI("Refresh")) {
            Width = { Pixels = 30 },
            Height = { Pixels = 30 },
            Left = { Pixels = -35, Precent = 1 },
            Top = { Pixels = upperPixels },
            ScaleToFit = true,
            AllowResizingDimensions = false,
        };
        upperPixels += 30;
        bool refreshButtonPressed = false;
        refreshButton.OnLeftClick += (_, _) => {
            refreshButtonPressed = false;
            Refresh();
        };
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
        uIPanel.Append(refreshButton);
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
        // TODO: 点按这个滚条会产生一个偏移的 bug
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
        #region 启用与禁用按钮
        ButtonAllMods = new(ModFolder.Instance.GetLocalization("UI.Menu.Buttons.AllMods.DisplayName"));
        ButtonAllMods.OnLeftClick += EnableMods;
        ButtonAllMods.OnRightClick += DisableMods;
        ButtonAllMods.OnMiddleClick += ResetMods;
        #endregion
        #region 重新加载按钮
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
        ButtonCreateFolder = new(ModFolder.Instance.GetLocalization("UI.Menu.Buttons.CreateFolder.DisplayName"));
        ButtonCreateFolder.OnLeftClick += (_, _) => {
            SoundEngine.PlaySound(SoundID.MenuTick);
            FolderNode node = new(ModFolder.Instance.GetLocalization("UI.Menu.NewFolderDefaultName").Value);
            CurrentFolderNode.Children.Insert(0, node);
            nodeToRename = node;
            list.ViewPosition = 0;
            updateNeeded = true;
        };
        #endregion
        #region 返回按钮
        // TODO: 在它之前添加一个重置模组启用状态的按钮 (使用直接操纵 ModLoader.EnabledMods 的方式修改, 最后再 ModOrganizer.SaveEnabledMods(), 还需要日志打印)
        ButtonB = new UIAutoScaleTextTextPanel<LocalizedText>(Language.GetText("UI.Back"));
        ButtonB.OnLeftClick += (_, _) => {
            if (!Main.keyState.PressingShift() && FolderPath.Count > 1) {
                GotoUpperFolder();
                return;
            }
            FolderDataSystem.Save();
            FolderPath.Clear();
            list.ViewPosition = 0;
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
        #region 右键松开时尝试移动位置
        OnRightMouseUp += (e, target) => {
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
            bool oldWay = false;
            if (oldWay) {
                #region 搜索目标元素
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
                    MoveNodeToTheStart(node);
                    return;
                }
                #endregion
                #region 将 UI 类转为 Node
                if (aim is UIFolder folder) {
                    if (Main.mouseY < aim._dimensions.Y + aim._dimensions.Height / 5) {
                        if (folder.Name == "..") {
                            MoveNodeToTheStart(node);
                        }
                        else if (folder.FolderNode != null) {
                            MoveNodeBeforeNode(node, folder.FolderNode);
                        }
                        else {
                            // 理应不该发生这种情况
                            return;
                        }
                    }
                    else if (Main.mouseY > aim._dimensions.Y + aim._dimensions.Height * 4 / 5) {
                        if (folder.Name == "..") {
                            MoveNodeToTheStart(node);
                        }
                        else if (folder.FolderNode != null) {
                            MoveNodeAfterNode(node, folder.FolderNode);
                        }
                        else {
                            // 理应不该发生这种情况
                        }
                    }
                    else {
                        if (folder.Name == "..") {
                            // 保险起见判断一下, 理应总是符合该条件的
                            if (FolderPath.Count > 1)
                                MoveNodeIntoFolder(node, FolderPath[^2]);
                        
                        }
                        else if (folder.FolderNode != null) {
                            MoveNodeIntoFolder(node, folder.FolderNode);
                        }
                        else {
                            // 理应不该发生这种情况
                        }
                    }
                    return;
                }
                Node? aimNode = aim.Node;
                if (aimNode == null) {
                    return;
                }
                if (Main.mouseY < aim._dimensions.Y + aim._dimensions.Height / 2) {
                    MoveNodeBeforeNode(node, aimNode);
                }
                else {
                    MoveNodeAfterNode(node, aimNode);
                }
                #endregion
            }
            else {
                if (_draggingTo == null) {
                    return;
                }
                Node? aimNode = _draggingTo.Node;
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
            }
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
                            updateNeeded = true;
                            return;
                        }
                    }
                }
                folder.Children.Add(node);
                updateNeeded = true;
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
                        updateNeeded = true;
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
                        updateNeeded = true;
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
                updateNeeded = true;
                parent.Children.Remove(node);
                parent.Children.Insert(0, node);
            }
        };
        #endregion
        uIPanel.Append(upperMenuContainer);
        Append(uIElement);
    }
    private FolderNode? nodeToRename;
    
    // 只启用或禁用本文件夹下的模组, 按住 shift 时才是所有模组
    // 按住 alt 同时包含子文件夹
    // 按住 ctrl 在禁用时同时禁用收藏
    // 使用悬浮文字以提示这些操作
    private IEnumerable<UIModItemInFolder> GetAffectedMods(bool tryIgnoreFavorite = false) {
        IEnumerable<UIModItemInFolder> result;
        if (Main.keyState.PressingShift()) {
            result = ModItemDict.Values;
        }
        if (!Main.keyState.IsKeyDown(Keys.LeftAlt) && !Main.keyState.IsKeyDown(Keys.RightAlt)) {
            result = list._items.Select(i => i as UIModItemInFolder).WhereNotNull();
        }
        if (CurrentFolderNode == FolderDataSystem.Root) {
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
    private void Refresh() {
        SoundEngine.PlaySound(SoundID.MenuTick);
        if (!loaded && modItemsTask != null) {
            return;
        }
        loading = true;
        Append(uiLoader);
        Repopulate();
    }

    public static bool IsPreviousUIStateOfConfigList { get; set; }

    private void ClearSearchField(UIMouseEvent evt, UIElement listeningElement) => filterTextBox.Text = "";

    public UIModItemInFolder? FindUIModItem(string modName) {
        return ModItemDict.Values.SingleOrDefault(m => m.ModName == modName);
    }

    public override void Update(GameTime gameTime) {
        Timer += 1;
        Update_RemoveChildrenToRemove();
        base.Update(gameTime);
        #region 当加载完成时做一些事情
        if (modItemsTask is { IsCompleted: true, IsFaulted: true }) {
            // TODO: 检查报错
            ;
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
                if (ModItemDict.TryGetValue(m.ModName, out var uiMod)) {
                    if (uiMod.PassFilters(filterResults)) {
                        uiMod.ModNode = m;
                        return uiMod;
                    }
                    else {
                        return null;
                    }
                }
                else {
                    return new UIModItemInFolderUnloaded(m);
                }
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
        #region 在根目录下时将文件夹树未包含的 Mod 全部放进来
        if (CurrentFolderNode == FolderDataSystem.Root) {
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
                    visibleItems.Add(value);
                }
            }
        }
        #endregion
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
        #region 若不在根目录, 则添加一个返回上级的文件夹
        if (CurrentFolderNode != FolderDataSystem.Root) {
            UIFolder upperFolder = new("..");
            list.Add(upperFolder);
            upperFolder.RightDraggable = false;
            upperFolder.Activate();
        }
        #endregion
        list.AddRange(visibleItems);
        foreach (var item in visibleItems) {
            item?.Activate();
        }
        Recalculate();
        if (_listViewPositionToSetAfterUpdateNeeded != null) {
            list.ViewPosition = _listViewPositionToSetAfterUpdateNeeded.Value;
            _listViewPositionToSetAfterUpdateNeeded = null;
        }
    }

    public override void Draw(SpriteBatch spriteBatch) {
        UILinkPointNavigator.Shortcuts.BackButtonCommand = 7;
        SetDraggingPosition();
        base.Draw(spriteBatch);
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
        #region 当鼠标在一些东西上时显示悬浮提示
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
        if (ButtonAllMods.IsMouseHovering)
            UICommon.TooltipMouseText(ModFolder.Instance.GetLocalization("UI.Menu.Buttons.AllMods.Tooltip").Value);
        else if (ButtonOMF.IsMouseHovering)
            UICommon.TooltipMouseText(Language.GetTextValue("tModLoader.ModsOpenModsFoldersTooltip"));
        #endregion
    }
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
        #region 搜索目标元素
        // TODO: 在间隙和有指向时明确显示
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
        if (aim is UIFolder folder) {
            DraggingTo = folder;
            if (Main.mouseY < aim._dimensions.Y + aim._dimensions.Height / 5) {
                DraggingDirection = -1;
            }
            else if (Main.mouseY > aim._dimensions.Y + aim._dimensions.Height * 4 / 5) {
                DraggingDirection = 1;
            }
            else {
                DraggingDirection = 0;
            }
            return;
        }
        DraggingTo = aim;
        if (Main.mouseY < aim._dimensions.Y + aim._dimensions.Height / 2) {
            DraggingDirection = -1;
        }
        else {
            DraggingDirection = 1;
        }
    }

    public override void OnActivate() {
        updateNeeded = true;
        Main.clrInput();
        list.Clear();
        if (!loaded) {
            loading = true;
            Append(uiLoader);
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
        SetListViewPositionAfterUpdateNeeded(list.ViewPosition);
        FolderDataSystem.Save();
    }
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
        updateNeeded = true;
        loading = false;
        modItemsTask = null;
    }
    public void Populate() {
        _cts = new();
        modItemsTask = Task.Run(FindModsTask, _cts.Token);
        Task.Run(FindModsTask, _cts.Token);
    }
    public void Repopulate() {
        if (_cts != null) {
            _cts.Cancel(false);
            _cts.Dispose();
            _cts = null;
        }
        _cts = new();
        loaded = false;
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
