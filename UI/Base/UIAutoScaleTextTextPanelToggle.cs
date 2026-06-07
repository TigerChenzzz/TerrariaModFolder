using Terraria.Audio;
using Terraria.ModLoader.UI;

namespace ModFolder.UI.Base;

public class UIAutoScaleTextTextPanelToggle<T> : UIAutoScaleTextTextPanel<T> {
    public void Toggle() => Toggled = !Toggled;
    public bool Toggled {
        get;
        set {
            if (field == value)
                return;
            field = value;
            UpdateColor();
            OnToggled?.Invoke(value);
        }
    }
    public Color OverColor;
    public Color OutColor;
    public Color OverBorderColor;
    public Color OutBorderColor;
    public Color NormalTextColor;
    public Color UntoggledOverColor;
    public Color UntoggledOutColor;
    public Color UntoggledOverBorderColor;
    public Color UntoggledOutBorderColor;
    public Color UntoggledTextColor;
    public event Action<bool>? OnToggled;
    public UIAutoScaleTextTextPanelToggle(T text, float textScaleMax = 1, bool large = false) : base(text, textScaleMax, large) {
        OverColor = UICommon.DefaultUIBlue;
        OutColor = UICommon.DefaultUIBlueMouseOver;
        OverBorderColor = UICommon.DefaultUIBorderMouseOver;
        OutBorderColor = UICommon.DefaultUIBorder;
        UntoggledOverColor = UICommon.DefaultUIBlue * 0.6f;
        UntoggledOutColor = UICommon.DefaultUIBlueMouseOver * 0.6f;
        UntoggledOverBorderColor = UICommon.DefaultUIBorderMouseOver * 0.6f;
        UntoggledOutBorderColor = UICommon.DefaultUIBorder * 0.6f;

        NormalTextColor = Color.White;
        UntoggledTextColor = Color.Gray;

        OnMouseOver += (_, _) => UpdateColor();
        OnMouseOut += (_, _) => UpdateColor();
        UpdateColor();

        OnLeftClick += (_, _) => {
            SoundEngine.PlaySound(SoundID.MenuTick);
            Toggle();
        };
    }
    public void UpdateColor() {
        (TextColor, BackgroundColor, BorderColor) = (Toggled, IsMouseHovering) switch {
            (false, false) => (UntoggledTextColor, UntoggledOutColor , UntoggledOutBorderColor ),
            (false, true ) => (UntoggledTextColor, UntoggledOverColor, UntoggledOverBorderColor),
            (true , false) => (NormalTextColor   , OutColor          , OutBorderColor          ),
            (true , true ) => (NormalTextColor   , OverColor         , OverBorderColor         ),
        };
    }
}
