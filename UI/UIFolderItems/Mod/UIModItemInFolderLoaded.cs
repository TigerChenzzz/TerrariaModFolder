using Microsoft.Xna.Framework.Input;
using ModFolder.Configs;
using ModFolder.Systems;
using ModFolder.UI.Base;
using ModFolder.UI.Menu;
using ReLogic.Content;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading.Tasks;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader.Config;
using Terraria.ModLoader.Core;
using Terraria.ModLoader.UI;
using Terraria.ModLoader.UI.ModBrowser;
using Terraria.Social.Base;
using Terraria.Social.Steam;
using Terraria.UI;

namespace ModFolder.UI.UIFolderItems.Mod;

/// <summary>
/// 文件夹系统列表中的一个模组 (加载完成)
/// </summary>
public class UIModItemInFolderLoaded(LocalMod localMod) : UIModItemInFolder {
    public override FolderDataSystem.Node? Node => ModNode;
    public FolderDataSystem.ModNode? ModNode { get; set; }
    public LocalMod TheLocalMod => _mod;
    public bool Loaded => _loaded;
    public override DateTime LastModified => _mod.lastModified;
    public override bool Favorite {
        get => FolderDataSystem.Favorites.Contains(ModName);
        set {
            if (Favorite == value) {
                return;
            }
            if (value) {
                FolderDataSystem.Favorites.Add(ModName);
            }
            else {
                FolderDataSystem.Favorites.Remove(ModName);
            }
            FolderDataSystem.DataChanged();
        }
    }
    private ulong? _publishId;
    public ulong PublishId {
        get {
            _publishId ??= WorkshopHelper.GetPublishIdLocal(_mod.modFile, out ulong publishId) ? publishId : (ulong?)0;
            return _publishId.Value;
        }
    }

