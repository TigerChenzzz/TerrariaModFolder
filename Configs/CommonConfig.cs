using ModFolder.UI;
using MonoMod.Cil;
using Newtonsoft.Json;
using ReLogic.Content;
using ReLogic.Graphics;
using System.ComponentModel;
using System.Reflection;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader.Config;
using Terraria.ModLoader.Config.UI;
using Terraria.ModLoader.Core;
using Terraria.ModLoader.UI;
using Terraria.UI;
using Terraria.UI.Chat;

namespace ModFolder.Configs;

public class CommonConfig : ModConfig {
    public static CommonConfig Instance { get; set; } = null!;
    public override ConfigScope Mode => ConfigScope.ClientSide;

    public bool LeftClickToEnterFolderSystem { get; set; }
    #region 显示模组来源
    [DefaultValue(true)]
    public bool ShowModLocation { get; set; }
    #endregion
    #region 更经常的保存
    [DefaultValue(true)]
    public bool SaveWhenChanged { get; set; }
    #endregion
    #region 删除文件夹需要二次确认
    [DefaultValue(true)]
    public bool AlwaysNeedConfirmWhenDeletingFolder { get; set; }
    #endregion
    #region 在拖动时自动移动列表
    [DefaultValue(true)]
    public bool AutoMoveListWhenDragging { get; set; }
    #endregion
    #region 数据保存位置
    [CustomModConfigItem(typeof(PathConfigElement))]
    [DefaultValue("")]
    public string? DataSavePath { get; set; } = string.Empty;
    public class PathConfigElement : ConfigElement<string> {
        public override void OnBind() {
            base.OnBind();
            Height.Set(60, 0);
            UIClickableImage openFolderButton = new(UICommon.ButtonOpenFolder){
                Width = { Pixels = 22 },
                Height = { Pixels = 22 },
                Top = { Pixels = 4 },
                Left = { Pixels = -4 },
                HAlign = 1,
            };
            Append(openFolderButton);
            UIPanel uIPanel = new(){
                Top = { Pixels = 30 },
                Left = { Pixels = 10 },
                Width = { Pixels = -20, Percent = 1 },
                Height = { Pixels = 30 },
            };
            uIPanel.SetPadding(0f);
            Append(uIPanel);
            UIFocusInputTextField uIInputTextField = new(Language.GetTextValue("tModLoader.ModConfigTypeHere")){
                Top = { Pixels = 5 },
                Left = { Pixels = 10 },
                Width = { Pixels = -20, Percent = 1 },
                Height = { Pixels = 20 },
            };
            uIInputTextField.SetText(Value);
            uIInputTextField.OnTextChange += delegate {
                Value = uIInputTextField.CurrentString;
            };
            uIInputTextField.OnRightClick += (_, _) => {
                uIInputTextField.SetText(string.Empty);
            };
            openFolderButton.OnLeftClick += (_, _) => {
                var folderPath = OpenExplorerToGetFolderPath(ModOrganizer.modPath);
                if (!string.IsNullOrEmpty(folderPath)) {
                    uIInputTextField.SetText(folderPath);
                }
            };
            uIPanel.Append(uIInputTextField);
            var originTooltipFunction = TooltipFunction;
            TooltipFunction = delegate {
                if (openFolderButton.IsMouseHovering) {
                    return ModFolder.Instance.GetLocalization("UI.ConfigButtons.OpenFolder.Tooltip").Value;
                }
                if (uIPanel.IsMouseHovering) {
                    return Value;
                }
                return originTooltipFunction();
            };
        }
        public class UIClickableImage : UIImage {
            public UIClickableImage(Asset<Texture2D> texture) : base(texture) {
                ScaleToFit = true;
                AllowResizingDimensions = false;
                RemoveFloatingPointsFromDrawPosition = true;
            }
            public override void DrawSelf(SpriteBatch spriteBatch) {
                Color = IsMouseHovering ? Color.White : Color.Silver;
                base.DrawSelf(spriteBatch);
            }
        }
    }
    #endregion
    #region 显示文件夹内的启用状态
    [DefaultValue(true)]
    public bool ShowEnableStatusBackground { get; set; }
    public ShowEnableStatusTextClass ShowEnableStatusText { get; set; } = new();
    public class ObjectElementFixed() : ObjectElement(false) {
        private static bool hooked;
        public override void OnBind() {
            TryHook();
            base.OnBind();
        }
        private void TryHook() {
            if (hooked) {
                return;
            }
            hooked = true;
            MonoModHooks.Modify(typeof(ObjectElement).GetMethod(nameof(SetupList), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance), FixSetupList);
        }
        private static void FixSetupList(ILContext il) {
            ILCursor cursor = new(il);
            if (!cursor.TryGotoNext(MoveType.AfterLabel, i => i.MatchCall(typeof(Attribute), nameof(Attribute.IsDefined)))) {
                return;
            }
            cursor.Remove();
            cursor.EmitDelegate((MemberInfo member, Type type) => Attribute.IsDefined(member, type) && !Attribute.IsDefined(member, typeof(ShowDespiteJsonIgnoreAttribute)));
        }
    }
    [CustomModConfigItem(typeof(ObjectElementFixed))]
    public class ShowEnableStatusTextClass {
        public enum ShowType {
            Never,
            WhenNotZero,
            Always
        }
        [JsonIgnore]
        public bool ShowAny => !ShowNone;
        [ShowDespiteJsonIgnore, JsonIgnore]
        public bool ShowAllAlways {
            get => AllMods == ShowType.Always && Enabled == ShowType.Always && ToEnable == ShowType.Always && ToDisable == ShowType.Always;
            set {
                if (value) {
                    AllMods = ShowType.Always;
                    Enabled = ShowType.Always;
                    ToEnable = ShowType.Always;
                    ToDisable = ShowType.Always;
                }
            }
        }
        [ShowDespiteJsonIgnore, JsonIgnore]
        public bool ShowAllWhenNotZero {
            get => AllMods == ShowType.WhenNotZero && Enabled == ShowType.WhenNotZero && ToEnable == ShowType.WhenNotZero && ToDisable == ShowType.WhenNotZero;
            set {
                if (value) {
                    AllMods = ShowType.WhenNotZero;
                    Enabled = ShowType.WhenNotZero;
                    ToEnable = ShowType.WhenNotZero;
                    ToDisable = ShowType.WhenNotZero;
                }
            }
        }
        [ShowDespiteJsonIgnore, JsonIgnore]
        public bool ShowNone {
            get => AllMods == ShowType.Never && Enabled == ShowType.Never && ToEnable == ShowType.Never && ToDisable == ShowType.Never;
            set {
                if (value) {
                    AllMods = ShowType.Never;
                    Enabled = ShowType.Never;
                    ToEnable = ShowType.Never;
                    ToDisable = ShowType.Never;
                }
            }
        }
        [DrawTicks, DefaultValue(ShowType.WhenNotZero)]
        public ShowType AllMods { get; set; } = ShowType.WhenNotZero;
        [DrawTicks, DefaultValue(ShowType.WhenNotZero)]
        public ShowType Enabled { get; set; } = ShowType.WhenNotZero;
        [DrawTicks, DefaultValue(ShowType.WhenNotZero)]
        public ShowType ToEnable { get; set; } = ShowType.WhenNotZero;
        [DrawTicks, DefaultValue(ShowType.WhenNotZero)]
        public ShowType ToDisable { get; set; } = ShowType.WhenNotZero;
        [DefaultValue(" / ")]
        public string Seperator { get; set; } = " / ";
        // 灰色
        [JsonDefaultValue("\"128, 128, 128, 255\"")]
        public Color AllModsColor { get; set; } = Color.Gray;
        // 白色
        [JsonDefaultValue("255, 255, 255, 255")]
        public Color EnabledColor { get; set; } = UIFolderItem.EnabledColor;
        // 绿色
        [JsonDefaultValue("0, 255, 0, 255")]
        public Color ToEnableColor { get; set; } = UIFolderItem.ToEnableColor;
        // 红色
        [JsonDefaultValue("255, 0, 0, 255")]
        public Color ToDisableColor { get; set; } = UIFolderItem.ToDisableColor;
    }
    #endregion

