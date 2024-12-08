using ReLogic.Content;
using ReLogic.Graphics;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.UI;
using Terraria.UI.Chat;

namespace ModFolder.UI.Menu.Notification;

public class TextNotification : IMenuNotification {
    public bool ShouldBeRemoved => TimeLeft <= 0;

    public int MaxTimeLeft { get; }
    public StyleDimension MaxWidth { get; }
    private float _realMaxWidth;
    [MemberNotNull(nameof(WrappedSnippets))]
    private void SetRealMaxWidth() {
        var value = MaxWidth.Percent * Main.screenWidth + MaxWidth.Pixels;
        if (WrappedSnippets != null && _realMaxWidth == value) {
            return;
        }
        _realMaxWidth = value;
        WrappedSnippets = [.. ChatManagerFix.CreateWrappedText(Font.Value, Snippets, 1, value)];
        panelSize = ChatManagerFix.GetStringSize(Font.Value, Snippets, 1, value) + new Vector2(BorderX, BorderY) * 2;
    }
    /// <summary>
    /// 从最小变到最大或者从最大变到最小的时间
    /// </summary>
    public int ScaleTime { get; set; } = 18;
    [Range(0f, 1f)] //!注意不是Config中的那个Range
    public float DisappearScale { get; set; } = 0.4f;
    public int TimeLeft { get; set; }
    public float BorderX { get; set; } = 20f;
    public float BorderY { get; set; } = 8f;

    private Asset<DynamicSpriteFont>? _font;
    private Asset<DynamicSpriteFont> Font {
        get => _font ?? FontAssets.MouseText;
        set => _font = value;
    }
    private void SetFont(Asset<DynamicSpriteFont>? font) => _font = font;

    private string? Text { get; }
    private TextSnippet[] Snippets { get; }
    private TextSnippet[]? WrappedSnippets { get; set; }
    /// <summary>
    /// 未经过缩放的板板大小
    /// </summary>
    private Vector2 panelSize;
    private Vector2 panelSizeScaled;

    public float BaseScale => (TimeLeft < ScaleTime) ? Lerp(0f, 1f, TimeLeft / (float)ScaleTime, false, LerpType.CubicByK, 3f, -0.8f) :
        (TimeLeft > MaxTimeLeft - ScaleTime) ? Lerp(0f, 1f, (MaxTimeLeft - TimeLeft) / (float)ScaleTime) :
        1f;
    public float EffectiveScale { get; set; } = 1;
    public float Scale => BaseScale * EffectiveScale;

    float Opacity => DisappearScale >= 1f ? (BaseScale >= 1f ? 1 : 0) :
        BaseScale <= DisappearScale ? 0f :
        (BaseScale - DisappearScale) / (1f - DisappearScale);

    public TextNotification(string text, StyleDimension maxWidth, int timeLeft = 300, Asset<DynamicSpriteFont>? font = null) {
        MaxTimeLeft = TimeLeft = timeLeft;
        MaxWidth = maxWidth;
        Text = text;
        SetFont(font);
        if (string.IsNullOrEmpty(Text)) {
            TimeLeft = 0;
            Snippets = [];
        }
        else {
            Snippets = [.. ChatManager.ParseMessage(text, Color.White)];
        }
    }

    public void Update() {
        TimeLeft -= 1;
    }

    public void Draw(SpriteBatch spriteBatch, Vector2 bottomRightPosition) {
        if (ShouldBeRemoved) {
            return;
        }
        if (Snippets == null) {
            return;
        }
        float opacity = Opacity;
        SetRealMaxWidth();
        var scale = Scale;
        panelSizeScaled = panelSize * scale;
        if (opacity <= 0f) {
            return;
        }
        //板板的方框
        Rectangle panelRect = NewRectangle(bottomRightPosition, panelSizeScaled, Vector2.One);
        //获得鼠标是否在板板上, 如果在则做一些事情
        bool hovering = panelRect.Contains(Main.MouseScreen.ToPoint());
        OnMouseOver(ref hovering);
        //画出板板, 如果鼠标在板板上则颜色淡一点
        Utils.DrawInvBG(spriteBatch, panelRect, new Color(64, 109, 164) * (hovering ? 0.75f : 0.5f) * opacity);//UI的蓝色
        Color color = Color.LightCyan;
        color.A = Main.mouseTextColor;
        color *= opacity;
        if (WrappedSnippets.Length > 0 && WrappedSnippets[0].Color != color) {
            foreach (var snippet in WrappedSnippets) {
                snippet.Color = color;
            }
        }
        ChatManager.DrawColorCodedStringWithShadow(spriteBatch, Font.Value, WrappedSnippets,
            position: panelRect.TopLeft() + new Vector2(BorderX, BorderY + 4) * scale,
            rotation: 0f,
            color: Color.Black * opacity,// new Color(Main.mouseTextColor, Main.mouseTextColor, Main.mouseTextColor / 5, Main.mouseTextColor) * opacity,
            shadowColor: Color.Black * opacity,
            origin: Vector2.Zero,
            baseScale: new(scale), out _);
    }

    private void OnMouseOver(ref bool hovering) {
        if (!hovering) {
            return;
        }
        if (PlayerInput.IgnoreMouseInterface || Main.LocalPlayer.mouseInterface) {
            hovering = false;
            return;
        }
        Main.LocalPlayer.mouseInterface = true;

        if (!Main.mouseLeft || !Main.mouseLeftRelease) {
            return;
        }

        Main.mouseLeftRelease = false;
        TimeLeft = Math.Min(TimeLeft, ScaleTime);
    }

    public void PushAnchor(ref Vector2 positionAnchorBottom) {
        positionAnchorBottom.Y -= panelSizeScaled.Y * Scale;
    }
}