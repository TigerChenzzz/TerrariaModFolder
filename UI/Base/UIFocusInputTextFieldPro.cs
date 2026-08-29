using Microsoft.Xna.Framework.Input;
using ModFolder.UI.Menu;
using ReLogic.Localization.IME;
using ReLogic.OS;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.UI;
using Terraria.UI.Chat;

namespace ModFolder.UI.Base;

// 主要修改:
//     在按下 Enter 时也会失去焦点
//     改名: CurrentString -> Text
//     将 Text 改为属性, 无论何时修改时触发 OnTextChange
public class UIFocusInputTextFieldPro(string hintText) : UIElement {
    public delegate void EventHandler(object sender, EventArgs e);
    public bool Focused {
        get;
        set {
            if (field == value) {
                return;
            }
            field = value;
            if (value) {
                UIModFolderMenu.Instance.SetIMEPositionGetter(GetIMEPosition);
            }
            else {
                UIModFolderMenu.Instance.SetIMEPositionGetter(null);
                OnUnfocus?.Invoke(this, new());
            }
        }
    }
    public string Text {
        get;
        set {
            if (field != value) {
                field = value;
                OnTextChange?.Invoke(this, new());
            }
        }
    } = string.Empty;
    public string HintText { get; set; } = hintText;
    private int _textBlinkerCount;
    private int _textBlinkerState;
    public bool UnfocusOnTab { get; set; }
    public event EventHandler? OnTextChange;
    public event EventHandler? OnUnfocus;
    public event EventHandler? OnTab;
    public float TextXAlign;
    public override void LeftClick(UIMouseEvent evt) {
        Main.clrInput();
        Focused = true;
    }
    public override void Update(GameTime gameTime) {
        Vector2 point = new(Main.mouseX, Main.mouseY);
        if (!ContainsPoint(point) && Main.mouseLeft) {
            Focused = false;
        }
        if (++_textBlinkerCount >= 20) {
            _textBlinkerState = (_textBlinkerState + 1) % 2;
            _textBlinkerCount = 0;
        }
        base.Update(gameTime);
    }
    private static bool JustPressed(Keys key) {
        if (Main.inputText.IsKeyDown(key)) {
            return !Main.oldInputText.IsKeyDown(key);
        }
        return false;
    }
    private void HandleInput() {
        if (!Focused) {
            return;
        }
        PlayerInput.WritingText = true;
        Main.instance.HandleIME();
        string inputText = Main.GetInputText(Text);
        if (Main.inputTextEscape) {
            Main.inputTextEscape = false;
            Focused = false;
        }
        Text = inputText;
        if (JustPressed(Keys.Tab)) {
            if (UnfocusOnTab) {
                Focused = false;
            }
            OnTab?.Invoke(this, new());
        }
        if (JustPressed(Keys.Enter)) {
            Focused = false;
        }
    }
    public override void DrawSelf(SpriteBatch spriteBatch) {
        HandleInput();
        string text = Text;
        var dimensions = _dimensions;
        var width = dimensions.Width;
        if (text.Length == 0 && !Focused) {
            var hintText = HintText;
            var hintSize = ChatManager.GetStringSize(FontAssets.MouseText.Value, hintText, Vector2.One).X;
            float hintLeft;
            if (width <= hintSize) {
                hintLeft = 0;
            }
            else {
                hintLeft = (width - hintSize) * TextXAlign;
            }
            Vector2 hintPosition = new((int)(dimensions.X + hintLeft), (int)dimensions.Y);
            Utils.DrawBorderString(spriteBatch, hintText, hintPosition, Color.Gray);
            return;
        }
        List<TextSnippet> textSnippets = ChatManager.ParseMessage(text, Color.White);
        // from Main.DrawPlayerChat
        string compositionString = Platform.Get<IImeService>().CompositionString;
		if (compositionString != null && compositionString.Length > 0)
			textSnippets.Add(new(compositionString, new Color(255, 240, 20)));
        var textSize = ChatManager.GetStringSize(FontAssets.MouseText.Value, [..textSnippets], Vector2.One).X + 12;
        float left;
        if (width <= textSize) {
            left = width - textSize;
        }
        else {
            left = (width - textSize) * TextXAlign;
        }
        Vector2 textPosition = new((int)(dimensions.X + left), (int)dimensions.Y);

        if (_textBlinkerState == 1 && Focused) {
            textSnippets.Add(new("|"));
        }
        // Utils.DrawBorderString(spriteBatch, textSnippets, textPosition, Color.White);
        ChatManager.DrawColorCodedStringWithShadow(spriteBatch, FontAssets.MouseText.Value, [.. textSnippets],
            textPosition, 0, Vector2.Zero, Vector2.One, out _);
    }

    private Vector2? GetIMEPosition() {
        if (!Focused) {
            return null;
        }
        var rect = _dimensions.ToRectangle();
        return rect.BottomLeft() + new Vector2(0, 32);
    }
}
