using Microsoft.Xna.Framework.Input;
using ModFolder.Configs;
using ModFolder.Systems;
using ReLogic.Content;
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

namespace ModFolder.UI;

/// <summary>
/// 文件夹系统列表中的一个模组
/// </summary>
public class UIModItemInFolder : UIFolderItem {
    public override FolderDataSystem.Node? Node => ModNode;
    public FolderDataSystem.ModNode? ModNode { get; set; }
    public LocalMod TheLocalMod => _mod;
    public bool Loaded => _loaded;
    public override string NameToSort => _mod.DisplayNameClean;
    public override DateTime LastModified => _mod.lastModified;
    public override bool Favorite {
        get => ModNode?.Favorite ?? false;
        set {
            bool changed = false;
            if (ModNode == null) {
                ModNode = new(_mod) {
                    Parent = UIModFolderMenu.Instance.CurrentFolderNode
                };
                changed = true;
            }
            if (ModNode.Favorite != value) {
                ModNode.Favorite = value;
                changed = true;
            }
            if (changed) {
                FolderDataSystem.TrySaveWhenChanged();
            }
        }
    }
    private ulong? _publishId;
    public ulong PublishId {
        get {
            _publishId ??= WorkshopHelper.GetPublishIdLocal(_mod.modFile, out ulong publishId) ? publishId : (ulong?)0;
            return _publishId.Value;
        }
    }

    private UIImage _modIcon = null!;
    private UIImageFramed? updatedModDot;
    private Version? previousVersionHint;
    private UIHoverImage? _keyImage;
    private UIHoverImage? _modDidNotFullyUnloadWarningImage;
    private UIText _modName = null!;
    internal UIAutoScaleTextTextPanel<string>? tMLUpdateRequired;
    private readonly LocalMod _mod;
    #region 右边的按钮
    private readonly UIImageWithVisible?[] rightButtons = new UIImageWithVisible[5];
    private UIImageWithVisible? DeleteButton       { get => rightButtons[0] ; set => rightButtons[0] = value; }
    private UIImageWithVisible  MoreInfoButton     { get => rightButtons[1]!; set => rightButtons[1] = value; }
    private UIImageWithVisible? ConfigButton       { get => rightButtons[2] ; set => rightButtons[2] = value; }
    private UIImageWithVisible  ModReferenceIcon   { get => rightButtons[3]!; set => rightButtons[3] = value; }
    private UIImageWithVisible? TranslationModIcon { get => rightButtons[4] ; set => rightButtons[4] = value; }
    #endregion
    // private bool modFromLocalModFolder;

    private bool _configChangesRequireReload;
    private bool _loaded;
    private string? _tooltip;
    public readonly string DisplayNameClean; // No chat tags: for search and sort functionality.

    public string ModName => _mod.Name;
    public bool NeedsReload => _mod.properties.side != ModSide.Server && (_mod.Enabled != _loaded || _configChangesRequireReload);

    private Asset<Texture2D>? hoverIcon;

    public UIModItemInFolder(LocalMod mod) {
        _mod = mod;
        DisplayNameClean = _mod.DisplayNameClean;
    }

