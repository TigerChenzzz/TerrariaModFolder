﻿using ReLogic.Content;
using ReLogic.Graphics;
using Terraria.GameContent;
using Terraria.ModLoader.Config;
using Terraria.ModLoader.Config.UI;
using Terraria.UI;
using Terraria.UI.Chat;

namespace ModFolder.Configs;

public class CommonConfig : ModConfig {
    public static CommonConfig Instance = null!;
    public override ConfigScope Mode => ConfigScope.ClientSide;

    public bool LeftClickToEnterFolderSystem { get; set; }

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
    [SeparatePage]
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
                if (_changelog == localizedChangelog)
                {
                    return _changelog;
                }
                _changelog = localizedChangelog;
                _changelogSnippets = [.. ChatManager.ParseMessage(_changelog, BaseColor)];
				Vector2 stringSize = ChatManager.GetStringSize(Font.Value, _changelog, Vector2.One, GetDimensions().Width);
                Height.Set(stringSize.Y, 0);
                if (Parent != null)
                {
                    Parent.Height.Set(stringSize.Y, 0);
                    UIElement root = Parent;
                    while (root.Parent != null)
                    {
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

    public override void OnLoaded() {
        Instance = this;
    }

}