    #region 子元素
    #region 图标
    private readonly static Asset<Texture2D> DefaultIcon = Main.Assets.Request<Texture2D>("Images/UI/DefaultResourcePackIcon");
    private readonly UIImage _modIcon = new UIImage(DefaultIcon).SettleCommonly();
    private void OnInitialize_ModIcon() => Append(_modIcon);
    private Asset<Texture2D>? smallIcon;
    private Asset<Texture2D>? bigIcon;
    private void SetModIcon() {
        Asset<Texture2D>? icon = LayoutType == LayoutTypes.Stripe ? smallIcon ?? bigIcon : bigIcon;
        if (icon != null) {
            _modIcon.SetImage(icon);
        }
    }
    #endregion
    #region 模组位置标志
    private UIImage? _modLocationIcon;
    private void OnInitialize_ModLocationIcon() {
        if (CommonConfig.Instance.ShowModLocation) {
            // 24x24
            var modLocationIconTexture = _mod.location switch {
                ModLocation.Workshop => TextureAssets.Extra[243],
                ModLocation.Modpack => UICommon.ModLocationModPackIcon,
                ModLocation.Local => UICommon.ModLocationLocalIcon,
                _ => throw new NotImplementedException(),
            };
            _modLocationIcon = new UIImage(modLocationIconTexture) {
                Width = new(24, 0),
                Height = new(24, 0),
            }.SettleCommonly();
            Append(_modLocationIcon);
            mouseOverTooltips.Add((_modLocationIcon, () => Language.GetTextValue("tModLoader.ModFrom" + _mod.location)));
        }
    }
    #endregion
    #region 升级小点
    private UIImageFramed? _updatedModDot;
    private void OnInitialize_UpdatedModDot() {
        var oldModVersionData = ModOrganizer.modsThatUpdatedSinceLastLaunch.FirstOrDefault(x => x.ModName == ModName);
        if (oldModVersionData != default) {
            previousVersionHint = oldModVersionData.previousVersion;
            var toggleImage = Main.Assets.Request<Texture2D>("Images/UI/Settings_Toggle");   // 大小: 30 x 14
            _updatedModDot = new UIImageFramed(toggleImage, toggleImage.Frame(2, 1, 1, 0)) {
                Color = previousVersionHint == null ? Color.Green : new Color(6, 95, 212),
            };
            //_modName.Left.Pixels += 18; // use these 2 for left of the modname

            Append(_updatedModDot);
            mouseOverTooltips.Add((_updatedModDot, () => previousVersionHint == null
                ? Language.GetTextValue("tModLoader.ModAddedSinceLastLaunchMessage")
                : Language.GetTextValue("tModLoader.ModUpdatedSinceLastLaunchMessage", previousVersionHint)));
        }
    }
    #endregion
    #region 未完全卸载
    private UIImage? _modDidNotFullyUnloadWarningImage;
    private void OnInitialize_ModDidNotFullyUnloadWarningImage() {
        // Keep this feature locked to Dev for now until we are sure modders are at fault for this warning.
        // TODO: 测试它的位置
        if (BuildInfo.IsDev && ModCompile.DeveloperMode && ModLoader.IsUnloadedModStillAlive(ModName)) {
            _modDidNotFullyUnloadWarningImage = new(UICommon.ButtonErrorTexture) {
                RemoveFloatingPointsFromDrawPosition = true,
            };
            Append(_modDidNotFullyUnloadWarningImage);
            mouseOverTooltips.Add(_modDidNotFullyUnloadWarningImage, () => Language.GetTextValue("tModLoader.ModDidNotFullyUnloadWarning"));
        }
    }
    #endregion
    #region ModStableOnPreview
    private UIImage? _modStableOnPreviewWarning;
    private void OnInitialize_ModStableOnPreviewWarning() {
        if (ModOrganizer.CheckStableBuildOnPreview(_mod)) {
            _modStableOnPreviewWarning = new UIImage(Main.Assets.Request<Texture2D>(TextureAssets.Item[ItemID.LavaSkull].Name)) {
                RemoveFloatingPointsFromDrawPosition = true,
            };
            Append(_modStableOnPreviewWarning);
            mouseOverTooltips.Add(_modStableOnPreviewWarning, () => Language.GetTextValue("tModLoader.ModStableOnPreviewWarning"));
        }
    }
    #endregion
    #region 更新提示
    [MemberNotNullWhen(true, nameof(_tMLUpdateRequiredInStripeLayout), nameof(_tMLUpdateRequiredInBlockLayout))]
    public bool HasUpdateRequired { get; set; }
    private UIAutoScaleTextTextPanel<string>? _tMLUpdateRequiredInStripeLayout;
    private UIImage? _tMLUpdateRequiredInBlockLayout;
    private int _tMLUpdateRequiredIndex;
    private void OnInitialize_TMLUpdateRequired() {
        // TODO: 美化
        // Don't show the Enable/Disable button if there is no loadable version
        string? updateVersion = null;
        Color updateColor = Color.Orange;

        // Detect if it's for a preview or stable version ahead of our time
        if (BuildInfo.tMLVersion.MajorMinorBuild() < _mod.tModLoaderVersion.MajorMinorBuild()) {
            updateVersion = $"v{_mod.tModLoaderVersion}";

            if (_mod.tModLoaderVersion.Build == 2)
                updateVersion = $"Preview {updateVersion}";
        }

        // Detect if it's for a different browser version entirely
        if (!CheckIfPublishedForThisBrowserVersion(out var modBrowserVersion)) {
            updateVersion = $"{modBrowserVersion} v{_mod.tModLoaderVersion}";
            updateColor = Color.Yellow;
        }

        if (updateVersion == null) {
            return;
        }
        HasUpdateRequired = true;
        _tMLUpdateRequiredInStripeLayout = new UIAutoScaleTextTextPanel<string>(Language.GetTextValue("tModLoader.MBRequiresTMLUpdate", updateVersion)).WithFadedMouseOver(updateColor, updateColor * 0.7f);
        _tMLUpdateRequiredInStripeLayout.BackgroundColor = updateColor * 0.7f;
        _tMLUpdateRequiredInStripeLayout.Width.Pixels = 280;
        _tMLUpdateRequiredInStripeLayout.Height.Pixels = 30;
        _tMLUpdateRequiredInStripeLayout.VAlign = 0.5f;
        _tMLUpdateRequiredInStripeLayout.Left.Pixels = 32;
        _tMLUpdateRequiredInStripeLayout.OnLeftClick += (_, _) => {
            Utils.OpenToURL("https://github.com/tModLoader/tModLoader/wiki/tModLoader-guide-for-players#beta-branches");
        };
        _tMLUpdateRequiredInBlockLayout = new(MTextures.Deprecated) {
            RemoveFloatingPointsFromDrawPosition = true,
            VAlign = 0.5f,
            HAlign = 0.5f,
        };
        _tMLUpdateRequiredInBlockLayout.OnLeftClick += (_, _) => {
            Utils.OpenToURL("https://github.com/tModLoader/tModLoader/wiki/tModLoader-guide-for-players#beta-branches");
        };
        if (BlockLayout) {
            _tMLUpdateRequiredIndex = this.AppendAndGetIndex(_tMLUpdateRequiredInBlockLayout);
        }
        else {
            _tMLUpdateRequiredIndex = this.AppendAndGetIndex(_tMLUpdateRequiredInStripeLayout);
        }
        mouseOverTooltips.Add((_tMLUpdateRequiredInStripeLayout, () => Language.GetTextValue("tModLoader.SwitchVersionInfoButton")));
        mouseOverTooltips.Add((_tMLUpdateRequiredInBlockLayout, () => Language.GetTextValue("tModLoader.SwitchVersionInfoButton")));
    }
    private void SwitchTMLUpdateRequiredToStripeLayout() {
        if (HasUpdateRequired) {
            this.ReplaceChildrenByIndex(_tMLUpdateRequiredIndex, _tMLUpdateRequiredInStripeLayout);
        }
    }
    private void SwitchTMLUpdateRequiedToBlockLayout() {
        if (HasUpdateRequired) {
            this.ReplaceChildrenByIndex(_tMLUpdateRequiredIndex, _tMLUpdateRequiredInBlockLayout);
        }
    }
    #endregion
    #region 右边的按钮
    protected override int RightButtonsLength => 6;
    private UIImageWithVisibility? DeleteButton       { get => rightButtons[0] ; set => rightButtons[0] = value; }
    private UIImageWithVisibility  RenameButton       { get => rightButtons[1]!; set => rightButtons[1] = value; }
    private UIImageWithVisibility  MoreInfoButton     { get => rightButtons[2]!; set => rightButtons[2] = value; }
    private UIImageWithVisibility? ConfigButton       { get => rightButtons[3] ; set => rightButtons[3] = value; }
    private UIImageWithVisibility  ModReferenceIcon   { get => rightButtons[4]!; set => rightButtons[4] = value; }
    private UIImageWithVisibility? TranslationModIcon { get => rightButtons[5] ; set => rightButtons[5] = value; }
    #endregion
    #endregion
    // private bool modFromLocalModFolder;
    
    private readonly LocalMod _mod = localMod;
    private Version? previousVersionHint;
    private bool _configChangesRequireReload;
    /// <summary>
    /// 是否已被加载, 通过 _mod.Enabled 判断是否即将被加载
    /// </summary>
    private bool _loaded;

    public override string ModName => _mod.Name;
    public bool NeedsReload => _mod.properties.side != ModSide.Server && (_mod.Enabled != _loaded || _configChangesRequireReload);

