using Microsoft.Xna.Framework.Input;
using Terraria.GameInput;
using Terraria.UI;

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
        base.Update(gameTime);
    }
    private static bool JustPressed(Keys key) {
        if (Main.inputText.IsKeyDown(key)) {
            return !Main.oldInputText.IsKeyDown(key);
        }
        return false;
    }
    public override void DrawSelf(SpriteBatch spriteBatch) {
        if (Focused) {
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
            // ------------- 主要修改的位置 -------------
            if (JustPressed(Keys.Enter)) {
                Focused = false;
                OnUnfocus?.Invoke(this, new());
            }
            // ------------------------------------------
            if (++_textBlinkerCount >= 20) {
                _textBlinkerState = (_textBlinkerState + 1) % 2;
                _textBlinkerCount = 0;
            }
        }
        string text = CurrentString;
        if (_textBlinkerState == 1 && Focused) {
            text += "|";
        }
        CalculatedStyle dimensions = GetDimensions();
        if (CurrentString.Length == 0) {
            Utils.DrawBorderString(spriteBatch, HintText, new Vector2(dimensions.X, dimensions.Y), Color.Gray);
        }
        Utils.DrawBorderString(spriteBatch, text, new Vector2(dimensions.X, dimensions.Y), Color.White);
    }
}