    #region 是否在模组加载时打印日志
    [CustomModConfigItem(typeof(BooleanElementForDeveloperMode))]
    public bool LogModLoading { get; set; }
    public class BooleanElementForDeveloperMode : BooleanElement {
        public BooleanElementForDeveloperMode() {
            if (!IsTMLDeveloperMode) {
                Height.Set(0, 0);
            }
        }
        public override void DrawSelf(SpriteBatch spriteBatch) {
            if (IsTMLDeveloperMode) {
                base.DrawSelf(spriteBatch);
            }
        }
        public override bool Value {
            get => IsTMLDeveloperMode && base.Value;
            set => base.Value = IsTMLDeveloperMode && value;
        }
    }
    #endregion

    #region 更新日志
    [SeparatePage, JsonIgnore, ShowDespiteJsonIgnore]
    public SeeChangelogClass SeeChangelog { get; set; } = new();
    public class SeeChangelogClass {
        [CustomModConfigItem(typeof(ChangelogDisplay))]
        public int ChangelogDisplay;
    }
    public class ChangelogDisplay : FloatElement {
        Asset<DynamicSpriteFont>? _font;
        Asset<DynamicSpriteFont> Font => _font ??= FontAssets.MouseText;
        static Color BaseColor => Color.White;
        string? _changelog;
        TextSnippet[]? _changelogSnippets;
        TextSnippet[] ChangelogSnippets => _changelogSnippets ??= [.. ChatManager.ParseMessage(Changelog, BaseColor)];
        string Changelog {
            get {
                string localizedChangelog = ModFolder.Instance.GetLocalization("Changelog").Value;
                if (_changelog == localizedChangelog) {
                    return _changelog;
                }
                _changelog = localizedChangelog;
                _changelogSnippets = [.. ChatManager.ParseMessage(_changelog, BaseColor)];
                Vector2 stringSize = ChatManager.GetStringSize(Font.Value, _changelog, Vector2.One, GetDimensions().Width);
                Height.Set(stringSize.Y, 0);
                if (Parent != null) {
                    Parent.Height.Set(stringSize.Y, 0);
                    UIElement root = Parent;
                    while (root.Parent != null) {
                        root = root.Parent;
                    }
                    root.Recalculate();
                }
                return _changelog;
            }
        }
        public override void Draw(SpriteBatch spriteBatch) {
            //base.Draw(spriteBatch);
            //spriteBatch.DrawString(Font.Value, Changelog, GetDimensions().Position(), Color.White);
            _ = Changelog;
            // ChatManager.DrawColorCodedString(spriteBatch, Font.Value, ChangelogSnippets, GetDimensions().Position(), BaseColor, 0f, Vector2.Zero, Vector2.One, out _, GetDimensions().Width);
            ChatManager.DrawColorCodedStringWithShadow(spriteBatch, Font.Value, ChangelogSnippets, GetDimensions().Position(), 0, Vector2.Zero, Vector2.One, out _, GetDimensions().Width);
        }
    }
    #endregion

    public void Save() {
        ConfigManager.Save(this);
    }

    public override void OnLoaded() {
        Instance = this;
    }

    public override void OnChanged() {
        ;
    }
}

public static class ShoeEnableStatusTextClassShowTypeExtensions {
    public static bool Never(this CommonConfig.ShowEnableStatusTextClass.ShowType self) => self == CommonConfig.ShowEnableStatusTextClass.ShowType.Never;
    public static bool WhenNotZero(this CommonConfig.ShowEnableStatusTextClass.ShowType self) => self == CommonConfig.ShowEnableStatusTextClass.ShowType.WhenNotZero;
    public static bool Always(this CommonConfig.ShowEnableStatusTextClass.ShowType self) => self == CommonConfig.ShowEnableStatusTextClass.ShowType.Always;
    public static bool Check(this CommonConfig.ShowEnableStatusTextClass.ShowType self, int value) => self == CommonConfig.ShowEnableStatusTextClass.ShowType.Always || self == CommonConfig.ShowEnableStatusTextClass.ShowType.WhenNotZero && value != 0;
}