    public override void OnInitialize() {
        base.OnInitialize();
        #region 前置判断
        if (ModLoader.TryGetMod(ModName, out var loadedMod)) {
            _loaded = true;
        }
        #endregion

        OnInitialize_ModIcon(); // 图标
        OnInitialize_ModLocationIcon(); // 模组位置标志
        OnInitialize_UpdatedModDot(); // 升级小点
        OnInitialize_ModDidNotFullyUnloadWarningImage(); // 未完全卸载
        OnInitialize_ModStableOnPreviewWarning(); // stable on preview
        OnInitialize_Name(); // 名字与重命名
        OnInitialize_TMLUpdateRequired(); // TML升级提示

        #region 右边的按钮
        #region 删除
        var deleteButton = DeleteButton = NewRightButton(MTextures.ButtonDelete);
        if (!_loaded && ModOrganizer.CanDeleteFrom(_mod.location)) {
            deleteButton.OnLeftClick += QuickModDelete;
            mouseOverTooltips.Add((deleteButton, () => Language.GetTextValue("UI.Delete")));
        }
        else {
            deleteButton.Visibility = 0.4f;
            if (_loaded) {
                mouseOverTooltips.Add((deleteButton, () => ModFolder.Instance.GetLocalizedValue("UI.Buttons.Delete.Tooltips.CantDeleteEnabled")));
            }
            else {
                mouseOverTooltips.Add((deleteButton, () => ModFolder.Instance.GetLocalizedValue("UI.Buttons.Delete.Tooltips.CantDeleteInModPack")));
            }
        }
        #endregion
        #region 重命名
        var renameButton = RenameButton = NewRightButton(MTextures.ButtonRename);
        _name.AttachRenameButtton(renameButton);
        mouseOverTooltips.Add((renameButton, () => ModFolder.Instance.GetLocalization("UI.Rename").Value));
        #endregion
        #region 更多信息
        var moreInfoButton = MoreInfoButton = NewRightButton(UICommon.ButtonModInfoTexture);
        moreInfoButton.OnLeftClick += ShowMoreInfo;
        mouseOverTooltips.Add((moreInfoButton, () => Language.GetTextValue("tModLoader.ModsMoreInfo")));
        #endregion
        #region 配置按钮
        if (loadedMod != null && ConfigManager.Configs.ContainsKey(loadedMod)) {
            var configButton = ConfigButton = NewRightButton(UICommon.ButtonModConfigTexture);
            configButton.OnLeftClick += OpenConfig;
            mouseOverTooltips.Add((configButton, () => Language.GetTextValue("tModLoader.ModsOpenConfig")));
            // TODO: 在合适的情况下更新此值
            // TODO: 看看这个类中还有没有这种可能会发生变化的值
            if (ConfigManager.ModNeedsReload(loadedMod)) {
                _configChangesRequireReload = true;
            }
        }
        #endregion
        #region 需求与引用
        var modReferenceIcon = ModReferenceIcon = NewRightButton(UICommon.ButtonDepsTexture, 0);
        mouseOverTooltips.Add((modReferenceIcon, () => _modRequiresTooltip));
        #endregion
        #region 翻译
        // if (_mod.properties.RefNames(true).Any() && _mod.properties.translationMod)
        if (_mod.properties.translationMod) {
            TranslationModIcon = NewRightButton(UICommon.ButtonTranslationModTexture);
            mouseOverTooltips.Add((TranslationModIcon, () => {
                string refs = string.Join(", ", _mod.properties.RefNames(true)); // Translation mods can be strong or weak references.
                return Language.GetTextValue("tModLoader.TranslationModTooltip", refs);
            }));
        }
        #endregion
        AppendRightButtonsPanel();
        #endregion

        #region 加载的物品 / NPC / ...
        if (loadedMod != null) {
            _loaded = true;
        }
        // TODO: 这几个标放在哪里 (当鼠标放在更多信息按钮上时? 放在模组名字上时?)
        if (loadedMod != null && false) {
            _loaded = true;
            // TODO: refactor and add nicer icons (and maybe not iterate 6 times)
            int[] values = [
                loadedMod.GetContent<ModItem>().Count(),
                loadedMod.GetContent<ModNPC>().Count(),
                loadedMod.GetContent<ModTile>().Count(),
                loadedMod.GetContent<ModWall>().Count(),
                loadedMod.GetContent<ModBuff>().Count(),
                loadedMod.GetContent<ModMount>().Count()
            ];
            string[] localizationKeys = ["ModsXItems", "ModsXNPCs", "ModsXTiles", "ModsXWalls", "ModsXBuffs", "ModsXMounts"];
            int xOffset = -40;

            for (int i = 0; i < values.Length; i++) {
                if (values[i] > 0) {
                    _modStableOnPreviewWarning = new UIHoverImage(Main.Assets.Request<Texture2D>(TextureAssets.InfoIcon[i].Name), Language.GetTextValue($"tModLoader.{localizationKeys[i]}", values[i])) {
                        Left = { Pixels = xOffset, Percent = 1f },
                        RemoveFloatingPointsFromDrawPosition = true,
                    };

                    Append(_modStableOnPreviewWarning);
                    xOffset -= 18;
                }
            }
        }
        #endregion
        #region 双击左键 启用 / 禁用
        OnLeftDoubleClick += (e, el) => {
            Main.MenuUI.LeftMouse.LastDown = null;
            if (_tMLUpdateRequiredInStripeLayout != null)
                return;
            // TODO: 双击某些位置时不能切换
            ToggleEnabled();
            var menu = UIModFolderMenu.Instance;
            menu.ClearSelectingItems();
            menu.ClearWaitingForSelect();
            menu.CurrentFolderNode.TryRefreshCountsInThisFolder();
        };
        #endregion
        #region alt 左键收藏
        OnLeftClick += (_, _) => {
            if (Main.keyState.IsKeyDown(Keys.LeftAlt) || Main.keyState.IsKeyDown(Keys.RightAlt))
                Favorite = !Favorite;
        };
        #endregion
        #region 服务器版本不同的提示
        //!!!!!!!!!!! TODO: 修改这个
        if (loadedMod != null && _mod.modFile.path != loadedMod.File.path) {
            //!!!!!!!! 本地化和美化 ↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓
            var serverDiffMessage = new UITextPanel<string>($"v{loadedMod.Version} currently loaded due to multiplayer game session or mod update") {
                Width = new(0, 1f),
                Height = new(30, 0f),
                VAlign = .5f,
                BackgroundColor = Color.Orange,
                IgnoresMouseInteraction = true,
                // Top = { Pixels = 82 },
            };
            Append(serverDiffMessage);

            // Height.Pixels = 130;
        }
        #endregion
        generateTask = GenerateAsync();
        ForceRecalculateLayout();
    }

