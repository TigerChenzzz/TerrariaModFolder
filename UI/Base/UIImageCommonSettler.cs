using Terraria.GameContent.UI.Elements;

namespace ModFolder.UI.Base;

public static class UIImageCommonSettler {
    public static T SettleCommonly<T>(this T image) where T : UIImage {
        image.ScaleToFit = true;
        image.AllowResizingDimensions = false;
        image.RemoveFloatingPointsFromDrawPosition = true;
        return image;
    }
}
