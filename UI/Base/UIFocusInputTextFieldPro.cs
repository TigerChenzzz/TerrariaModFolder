using Microsoft.Xna.Framework.Input;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.UI;
using Terraria.UI.Chat;

namespace ModFolder.UI.Base;

// 主要修改: 在按下 Enter 时也会失去焦点
public class UIFocusInputTextFieldPro(string hintText) : UIElement {
    public delegate void EventHandler(object sender, EventArgs e);
    public bool Focused;
    public string CurrentString = "";
    public string HintText { get; set; } = hintText;
    private int _textBlinkerCount;
    private int _textBlinkerState;
    public bool UnfocusOnTab { get; set; }
    public event EventHandler? OnTextChange;
    public event EventHandler? OnUnfocus;
    public event EventHandler? OnTab;
    public float TextXAlign;
    public void SetText(string? text) {
        text ??= "";
        if (CurrentString != text) {
            CurrentString = text;
            OnTextChange?.Invoke(this, new());
        }
    }
    public override void LeftClick(UIMouseEvent evt) {
        Main.clrInput();
        Focused = true;
    }
    public override void Update(GameTime gameTime) {
        Vector2 point = new(Main.mouseX, Main.mouseY);
        if (!ContainsPoint(point) && Main.mouseLeft) {
            Focused = false;
            OnUnfocus?.Invoke(this, new());
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
        string inputText = Main.GetInputText(CurrentString);
        if (Main.inputTextEscape) {
            Main.inputTextEscape = false;
            Focused = false;
            OnUnfocus?.Invoke(this, new());
        }
        if (!inputText.Equals(CurrentString)) {
            CurrentString = inputText;
            OnTextChange?.Invoke(this, new());
        }
        else {
            CurrentString = inputText;
        }
        if (JustPressed(Keys.Tab)) {
            if (UnfocusOnTab) {
                Focused = false;
                OnUnfocus?.Invoke(this, new());
            }
            OnTab?.Invoke(this, new());
        }
        if (JustPressed(Keys.Enter)) {
            Focused = false;
            OnUnfocus?.Invoke(this, new());
        }
    }
    public override void DrawSelf(SpriteBatch spriteBatch) {
        HandleInput();
        string text = CurrentString;
        var dimensions = _dimensions;
        var textSize = ChatManager.GetStringSize(FontAssets.MouseText.Value, text, Vector2.One).X;
        var width = dimensions.Width;
        float left;
        if (width <= textSize + 8) {
            left = width - textSize - 8;
        }
        else {
            left = (width - textSize) * TextXAlign;
        }
        Vector2 textPosition = new((int)(dimensions.X + left), (int)dimensions.Y);

        if (_textBlinkerState == 1 && Focused) {
            text += "|";
        }
        if (CurrentString.Length == 0) {
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
        }
        Utils.DrawBorderString(spriteBatch, text, textPosition, Color.White);
    }
}