    #region 异步加载
    public bool Generated => generateTask == null;
    Task? generateTask;
    private async Task GenerateAsync() {
        await Task.Yield();
        #region 加载图标
        if (_mod.modFile.HasFile("icon_small.rawimg")) {
            try {
                using (_mod.modFile.Open()) {
                    using var s = _mod.modFile.GetStream("icon_small.rawimg");
                    smallIcon = Main.Assets.CreateUntracked<Texture2D>(s, ".rawimg");
                }
            }
            catch (Exception e) {
                Logging.tML.Error("Unknown error", e);
            }
        }
        if (_mod.modFile.HasFile("icon.png")) {
            try {
                using (_mod.modFile.Open()) {
                    using var s = _mod.modFile.GetStream("icon.png");
                    // if (bigIcon.Width() == 80 && bigIcon.Height() == 80)
                    bigIcon = Main.Assets.CreateUntracked<Texture2D>(s, ".png");
                }
            }
            catch (Exception e) {
                Logging.tML.Error("Unknown error", e);
            }
        }
        SetModIcon();
        /*
        if (smallIcon != null && bigIcon != null) {
            _modIcon.OnMouseOver += (_, _) => _modIcon.SetImage(bigIcon);
            _modIcon.OnMouseOut += (_, _) => _modIcon.SetImage(smallIcon);
        }
        */
        #endregion
        generateTask = null;

        // 依赖关系的加载在 UIModFolderMenu 的 Populate 时通过调用所有模组的 SetModReferences 完成
    }
    #endregion
    #region 引用相关
    /// <summary>
    /// 直接的模组引用
    /// </summary>
    private string[] _modReferences = null!;
    /// <summary>
    /// 递归的模组引用
    /// </summary>
    private string[] _modDependencies = null!;
    /// <summary>
    /// 递归的被引
    /// </summary>
    private string[] _modDependents = null!;
    private string _modRequiresTooltip = null!;
    public void SetModReferences(IEnumerable<LocalMod>? availableMods) {
        // TODO: 弱引用与弱被引: _mod.properties.weakReferences
        _modReferences = [.. _mod.properties.modReferences.Select(x => x.mod)];

        StringBuilder tooltip = new();

        _modDependencies = [.. GetDependencies()];
        if (_modDependencies.Length != 0) {
            StringBuilder dependenciesBuilder = new();
            int i = 0;
        StartOfModDependencies:
            var dependency = _modDependencies[i];
            dependenciesBuilder.Append("- ");
            var uiModItem = UIModFolderMenu.Instance.FindUIModItem(dependency);
            if (uiModItem != null) {
                dependenciesBuilder.Append(uiModItem._mod.DisplayName);
            }
            else {
                dependenciesBuilder.Append(dependency);
                dependenciesBuilder.Append(Language.GetTextValue("tModLoader.ModPackMissing"));
            }
            if (i < _modDependencies.Length - 1) {
                dependenciesBuilder.Append('\n');
                i += 1;
                goto StartOfModDependencies;
            }
            tooltip.Append(Language.GetTextValue("tModLoader.ModDependencyTooltip", dependenciesBuilder.ToString()));
        }

        _modDependents = [.. GetDependents(availableMods)];
        if (_modDependents.Length != 0) {
            if (tooltip.Length != 0)
                tooltip.Append('\n', 2);
            StringBuilder dependentsBuilder = new();
            int i = 0;
        StartOfModDependents:
            var dependent = _modDependents[i];
            dependentsBuilder.Append("- ");
            var uiModItem = UIModFolderMenu.Instance.FindUIModItem(dependent);
            if (uiModItem != null) {
                dependentsBuilder.Append(uiModItem._mod.DisplayName);
            }
            else {
                dependentsBuilder.Append(dependent);
                dependentsBuilder.Append(Language.GetTextValue("tModLoader.ModPackMissing"));
            }
            if (i < _modDependents.Length - 1) {
                dependentsBuilder.Append('\n');
                i += 1;
                goto StartOfModDependents;
            }
            tooltip.Append(Language.GetTextValue("tModLoader.ModDependentsTooltip", dependentsBuilder.ToString()));
        }

        _modRequiresTooltip = tooltip.ToString();
        float visibleToSet = string.IsNullOrWhiteSpace(_modRequiresTooltip) ? 0 : 1;
        if (ModReferenceIcon.Visibility != visibleToSet) {
            ModReferenceIcon.Visibility = visibleToSet;
            SettleRightButtons();
        }
    }
    private HashSet<string> GetDependencies() {
        HashSet<string> result = [];
        GetDependencies(ModName, result);
        return result;
    }
    private static void GetDependencies(string modName, HashSet<string> allDependencies) {
        var modItem = UIModFolderMenu.Instance.FindUIModItem(modName);
        if (modItem == null) {
            return; // Mod isn't downloaded, can't determine further recursive dependencies
        }
        foreach (var dependency in modItem._mod.properties.modReferences.Select(x => x.mod)) {
            if (allDependencies.Add(dependency)) {
                GetDependencies(dependency, allDependencies);
            }
        }
    }
    private HashSet<string> GetDependents(IEnumerable<LocalMod>? availableMods) {
        HashSet<string> result = [];
        GetDependents(ModName, result, availableMods ?? ModOrganizer.RecheckVersionsToLoad());
        return result;
    }
    private static void GetDependents(string modName, HashSet<string> allDependents, IEnumerable<LocalMod> availableMods) {
        var dependents = availableMods
            .Where(m => m.properties.RefNames(includeWeak: false).Any(refName => refName.Equals(modName)))
            .Select(m => m.Name);
        foreach (var dependent in dependents) {
            if (allDependents.Add(dependent))
                GetDependents(dependent, allDependents, availableMods);
        }
    }

    public bool AnyDependentOn() {
        foreach (var dependent in _modDependents) {
            if (ModLoader.EnabledMods.Contains(dependent)) {
                return true;
            }
        }
        return false;
    }

