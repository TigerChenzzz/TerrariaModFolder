using Microsoft.Xna.Framework.Input;
using ModFolder.Systems;
using ReLogic.Content;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader.Config;
using Terraria.ModLoader.Core;
using Terraria.ModLoader.UI;
using Terraria.ModLoader.UI.ModBrowser;
using Terraria.Social.Base;
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
            if (ModNode != null) {
                ModNode.Favorite = value;
            }
            if (!value) {
                return;
            }
            ModNode = new(_mod) {
                Favorite = true
            };
            UIModFolderMenu.Instance.CurrentFolderNode.Children.Add(ModNode);
        }
    }

    private const float PADDING = 5f;
    private UIImage _moreInfoButton = null!;
    private UIImage _modIcon = null!;
    private UIImageFramed? updatedModDot;
    private Version? previousVersionHint;
    private UIHoverImage? _keyImage;
    private UIHoverImage? _modDidNotFullyUnloadWarningImage;
    private UIImage? _configButton;
    private UIText _modName = null!;
    internal UIAutoScaleTextTextPanel<string>? tMLUpdateRequired;
    private UIImage? _modReferenceIcon;
    private UIImage? _translationModIcon;
    private UIImage? _deleteModButton;
    private readonly LocalMod _mod;
    // private bool modFromLocalModFolder;

    private bool _configChangesRequireReload;
    private bool _loaded;
    private string? _tooltip;
    private string[] _modReferences = null!;
    private string[] _modDependents = null!; // Note: Recursive
    private string[] _modDependencies = null!; // Note: Recursive
    private string _modRequiresTooltip = null!;
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
        Asset<Texture2D>? modIcon = null;
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
        modIcon = smallIcon ?? bigIcon ?? Main.Assets.Request<Texture2D>("Images/UI/DefaultResourcePackIcon");
        _modIcon = new UIImage(modIcon) {
            Left = { Pixels = 1 },
            Top = { Pixels = 1 },
            Width = { Pixels = 28 },
            Height = { Pixels = 28 },
            ScaleToFit = true,
            AllowResizingDimensions = false,
        };
        float leftOffset = 30 + PADDING;
        /*
        if (smallIcon != null && bigIcon != null) {
            _modIcon.OnMouseOver += (_, _) => _modIcon.SetImage(bigIcon);
            _modIcon.OnMouseOut += (_, _) => _modIcon.SetImage(smallIcon);
        }
        */
        Append(_modIcon);
        #endregion
        #region 未完全卸载
        // Keep this feature locked to Dev for now until we are sure modders are at fault for this warning.
        // TODO: 测试它的位置
        if (BuildInfo.IsDev && ModCompile.DeveloperMode && ModLoader.IsUnloadedModStillAlive(ModName)) {
            _modDidNotFullyUnloadWarningImage = new(UICommon.ButtonErrorTexture, Language.GetTextValue("tModLoader.ModDidNotFullyUnloadWarning")) {
                Left = { Pixels = leftOffset },
                VAlign = 0.5f,
            };
            leftOffset += _modDidNotFullyUnloadWarningImage.Width.Pixels + PADDING;
            Append(_modDidNotFullyUnloadWarningImage);
        }
        #endregion
        #region ModStableOnPreviewWarning
        if (ModOrganizer.CheckStableBuildOnPreview(_mod)) {
            _keyImage = new UIHoverImage(Main.Assets.Request<Texture2D>(TextureAssets.Item[ItemID.LavaSkull].Name), Language.GetTextValue("tModLoader.ModStableOnPreviewWarning")) {
                Left = { Pixels = leftOffset },
                VAlign = 0.5f,
            };
            leftOffset += _keyImage.Width.Pixels + PADDING;
            Append(_keyImage);
        }
        #endregion
        #region 名字
        // TODO: 名字太长怎么办 (UIHorizontalList?)
        string text = _mod.DisplayName + " v" + _mod.modFile.Version;
        _modName = new(text);
        _modName.Left.Pixels = leftOffset;
        _modName.Top.Pixels = 7;
        Append(_modName);
        #endregion
        #region 已升级小点
        var oldModVersionData = ModOrganizer.modsThatUpdatedSinceLastLaunch.FirstOrDefault(x => x.ModName == ModName);
        if (oldModVersionData != default) {
            previousVersionHint = oldModVersionData.previousVersion;
            var toggleImage = Main.Assets.Request<Texture2D>("Images/UI/Settings_Toggle");   // 大小: 30 x 14
            updatedModDot = new UIImageFramed(toggleImage, toggleImage.Frame(2, 1, 1, 0)) {
                Left = { Pixels = _modName.GetInnerDimensions().ToRectangle().Right + 8 /* _modIconAdjust*/, Percent = 0f },
                Top = { Pixels = 8, Percent = 0f },
                Color = previousVersionHint == null ? Color.Green : new Color(6, 95, 212)
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
        #region 删除
        // TODO: 配置右边这堆按钮是否向右缩紧 
        bool leanToTheRight = true;
        int bottomRightRowOffset = -30;
        if (!_loaded && ModOrganizer.CanDeleteFrom(_mod.location)) {
            _deleteModButton = new UIImage(Textures.ButtonDelete) {
                Width = { Pixels = 24 },
                Height = { Pixels = 24 },
                Left = { Pixels = bottomRightRowOffset, Precent = 1 },
                Top = { Pixels = -12, Percent = 0.5f },
                ScaleToFit = true,
            };
            _deleteModButton.OnLeftClick += QuickModDelete;
            Append(_deleteModButton);
        }
        else if (leanToTheRight) {
            bottomRightRowOffset += 24;
        }
        #endregion
        #region 更多信息
        bottomRightRowOffset -= 24;
        _moreInfoButton = new UIImage(UICommon.ButtonModInfoTexture) {
            Width = { Pixels = 24 },
            Height = { Pixels = 24 },
            Left = { Pixels = bottomRightRowOffset, Percent = 1 },
            Top = { Pixels = -12, Percent = 0.5f },
            ScaleToFit = true,
        };
        _moreInfoButton.OnLeftClick += ShowMoreInfo;
        Append(_moreInfoButton);
        #endregion
        #region 配置按钮
        bottomRightRowOffset -= 24;
        if (ModLoader.TryGetMod(ModName, out var loadedMod) && ConfigManager.Configs.ContainsKey(loadedMod)) {
            _configButton = new UIImage(UICommon.ButtonModConfigTexture) {
                Width = { Pixels = 24 },
                Height = { Pixels = 24 },
                Left = { Pixels = bottomRightRowOffset, Percent = 1f },
                Top = { Pixels = -12, Percent = 0.5f },
                ScaleToFit = true,
            };
            _configButton.OnLeftClick += OpenConfig;
            Append(_configButton);
            // TODO: 在合适的情况下更新此值
            // TODO: 看看这个类中还有没有这种可能会发生变化的值
            if (ConfigManager.ModNeedsReload(loadedMod)) {
                _configChangesRequireReload = true;
            }
        }
        else if (leanToTheRight) {
            bottomRightRowOffset += 24;
        }
        #endregion
        #region 需求与引用
        // TODO: 显示引用了它的目前启用了的模组的数量
        _modReferences = _mod.properties.modReferences.Select(x => x.mod).ToArray();

        var availableMods = ModOrganizer.RecheckVersionsToLoad();
        _modRequiresTooltip = "";
        HashSet<string> allDependencies = [];
        GetDependencies(_mod.Name, allDependencies);
        _modDependencies = [.. allDependencies];
        if (_modDependencies.Length != 0) {
            string refs = string.Join("\n", _modDependencies.Select(x => "- " + (UIModFolderMenu.Instance.FindUIModItem(x)?._mod.DisplayName ?? x + Language.GetTextValue("tModLoader.ModPackMissing"))));
            _modRequiresTooltip += Language.GetTextValue("tModLoader.ModDependencyTooltip", refs);
        }
        void GetDependencies(string modName, HashSet<string> allDependencies) {
            var modItem = UIModFolderMenu.Instance.FindUIModItem(modName);
            if (modItem == null)
                return; // Mod isn't downloaded, can't determine further recursive dependencies

            var dependencies = modItem._mod.properties.modReferences.Select(x => x.mod).ToArray();
            foreach (var dependency in dependencies) {
                if (allDependencies.Add(dependency)) {
                    GetDependencies(dependency, allDependencies);
                }
            }
        }

        HashSet<string> allDependents = [];
        GetDependents(_mod.Name, allDependents);
        _modDependents = [.. allDependents];
        if (_modDependents.Length != 0) {
            if (!string.IsNullOrWhiteSpace(_modRequiresTooltip))
                _modRequiresTooltip += "\n\n";
            string refs = string.Join("\n", _modDependents.Select(x => "- " + (UIModFolderMenu.Instance.FindUIModItem(x)?._mod.DisplayName ?? x + Language.GetTextValue("tModLoader.ModPackMissing"))));
            _modRequiresTooltip += Language.GetTextValue("tModLoader.ModDependentsTooltip", refs);
        }
        void GetDependents(string modName, HashSet<string> allDependents) {
            var dependents = availableMods
                .Where(m => m.properties.RefNames(includeWeak: false).Any(refName => refName.Equals(modName)))
                .Select(m => m.Name).ToArray();
            foreach (var dependent in dependents) {
                if (allDependents.Add(dependent))
                    GetDependents(dependent, allDependents);
            }
        }

        bottomRightRowOffset -= 24;
        if (!string.IsNullOrWhiteSpace(_modRequiresTooltip)) {
            var icon = UICommon.ButtonDepsTexture;
            _modReferenceIcon = new UIImage(icon) {
                Width = new(24, 0),
                Height = new(24, 0),
                Left = new(bottomRightRowOffset, 1),
                Top = new(-12, 0.5f),
                ScaleToFit = true,
            };
            // _modReferenceIcon.OnLeftClick += EnableDependencies;

            Append(_modReferenceIcon);
        }
        else if (leanToTheRight) {
            bottomRightRowOffset += 24;
        }
        #endregion
        #region 翻译
        bottomRightRowOffset -= 24;
        if (_mod.properties.RefNames(true).Any() && _mod.properties.translationMod) {
            var icon = UICommon.ButtonTranslationModTexture;
            _translationModIcon = new UIImage(icon) {
                Width = new(24, 0),
                Height = new(24, 0),
                Left = new(bottomRightRowOffset, 1),
                Top = new(-12, 0.5f),
                ScaleToFit = true,
            };
            Append(_translationModIcon);
        }
        else if (leanToTheRight) {
            bottomRightRowOffset += 24;
        }
        #endregion
        #region Steam 标志
        // TODO:  Steam 标志和整合包标志放在何处
        if (_mod.location == ModLocation.Workshop && false) {
            var steamIcon = new UIImage(TextureAssets.Extra[243])
            {
                Left = { Pixels = -22, Percent = 1f }
            };
            Append(steamIcon);
        }
        #endregion
        #region 整合包标志
        else if (_mod.location == ModLocation.Modpack) {
            var modpackIcon = new UIImage(UICommon.ModLocationModPackIcon)
            {
                Left = { Pixels = -22, Percent = 1f }
            };
            Append(modpackIcon);
        }
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
                        Left = { Pixels = xOffset, Percent = 1f }
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
                // Top = { Pixels = 82 }
            };
            Append(serverDiffMessage);

            // Height.Pixels = 130;
        }
        #endregion
    }

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
            spriteBatch.DrawBox(rectangle, Color.White * 0.6f, Color.White * 0.2f);
        }
        #endregion
        #region 需要重新加载
        // TODO: 调色   现在的绿色貌似不是很显眼
        if (_mod.properties.side != ModSide.Server) {
            if (_mod.Enabled && !_loaded) {
                spriteBatch.DrawBox(rectangle, new Color(0f, 1f, 0f) * 0.6f, new Color(0f, 1f, 0f) * 0.15f);
            }
            else if (!_mod.Enabled && _loaded) {
                spriteBatch.DrawBox(rectangle, Color.Red * 0.6f, Color.Red * 0.15f);
            }
            else if (_configChangesRequireReload) {
                // TODO: 和收藏的颜色冲突了
                spriteBatch.DrawBox(rectangle, Color.Yellow * 0.6f, Color.Yellow * 0.2f);
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
        if (_moreInfoButton?.IsMouseHovering == true) {
            _tooltip = Language.GetTextValue("tModLoader.ModsMoreInfo");
        }
        // 删除按钮
        else if (_deleteModButton?.IsMouseHovering == true) {
            _tooltip = Language.GetTextValue("UI.Delete");
        }
        // 模组名
        else if (_modName?.IsMouseHovering == true && _mod.properties.author.Length > 0) {
            _tooltip = Language.GetTextValue("tModLoader.ModsByline", _mod.properties.author);
        }
        // 配置
        else if (_configButton?.IsMouseHovering == true) {
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
        else if (_modReferenceIcon?.IsMouseHovering == true) {
            _tooltip = _modRequiresTooltip;
        }
        // 翻译
        else if (_translationModIcon?.IsMouseHovering == true) {
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
            return;
        }

        EnableDependencies();
    }

    internal void Enable() {
        if (_mod.Enabled) { return; }
        _mod.Enabled = true;
    }

    internal void Disable() {
        if (!_mod.Enabled) { return; }
        _mod.Enabled = false;
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
            if (UIModFolderMenu.Instance.ShowType == MenuShowType.AllMods || Main.keyState.PressingControl() || Main.keyState.PressingShift()) {
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
        if (UIModFolderMenu.Instance.ShowType == MenuShowType.FolderSystem) {
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
        if (UIModFolderMenu.Instance.ShowType == MenuShowType.AllMods || Main.keyState.PressingControl()) {
            ModOrganizer.DeleteMod(_mod);
            UIModFolderMenu.Instance.ModItemDict.Remove(ModName);
            UIModFolderMenu.Instance.ArrangeGenerate();
        }
        if (UIModFolderMenu.Instance.ShowType == MenuShowType.FolderSystem && Main.keyState.PressingShift() && ModNode != null) {
            UIModFolderMenu.Instance.CurrentFolderNode.Children.Remove(ModNode);
            UIModFolderMenu.Instance.ArrangeGenerate();
        }
        UIModFolderMenu.Instance.RemoveConfirmPanel();
    }

    private bool CheckIfPublishedForThisBrowserVersion(out string recommendedModBrowserVersion) {
        recommendedModBrowserVersion = SocialBrowserModule.GetBrowserVersionNumber(_mod.tModLoaderVersion);
        return recommendedModBrowserVersion == SocialBrowserModule.GetBrowserVersionNumber(BuildInfo.tMLVersion);
    }
}