    public override void OnInitialize() {
        base.OnInitialize();

        #region 图标
        float leftOffset = 30;
        _modIcon = new(Main.Assets.Request<Texture2D>("Images/UI/DefaultResourcePackIcon")) {
            Left = { Pixels = 1 },
            Top = { Pixels = 1 },
            Width = { Pixels = 28 },
            Height = { Pixels = 28 },
            ScaleToFit = true,
            AllowResizingDimensions = false,
            RemoveFloatingPointsFromDrawPosition = true,
        };
        Append(_modIcon);
        #endregion
        #region 未完全卸载
        // Keep this feature locked to Dev for now until we are sure modders are at fault for this warning.
        // TODO: 测试它的位置
        if (BuildInfo.IsDev && ModCompile.DeveloperMode && ModLoader.IsUnloadedModStillAlive(ModName)) {
            leftOffset += 2;
            _modDidNotFullyUnloadWarningImage = new(UICommon.ButtonErrorTexture, Language.GetTextValue("tModLoader.ModDidNotFullyUnloadWarning")) {
                Left = { Pixels = leftOffset },
                VAlign = 0.5f,
                RemoveFloatingPointsFromDrawPosition = true,
            };
            leftOffset += _modDidNotFullyUnloadWarningImage.Width.Pixels;
            Append(_modDidNotFullyUnloadWarningImage);
        }
        #endregion
        #region ModStableOnPreviewWarning
        if (ModOrganizer.CheckStableBuildOnPreview(_mod)) {
            leftOffset += 2;
            _keyImage = new UIHoverImage(Main.Assets.Request<Texture2D>(TextureAssets.Item[ItemID.LavaSkull].Name), Language.GetTextValue("tModLoader.ModStableOnPreviewWarning")) {
                Left = { Pixels = leftOffset },
                VAlign = 0.5f,
                UseTooltipMouseText = true,
                RemoveFloatingPointsFromDrawPosition = true,
            };
            leftOffset += _keyImage.Width.Pixels;
            Append(_keyImage);
        }
        #endregion
        #region 名字
        if (leftOffset == 30) {
            leftOffset += 5;
        }
        else {
            leftOffset += 2;
        }
        // TODO: 名字太长怎么办 (UIHorizontalList?)
        string text = _mod.DisplayName + " v" + _mod.modFile.Version;
        _modName = new(text);
        _modName.Left.Pixels = leftOffset;
        _modName.Top.Pixels = 7;
        leftOffset += _modName.MinWidth.Pixels;
        Append(_modName);
        #endregion
        #region 模组位置标志
        if (CommonConfig.Instance.ShowModLocation) {
            // 24x24
		    var modLocationIconTexture = _mod.location switch {
			    ModLocation.Workshop => TextureAssets.Extra[243],
			    ModLocation.Modpack => UICommon.ModLocationModPackIcon,
			    ModLocation.Local => UICommon.ModLocationLocalIcon,
			    _ => throw new NotImplementedException(),
		    };
            leftOffset += 2;
		    var modLocationIcon = new UIHoverImage(modLocationIconTexture, Language.GetTextValue("tModLoader.ModFrom" + _mod.location)) {
			    RemoveFloatingPointsFromDrawPosition = true,
			    UseTooltipMouseText = true,
			    Left = { Pixels = leftOffset },
                VAlign = 0.5f,
		    };
            leftOffset += modLocationIcon.Width.Pixels;
		    Append(modLocationIcon);
        }
        #endregion
        #region 已升级小点
        var oldModVersionData = ModOrganizer.modsThatUpdatedSinceLastLaunch.FirstOrDefault(x => x.ModName == ModName);
        if (oldModVersionData != default) {
            previousVersionHint = oldModVersionData.previousVersion;
            var toggleImage = Main.Assets.Request<Texture2D>("Images/UI/Settings_Toggle");   // 大小: 30 x 14
            leftOffset += 8;
            updatedModDot = new UIImageFramed(toggleImage, toggleImage.Frame(2, 1, 1, 0)) {
                Left = { Pixels = leftOffset, Percent = 0f },
                Top = { Pixels = 8, Percent = 0f },
                Color = previousVersionHint == null ? Color.Green : new Color(6, 95, 212),
            };
            //_modName.Left.Pixels += 18; // use these 2 for left of the modname

            Append(updatedModDot);
        }
        #endregion
        #region 升级版本提示
        // TODO: 美化
        // Don't show the Enable/Disable button if there is no loadable version
        string? updateVersion = null;
        string updateURL = "https://github.com/tModLoader/tModLoader/wiki/tModLoader-guide-for-players#beta-branches";
        Color updateColor = Color.Orange;

        // Detect if it's for a preview version ahead of our time
        if (BuildInfo.tMLVersion.MajorMinorBuild() < _mod.tModLoaderVersion.MajorMinorBuild()) {
            updateVersion = $"v{_mod.tModLoaderVersion}";

            if (_mod.tModLoaderVersion.MajorMinor() > BuildInfo.stableVersion)
                updateVersion = $"Preview {updateVersion}";
        }

        // Detect if it's for a different browser version entirely
        if (!CheckIfPublishedForThisBrowserVersion(out var modBrowserVersion)) {
            updateVersion = $"{modBrowserVersion} v{_mod.tModLoaderVersion}";
            updateColor = Color.Yellow;
        }

        // Hide the Enabled button if it's not for this built version
        if (updateVersion != null) {
            tMLUpdateRequired = new UIAutoScaleTextTextPanel<string>(Language.GetTextValue("tModLoader.MBRequiresTMLUpdate", updateVersion)).WithFadedMouseOver(updateColor, updateColor * 0.7f);
            tMLUpdateRequired.BackgroundColor = updateColor * 0.7f;
            tMLUpdateRequired.Width.Pixels = 280;
            tMLUpdateRequired.Height.Pixels = 30;
            tMLUpdateRequired.OnLeftClick += (a, b) => {
                Utils.OpenToURL(updateURL);
            };
            Append(tMLUpdateRequired);
        }
        else {
            // Append(_uiModStateCheckBoxHitbox);
        }
        #endregion

        #region 右边的按钮
        #region 删除
        if (!_loaded && ModOrganizer.CanDeleteFrom(_mod.location)) {
            DeleteButton = new(Textures.ButtonDelete) {
                Width = { Pixels = 24 },
                Height = { Pixels = 24 },
                Top = { Pixels = -12, Percent = 0.5f },
                ScaleToFit = true,
                AllowResizingDimensions = false,
                RemoveFloatingPointsFromDrawPosition = true,
            };
            DeleteButton.OnLeftClick += QuickModDelete;
            Append(DeleteButton);
        }
        #endregion
        #region 更多信息
        MoreInfoButton = new(UICommon.ButtonModInfoTexture) {
            Width = { Pixels = 24 },
            Height = { Pixels = 24 },
            Top = { Pixels = -12, Percent = 0.5f },
            ScaleToFit = true,
            AllowResizingDimensions = false,
            RemoveFloatingPointsFromDrawPosition = true,
        };
        MoreInfoButton.OnLeftClick += ShowMoreInfo;
        Append(MoreInfoButton);
        #endregion
        #region 配置按钮
        if (ModLoader.TryGetMod(ModName, out var loadedMod) && ConfigManager.Configs.ContainsKey(loadedMod)) {
            ConfigButton = new(UICommon.ButtonModConfigTexture) {
                Width = { Pixels = 24 },
                Height = { Pixels = 24 },
                Top = { Pixels = -12, Percent = 0.5f },
                ScaleToFit = true,
                AllowResizingDimensions = false,
                RemoveFloatingPointsFromDrawPosition = true,
            };
            ConfigButton.OnLeftClick += OpenConfig;
            Append(ConfigButton);
            // TODO: 在合适的情况下更新此值
            // TODO: 看看这个类中还有没有这种可能会发生变化的值
            if (ConfigManager.ModNeedsReload(loadedMod)) {
                _configChangesRequireReload = true;
            }
        }
        #endregion
        #region 需求与引用
        ModReferenceIcon = new(UICommon.ButtonDepsTexture) {
            Width = new(24, 0),
            Height = new(24, 0),
            Top = new(-12, 0.5f),
            ScaleToFit = true,
            AllowResizingDimensions = false,
            RemoveFloatingPointsFromDrawPosition = true,
            Visible = false,
        };
        Append(ModReferenceIcon);
        #endregion
        #region 翻译
        // if (_mod.properties.RefNames(true).Any() && _mod.properties.translationMod)
        if (_mod.properties.translationMod) {
            var icon = UICommon.ButtonTranslationModTexture;
            TranslationModIcon = new(icon) {
                Width = new(24, 0),
                Height = new(24, 0),
                Top = new(-12, 0.5f),
                ScaleToFit = true,
                AllowResizingDimensions = false,
                RemoveFloatingPointsFromDrawPosition = true,
            };
            Append(TranslationModIcon);
        }
        #endregion
        SettleRightButtons();
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
                    _keyImage = new UIHoverImage(Main.Assets.Request<Texture2D>(TextureAssets.InfoIcon[i].Name), Language.GetTextValue($"tModLoader.{localizationKeys[i]}", values[i])) {
                        Left = { Pixels = xOffset, Percent = 1f },
                        RemoveFloatingPointsFromDrawPosition = true,
                    };

                    Append(_keyImage);
                    xOffset -= 18;
                }
            }
        }
        #endregion
        #region 双击左键 启用 / 禁用
        OnLeftDoubleClick += (e, el) => {
            if (tMLUpdateRequired != null)
                return;
            // TODO: 双击某些位置时不能切换
            ToggleEnabled();
        };
        #endregion
        #region alt 左键收藏
        OnLeftClick += (_, _) => {
            if (Main.keyState.IsKeyDown(Keys.LeftAlt) || Main.keyState.IsKeyDown(Keys.RightAlt))
                Favorite = !Favorite;
        };
        #endregion
        #region 服务器版本不同的提示
        // TODO: 修改这个
        if (loadedMod != null && _mod.modFile.path != loadedMod.File.path) {
            var serverDiffMessage = new UITextPanel<string>($"v{loadedMod.Version} currently loaded due to multiplayer game session")
            {
                Left = new StyleDimension(0, 0f),
                Width = new StyleDimension(0, 1f),
                Height = new StyleDimension(30, 0f),
                BackgroundColor = Color.Orange,
                // Top = { Pixels = 82 },
            };
            Append(serverDiffMessage);

            // Height.Pixels = 130;
        }
        #endregion
        generateTask = GenerateAsync();
    }

    private void SettleRightButtons() {
        // TODO: 配置右边这堆按钮是否向右缩紧
        bool leanToTheRight = true;
        int rightOffset = -6;
        for (int i = 0; i < rightButtons.Length; ++i) {
            var button = rightButtons[i];
            if (button != null && button.Visible) {
                rightOffset -= 24;
                button.Left = new(rightOffset, 1);
            }
            else if (!leanToTheRight) {
                rightOffset -= 24;
            }
        }
        RecalculateChildren();
    }

    #region 异步加载
    public bool Generated => generateTask == null;
    Task? generateTask;
    private async Task GenerateAsync() {
        await Task.Yield();
        #region 加载图标
        Asset<Texture2D>? smallIcon = null;
        Asset<Texture2D>? bigIcon = null;
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
                    hoverIcon = bigIcon;
                }
            }
            catch (Exception e) {
                Logging.tML.Error("Unknown error", e);
            }
        }
        Asset<Texture2D>? modIcon = smallIcon ?? bigIcon;
        if (modIcon != null) {
            _modIcon.SetImage(modIcon);
        }
        /*
        if (smallIcon != null && bigIcon != null) {
            _modIcon.OnMouseOver += (_, _) => _modIcon.SetImage(bigIcon);
            _modIcon.OnMouseOut += (_, _) => _modIcon.SetImage(smallIcon);
        }
        */
        #endregion
        generateTask = null;
    }
    #endregion
    #region 引用相关
    private string[] _modReferences = null!;
    private string[] _modDependencies = null!; // Note: 递归的
    private string[] _modDependents = null!; // Note: 递归的
    private string _modRequiresTooltip = null!;
    public void SetModReferences(IEnumerable<LocalMod>? availableMods) {
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
        bool visibleToSet = !string.IsNullOrWhiteSpace(_modRequiresTooltip);
        if (ModReferenceIcon.Visible != visibleToSet) {
            ModReferenceIcon.Visible = visibleToSet;
            SettleRightButtons();
        }
    }
    public void SetDependents(IEnumerable<LocalMod> availableMods) {
        _modDependents = [.. GetDependents(availableMods)];

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

    public void EnableQuick(HashSet<string> enabled, HashSet<string> missingRefs) {
        if (!ModLoader.EnabledMods.Add(_mod.Name)) {
            return;
        }
        enabled.Add(_mod.Name);
        foreach (var name in _modReferences) {
            var dep = UIModFolderMenu.Instance.FindUIModItem(name);
            if (dep == null) {
                missingRefs.Add(name);
                continue;
            }
            dep.EnableQuick(enabled, missingRefs);
        }
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

    internal void EnableDependencies() {
        var missingRefs = new List<string>();
        EnableDepsRecursive(missingRefs);

        if (missingRefs.Count != 0) {
            Interface.infoMessage.Show(Language.GetTextValue("tModLoader.ModDependencyModsNotFound", string.Join(", ", missingRefs)), UIModFolderMenu.MyMenuMode);
        }
    }
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
        DisableDependentsRecursive();
    }
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
        // 更多信息按钮
        if (MoreInfoButton?.IsMouseHovering == true) {
            _tooltip = Language.GetTextValue("tModLoader.ModsMoreInfo");
        }
        // 删除按钮
        else if (DeleteButton?.IsMouseHovering == true) {
            _tooltip = Language.GetTextValue("UI.Delete");
        }
        // 模组名
        else if (_modName?.IsMouseHovering == true && _mod.properties.author.Length > 0) {
            _tooltip = Language.GetTextValue("tModLoader.ModsByline", _mod.properties.author);
        }
        // 配置
        else if (ConfigButton?.IsMouseHovering == true) {
            _tooltip = Language.GetTextValue("tModLoader.ModsOpenConfig");
        }
        // 已升级小点
        else if (updatedModDot?.IsMouseHovering == true) {
            if (previousVersionHint == null)
                _tooltip = Language.GetTextValue("tModLoader.ModAddedSinceLastLaunchMessage");
            else
                _tooltip = Language.GetTextValue("tModLoader.ModUpdatedSinceLastLaunchMessage", previousVersionHint);
        }
        // 需升级
        else if (tMLUpdateRequired?.IsMouseHovering == true) {
            _tooltip = Language.GetTextValue("tModLoader.SwitchVersionInfoButton");
        }
        // 引用
        else if (ModReferenceIcon?.IsMouseHovering == true) {
            _tooltip = _modRequiresTooltip;
        }
        // 翻译
        else if (TranslationModIcon?.IsMouseHovering == true) {
            string refs = string.Join(", ", _mod.properties.RefNames(true)); // Translation mods can be strong or weak references.
            _tooltip = Language.GetTextValue("tModLoader.TranslationModTooltip", refs);
        }
        // 图标
        else if (_modIcon?.IsMouseHovering == true) {
            if (hoverIcon != null) {
                UIModFolderMenu.Instance.SetMouseTexture(hoverIcon.Value, 160, 160, 8, 8);
            }
        }
        #endregion
    }

    private void ToggleEnabled() {
        SoundEngine.PlaySound(SoundID.MenuTick);
        _mod.Enabled = !_mod.Enabled;
        if (UIModFolderMenu.Instance.EnabledFilterMode != FolderEnabledFilter.All) {
            UIModFolderMenu.Instance.ArrangeGenerate();
        }

        if (!_mod.Enabled) {
            DisableDependents();
            if (Main.keyState.PressingShift()) {
                DisableRedundantDependencies();
            }
            return;
        }

        EnableDependencies();
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

    public override int PassFiltersInner() {
        var filter = UIModFolderMenu.Instance.Filter;
        if (filter.Length > 0) {
            if (UIModFolderMenu.Instance.searchFilterMode == SearchFilter.Author) {
                if (!_mod.properties.author.Contains(filter, StringComparison.OrdinalIgnoreCase)) {
                    return 1;
                }
            }
            else if (!DisplayNameClean.Contains(filter, StringComparison.OrdinalIgnoreCase) && !ModName.Contains(filter, StringComparison.OrdinalIgnoreCase)) {
                return 1;
            }
        }
        if (UIModFolderMenu.Instance.ModSideFilterMode != ModSideFilter.All) {
            if ((int)_mod.properties.side != (int)UIModFolderMenu.Instance.ModSideFilterMode - 1) {
                return 2;
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
        return passed ? 0 : 3;
    }

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
            UIModFolderMenu.Instance.ArrangeGenerate();
            FolderDataSystem.TrySaveWhenChanged();
        }
        UIModFolderMenu.Instance.RemoveConfirmPanel();
    }
    #endregion
    private bool CheckIfPublishedForThisBrowserVersion(out string recommendedModBrowserVersion) {
        recommendedModBrowserVersion = SocialBrowserModule.GetBrowserVersionNumber(_mod.tModLoaderVersion);
        return recommendedModBrowserVersion == SocialBrowserModule.GetBrowserVersionNumber(BuildInfo.tMLVersion);
    }
}