    public bool EnableQuick(HashSet<string> enabled, HashSet<string> missingRefs) {
        if (ModLoader.EnabledMods.Contains(_mod.Name)) {
            return true;
        }
        bool fail = false;
        foreach (var name in _modReferences) {
            var dep = UIModFolderMenu.Instance.FindUIModItem(name);
            if (dep == null) {
                missingRefs.Add(name);
                fail = true;
                continue;
            }
            if (!dep.EnableQuick(enabled, missingRefs)) {
                fail = true;
            }
        }
        if (fail) {
            return false;
        }
        enabled.Add(_mod.Name);
        ModLoader.EnabledMods.Add(_mod.Name);
        return true;
    }
    public void DisableQuick(HashSet<string> disabled, bool disableRedundantDependencies = false) {
        if (!ModLoader.EnabledMods.Remove(_mod.Name)) {
            return;
        }
        disabled.Add(_mod.Name);
        foreach (var name in _modDependents) {
            var dep = UIModFolderMenu.Instance.FindUIModItem(name);
            if (dep == null) {
                continue;
            }
            dep.DisableQuick(disabled, disableRedundantDependencies);
        }
        if (!disableRedundantDependencies) {
            return;
        }
        foreach (var dependency in _modReferences) {
            var dep = UIModFolderMenu.Instance.FindUIModItem(dependency);
            if (dep == null || dep.AnyDependentOn()) {
                continue;
            }
            dep.DisableQuick(disabled, disableRedundantDependencies);
        }
    }
    /// <summary>
    /// 返回最终自己是否是禁用状态
    /// </summary>
    public bool DisableWhenRedundant(HashSet<string> disabled, HashSet<string> toSearch, bool includeDependents) {
        if (!ModLoader.EnabledMods.Contains(ModName)) {
            return true;
        }
        if (_modDependents.Length == 0) {
            return false;
        }
        if (!includeDependents && _modReferences.Length != 0) {
            return false;
        }
        foreach (var dependent in _modDependents) {
            if (!ModLoader.EnabledMods.Contains(dependent)) {
                continue;
            }
            if (!includeDependents) {
                return false;
            }
            if (!toSearch.Contains(dependent)) {
                return false;
            }
            var modItem = UIModFolderMenu.Instance.FindUIModItem(dependent);
            if (modItem == null) {
                ModFolder.Instance.Logger.Error("enabled mods should have a corresponding UIModItem at " + nameof(DisableWhenRedundant));
                return false;
            }
            if (!modItem.DisableWhenRedundant(disabled, toSearch, includeDependents)) {
                return false;
            }
        }
        if (ModLoader.EnabledMods.Remove(ModName)) {
            disabled.Add(ModName);
        }
        return true;
    }

    internal bool TryEnableDependencies() {
        List<string> missingRefs = [];
        List<UIModItemInFolderLoaded> modItems = [];
        foreach (var mod in _modDependencies) {
            var modItem = UIModFolderMenu.Instance.FindUIModItem(mod);
            if (modItem == null) {
                missingRefs.Add(mod);
            }
            else {
                modItems.Add(modItem);
            }
        }
        if (missingRefs.Count != 0) {
            Interface.infoMessage.Show(Language.GetTextValue("tModLoader.ModDependencyModsNotFound", string.Join(", ", missingRefs)), UIModFolderMenu.MyMenuMode);
            return false;
        }
        foreach (var modItem in modItems) {
            modItem.Enable();
        }
        return true;
    }
    [Obsolete("不再使用", true)]
    private void EnableDepsRecursive(List<string> missingRefs) {
        foreach (var name in _modReferences) {
            var dep = UIModFolderMenu.Instance.FindUIModItem(name);
            if (dep == null) {
                missingRefs.Add(name);
                continue;
            }
            dep.EnableDepsRecursive(missingRefs);
            dep.Enable();
        }
    }
    private void DisableDependents() {
        foreach (var name in _modDependents) {
            var dep = UIModFolderMenu.Instance.FindUIModItem(name);
            if (dep == null) {
                continue;
            }
            dep.Disable();
        }
    }
    [Obsolete("不再使用", true)]
    private void DisableDependentsRecursive() {
        foreach (var name in _modDependents) {
            var dep = UIModFolderMenu.Instance.FindUIModItem(name);
            if (dep == null) {
                continue;
            }
            dep.DisableDependentsRecursive();
            dep.Disable();
        }
    }
    #endregion
    #region commented: Generate Language File
    // TODO: "Generate Language File Template" button in upcoming "Miscellaneous Tools" menu.
    /*private void GenerateLangTemplate_OnClick(UIMouseEvent evt, UIElement listeningElement) {
		Mod loadedMod = ModLoader.GetMod(ModName);
		var dictionary = (Dictionary<string, ModTranslation>)loadedMod.translations;
		var result = loadedMod.items.Where(x => !dictionary.ContainsValue(x.Value.DisplayName)).Select(x => x.Value.DisplayName.Key + "=")
			.Concat(loadedMod.items.Where(x => !dictionary.ContainsValue(x.Value.Tooltip)).Select(x => x.Value.Tooltip.Key + "="))
			.Concat(loadedMod.npcs.Where(x => !dictionary.ContainsValue(x.Value.DisplayName)).Select(x => x.Value.DisplayName.Key + "="))
			.Concat(loadedMod.buffs.Where(x => !dictionary.ContainsValue(x.Value.DisplayName)).Select(x => x.Value.DisplayName.Key + "="))
			.Concat(loadedMod.buffs.Where(x => !dictionary.ContainsValue(x.Value.Description)).Select(x => x.Value.Description.Key + "="))
			.Concat(loadedMod.projectiles.Where(x => !dictionary.ContainsValue(x.Value.DisplayName)).Select(x => x.Value.DisplayName.Key + "="));
		//.Concat(loadedMod.tiles.Where(x => !dictionary.ContainsValue(x.Value.)).Select(x => x.Value..Key + "="))
		//.Concat(loadedMod.walls.Where(x => !dictionary.ContainsValue(x.Value.)).Select(x => x.Value..Key + "="));

		result = result.Select(x => x.Remove(0, $"Mods.{ModName}.".Length));

		Platform.Get<IClipboard>().Value = string.Join("\n", result);

		// TODO: ITranslatable or something?
	}*/
    #endregion
    #region Draw
    public override void DrawSelf(SpriteBatch spriteBatch) {
        #region 根据启用状态设置块状图标的透明度
        {
            Color iconColor;
            if (BlockLayout) {
                if (_mod.Enabled) {
                    iconColor = Color.White;
                }
                else {
                    iconColor = Color.White * 0.3f;
                }
            }
            else {
                iconColor = Color.White;
            }
            _modIcon.Color = iconColor;
        }
        #endregion
        base.DrawSelf(spriteBatch);
        var dimensions = GetDimensions();
        var rectangle = dimensions.ToRectangle();
        #region 是否启用
        // TODO: 显示因配置而需要重载的状态?  _configChangesRequireReload
        if (_mod.Enabled && (_loaded || _mod.properties.side == ModSide.Server)) {
            spriteBatch.DrawBox(rectangle, EnabledBorderColor, EnabledInnerColor);
        }
        #endregion
        #region 需要重新加载
        if (_mod.properties.side != ModSide.Server) {
            if (_mod.Enabled && !_loaded) {
                spriteBatch.DrawBox(rectangle, ToEnableBorderColor, ToEnableInnerColor);
            }
            else if (!_mod.Enabled && _loaded) {
                spriteBatch.DrawBox(rectangle, ToDisableBorderColor, ToDisableInnerColor);
            }
            else if (_configChangesRequireReload) {
                spriteBatch.DrawBox(rectangle, ConfigNeedReloadBorderColor, ConfigNeedReloadInnerColor);
            }
        }
        #endregion
        #region 显示 mod 是否为仅服务端 (禁用)
        /*
        if (_mod.properties.side == ModSide.Server) {
            Vector2 drawPos = new(innerDimensions.X + 10f + _modIconAdjust, innerDimensions.Y + 45f);
            drawPos += new Vector2(90f, -2f);
            spriteBatch.Draw(UICommon.ModBrowserIconsTexture.Value, drawPos, new Rectangle(5 * 34, 3 * 34, 32, 32), Color.White, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
            if (new Rectangle((int)drawPos.X, (int)drawPos.Y, 32, 32).Contains(Main.MouseScreen.ToPoint()))
                UICommon.DrawHoverStringInBounds(spriteBatch, Language.GetTextValue("tModLoader.ModIsServerSide"));
        }
        */
        #endregion
        #region 当鼠标在某些东西上时显示些东西
        // 图标
        if (StripeLayout && _modIcon?.IsMouseHovering == true) {
            if (bigIcon != null) {
                UIModFolderMenu.Instance.SetMouseTexture(bigIcon.Value, 160, 160, 8, 8);
            }
        }
        #endregion
    }
    #endregion
    #region 启用与禁用
    public bool TryToggleEnabled() {
        if (_tMLUpdateRequiredInStripeLayout != null)
            return false;
        // TODO: 双击某些位置时不能切换
        ToggleEnabled();
        var menu = UIModFolderMenu.Instance;
        menu.ClearSelectingItems();
        menu.ClearWaitingForSelect();
        menu.CurrentFolderNode.TryRefreshCountsInThisFolder();
        return true;
    }
    private void ToggleEnabled() {
        SoundEngine.PlaySound(SoundID.MenuTick);
        if (!_mod.Enabled) {
            if (!TryEnableDependencies()) {
                return;
            }
        }
        else {
            DisableDependents();
            if (Main.keyState.PressingShift()) {
                DisableRedundantDependencies();
            }
        }
        _mod.Enabled = !_mod.Enabled;
        if (UIModFolderMenu.Instance.EnabledFilterMode != FolderEnabledFilter.All) {
            UIModFolderMenu.Instance.ArrangeGenerate();
        }
    }
    void DisableRedundantDependencies() {
        foreach (var dependency in _modReferences) {
            if (!ModLoader.EnabledMods.Contains(dependency)) {
                continue;
            }
            if (!UIModFolderMenu.Instance.ModItemDict.TryGetValue(dependency, out var dep) || dep.AnyDependentOn()) {
                continue;
            }
            dep.Disable();
            dep.DisableRedundantDependencies();
        }
    }
    public bool IsRedundantDependency() {
        return _modDependents.Length != 0 && !AnyDependentOn();
    }

    internal void Enable() {
        _mod.Enabled = true;
    }

    internal void Disable() {
        _mod.Enabled = false;
    }
    #endregion

    internal void ShowMoreInfo(UIMouseEvent evt, UIElement listeningElement) {
        SoundEngine.PlaySound(SoundID.MenuOpen);
        Interface.modInfo.Show(ModName, _mod.DisplayName, UIModFolderMenu.MyMenuMode, _mod, _mod.properties.description, _mod.properties.homepage);
    }

    internal void OpenConfig(UIMouseEvent evt, UIElement listeningElement) {
        SoundEngine.PlaySound(SoundID.MenuOpen);
        Interface.modConfigList.ModToSelectOnOpen = ModLoader.GetMod(ModName);
        Main.menuMode = Interface.modConfigListID;
        UIModFolderMenu.IsPreviousUIStateOfConfigList = true;
    }

    #region DisplayName 相关
    public override string ModDisplayName => _mod.DisplayName;
    protected override string GetDisplayName() {
        var name = Alias ?? ModDisplayName;
        if (CommonConfig.Instance.ShowModVersion) {
            return $"{name} v{_mod.modFile.Version}";
        }
        return name;
    }
    protected override Func<string> GetNameMouseOverTooltipFunc() {
        if (string.IsNullOrEmpty(_mod.properties.author)) {
            return () => HasAlias ? $"{Alias} ({ModDisplayName})" : ModDisplayName;
        }
        return () => HasAlias
            ? $"{Alias} ({ModDisplayName})\n{Language.GetTextValue("tModLoader.ModsByline", _mod.properties.author)}"
            : $"{ModDisplayName}\n{Language.GetTextValue("tModLoader.ModsByline", _mod.properties.author)}";
    }
    #endregion
    #region PassFilters
    public override PassFilterResults PassFiltersInner() {
        var filter = UIModFolderMenu.Instance.Filter;
        if (filter.Length > 0) {
            if (UIModFolderMenu.Instance.searchFilterMode == SearchFilter.Author) {
                if (_mod.properties.author.Contains(filter, StringComparison.OrdinalIgnoreCase)) {
                    goto NameFilterPassed;
                }
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
        if (UIModFolderMenu.Instance.LoadedFilterMode == ModLoadedFilter.Unloaded) {
            return PassFilterResults.FilteredByLoaded;
        }
        if (UIModFolderMenu.Instance.ModSideFilterMode != ModSideFilter.All) {
            if ((int)_mod.properties.side != (int)UIModFolderMenu.Instance.ModSideFilterMode - 1) {
                return PassFilterResults.FilteredByModSide;
            }
        }
        var passed = UIModFolderMenu.Instance.EnabledFilterMode switch {
            FolderEnabledFilter.All => true,
            FolderEnabledFilter.Enabled => Loaded,
            FolderEnabledFilter.Disabled => !Loaded,
            FolderEnabledFilter.ToBeEnabled => !Loaded && _mod.Enabled,
            FolderEnabledFilter.ToBeDisabled => Loaded && !_mod.Enabled,
            FolderEnabledFilter.ToToggle => Loaded ^ _mod.Enabled,
            FolderEnabledFilter.WouldBeEnabled => _mod.Enabled,
            FolderEnabledFilter.WouldBeDisabled => !_mod.Enabled,
            _ => false,
        };
        return passed ? PassFilterResults.NotFiltered : PassFilterResults.FilteredByEnabled;
    }
    #endregion
    #region 删除
    private void QuickModDelete(UIMouseEvent evt, UIElement listeningElement) {
        // TODO: 二次确认时选择删除索引还是取消订阅
        // TODO: shift 和 ctrl 控制删除索引和取消订阅 (在二次确认面板中提示这个操作)
        // TODO: 同时按住 shift 和 ctrl 则既删除索引同时取消订阅 (只按住 ctrl 只是取消订阅但不删除索引 (方便重新下载))
        // TODO: 他们的提示
        bool shiftPressed = Main.keyState.PressingShift();

        if (Main.keyState.PressingShift() || Main.keyState.PressingControl()) {
            DeleteMod(evt, listeningElement);
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
        UIModFolderMenu.Instance.Append(_deleteModDialog);
        UIModFolderMenu.Instance.AppendConfirmPanel(_deleteModDialog);

        #region 按钮是
        var _dialogYesButton = new UIAutoScaleTextTextPanel<string>(Language.GetTextValue("LegacyMenu.105")) {
            TextColor = Color.White,
            Width = new StyleDimension(-10f, 1f / 3f),
            Height = { Pixels = 40 },
            VAlign = .85f,
            HAlign = .15f
        }.WithFadedMouseOver();
        _dialogYesButton.OnUpdate += _ => {
            if (UIModFolderMenu.Instance.ShowAllMods || Main.keyState.PressingControl() || Main.keyState.PressingShift()) {
                _dialogYesButton.SetText(Language.GetTextValue("LegacyMenu.104"));
            }
            else {
                _dialogYesButton.SetText(Language.GetTextValue("LegacyMenu.105"));
            }
        };
        _dialogYesButton.OnLeftClick += DeleteMod;
        _deleteModDialog.Append(_dialogYesButton);
        #endregion
        #region 按钮否
        var _dialogNoButton = new UIAutoScaleTextTextPanel<string>(Language.GetTextValue("LegacyMenu.105")) {
            TextColor = Color.White,
            Width = new StyleDimension(-10f, 1f / 3f),
            Height = { Pixels = 40 },
            VAlign = .85f,
            HAlign = .85f
        }.WithFadedMouseOver();
        _dialogNoButton.OnLeftClick += (_, _) => UIModFolderMenu.Instance.RemoveConfirmPanel();
        _deleteModDialog.Append(_dialogNoButton);
        #endregion

        string tip = Language.GetTextValue("tModLoader.DeleteModConfirm");
        if (UIModFolderMenu.Instance.ShowFolderSystem) {
            tip = string.Join('\n', tip, ModFolder.Instance.GetLocalization("UI.DeleteModItemCofirmTextToAdd"));
        }
        var _dialogText = new UIText(tip) {
            Width = { Percent = .85f },
            HAlign = .5f,
            VAlign = .3f,
            IsWrapped = true,
        };
        _deleteModDialog.Append(_dialogText);

        UIModFolderMenu.Instance.Recalculate();
    }

    private void DeleteMod(UIMouseEvent evt, UIElement listeningElement) {
        if (UIModFolderMenu.Instance.ShowAllMods || Main.keyState.PressingControl()) {
            UIModFolderMenu.Instance.ArrangeDeleteMod(this);
        }
        // TODO: 在显示全部界面不可以删除索引的提示
        if (UIModFolderMenu.Instance.ShowFolderSystem && Main.keyState.PressingShift() && ModNode != null) {
            ModNode.Parent = null;
        }
        UIModFolderMenu.Instance.RemoveConfirmPanel();
    }
    #endregion
    private bool CheckIfPublishedForThisBrowserVersion(out string recommendedModBrowserVersion) {
        recommendedModBrowserVersion = SocialBrowserModule.GetBrowserVersionNumber(_mod.tModLoaderVersion);
        return recommendedModBrowserVersion == SocialBrowserModule.GetBrowserVersionNumber(BuildInfo.tMLVersion);
    }
    #region 布局 Layout
    protected override void RecalculateStripeLayout() {
        base.RecalculateStripeLayout();
        float leftOffset = 32;
        #region icon
        SetModIcon();
        _modIcon.Left.Set(1, 0);
        _modIcon.Top.Set(1, 0);
        _modIcon.Width.Set(30, 0);
        _modIcon.Height.Set(30, 0);
        #endregion
        #region ModLocationIcon
        if (_modLocationIcon != null) {
            leftOffset += 2;
            _modLocationIcon.Left = new(leftOffset, 0);
            _modLocationIcon.Top = new(0, 0);
            _modLocationIcon.VAlign = 0.5f;
            leftOffset += _modLocationIcon.Width.Pixels;
        }
        #endregion
        #region UpdateDot
        if (_updatedModDot != null) {
            leftOffset += 2;
            _updatedModDot.Left = new(leftOffset, 0);
            _updatedModDot.Top = new();
            _updatedModDot.VAlign = 0.5f;
            leftOffset += _updatedModDot.Width.Pixels;
        }
        #endregion
        #region 未完全卸载
        if (_modDidNotFullyUnloadWarningImage != null) {
            leftOffset += 2;
            _modDidNotFullyUnloadWarningImage.Left = new(leftOffset, 0);
            _modDidNotFullyUnloadWarningImage.Top = new();
            _modDidNotFullyUnloadWarningImage.VAlign = 0.5f;
            leftOffset += _modDidNotFullyUnloadWarningImage.Width.Pixels;
        }
        #endregion
        #region StableOnPreview
        if (_modStableOnPreviewWarning != null) {
            leftOffset += 2;
            _modStableOnPreviewWarning.Left = new(leftOffset, 0);
            _modStableOnPreviewWarning.Top = new();
            _modStableOnPreviewWarning.VAlign = 0.5f;
            leftOffset += _modStableOnPreviewWarning.Width.Pixels;
        }
        #endregion
        #region 右边的按钮
        var rightOffset = SetRightButtonsPanelToStripeLayout();
        #endregion
        #region 名字和重命名输入框
        if (leftOffset == 32) {
            leftOffset += 5;
        }
        else {
            leftOffset += 2;
        }
        _name.Left = new(leftOffset, 0);
        _name.Width = new(-leftOffset + rightOffset, 1);
        _name.Height = new(0, 1);
        _name.VAlign = 0;
        SetNameToNormal();
        #endregion
        #region UpdateRequired
        SwitchTMLUpdateRequiredToStripeLayout();
        #endregion
    }
    protected override void RecalculateBlockLayout() {
        base.RecalculateBlockLayout();
        float topLeftOffset = 0;
        #region icon
        SetModIcon();
        _modIcon.Left.Set(-40, 0.5f);
        _modIcon.Top.Set(-40, 0.5f);
        _modIcon.Width.Set(80, 0);
        _modIcon.Height.Set(80, 0);
        #endregion
        #region ModLocationIcon
        if (_modLocationIcon != null) {
            topLeftOffset += 2;
            _modLocationIcon.Left = new(topLeftOffset, 0);
            _modLocationIcon.Top = new(2, 0);
            _modLocationIcon.VAlign = 0;
            topLeftOffset += _modLocationIcon.Width.Pixels;
        }
        #endregion
        #region UpdateDot
        if (_updatedModDot != null) {
            topLeftOffset += 2;
            _updatedModDot.Left = new(topLeftOffset, 0);
            _updatedModDot.Top = new(14 - _updatedModDot.Height.Pixels / 2, 0);
            _updatedModDot.VAlign = 0;
            topLeftOffset += _updatedModDot.Width.Pixels;
        }
        #endregion
        #region 未完全卸载
        if (_modDidNotFullyUnloadWarningImage != null) {
            topLeftOffset += 2;
            _modDidNotFullyUnloadWarningImage.Left = new(topLeftOffset, 0);
            _modDidNotFullyUnloadWarningImage.Top = new(14 - _modDidNotFullyUnloadWarningImage.Height.Pixels / 2, 0);
            _modDidNotFullyUnloadWarningImage.VAlign = 0;
            topLeftOffset += _modDidNotFullyUnloadWarningImage.Width.Pixels;
        }
        #endregion
        #region StableOnPreview
        if (_modStableOnPreviewWarning != null) {
            topLeftOffset += 2;
            _modStableOnPreviewWarning.Left = new(topLeftOffset, 0);
            _modStableOnPreviewWarning.Top = new(14 - _modStableOnPreviewWarning.Height.Pixels / 2, 0);
            _modStableOnPreviewWarning.VAlign = 0;
            // topLeftOffset += _keyImage.Width.Pixels;
        }
        #endregion
        #region 右边的按钮
        SetRightButtonsPanelToBlockLayout();
        #endregion
        #region 名字和重命名输入框
        _name.Left = new(2, 0);
        _name.Width = new(-4, 1);
        _name.Height = new(StripeHeight, 0);
        _name.VAlign = 1;
        SetNameToPlaceHolder();
        #endregion
        #region UpdateRequired
        SwitchTMLUpdateRequiedToBlockLayout();
        #endregion
    }
    protected override void RecalculateBlockWithNameLayout() {
        base.RecalculateBlockWithNameLayout();
        float topLeftOffset = 0;
        #region icon
        SetModIcon();
        _modIcon.Left.Set(-40, 0.5f);
        _modIcon.Top.Set((BlockHeight - 80) / 2, 0);
        _modIcon.Width.Set(80, 0);
        _modIcon.Height.Set(80, 0);
        #endregion
        #region ModLocationIcon
        if (_modLocationIcon != null) {
            topLeftOffset += 2;
            _modLocationIcon.Left = new(topLeftOffset, 0);
            _modLocationIcon.Top = new(2, 0);
            _modLocationIcon.VAlign = 0;
            topLeftOffset += _modLocationIcon.Width.Pixels;
        }
        #endregion
        #region UpdateDot
        if (_updatedModDot != null) {
            topLeftOffset += 2;
            _updatedModDot.Left = new(topLeftOffset, 0);
            _updatedModDot.Top = new(14 - _updatedModDot.Height.Pixels / 2, 0);
            _updatedModDot.VAlign = 0;
            topLeftOffset += _updatedModDot.Width.Pixels;
        }
        #endregion
        #region 未完全卸载
        if (_modDidNotFullyUnloadWarningImage != null) {
            topLeftOffset += 2;
            _modDidNotFullyUnloadWarningImage.Left = new(topLeftOffset, 0);
            _modDidNotFullyUnloadWarningImage.Top = new(14 - _modDidNotFullyUnloadWarningImage.Height.Pixels / 2, 0);
            _modDidNotFullyUnloadWarningImage.VAlign = 0;
            topLeftOffset += _modDidNotFullyUnloadWarningImage.Width.Pixels;
        }
        #endregion
        #region StableOnPreview
        if (_modStableOnPreviewWarning != null) {
            topLeftOffset += 2;
            _modStableOnPreviewWarning.Left = new(topLeftOffset, 0);
            _modStableOnPreviewWarning.Top = new(14 - _modStableOnPreviewWarning.Height.Pixels / 2, 0);
            _modStableOnPreviewWarning.VAlign = 0;
            // topLeftOffset += _keyImage.Width.Pixels;
        }
        #endregion
        #region 右边的按钮
        SetRightButtonsPanelToBlockWithNameLayout();
        #endregion
        #region 名字和重命名输入框
        _name.Left = new(2, 0);
        _name.Width = new(-4, 1);
        _name.Height = new(StripeHeight, 0);
        _name.VAlign = 1;
        SetNameToNormal();
        #endregion
        #region UpdateRequired
        SwitchTMLUpdateRequiedToBlockLayout();
        #endregion
    }
    #endregion
}
