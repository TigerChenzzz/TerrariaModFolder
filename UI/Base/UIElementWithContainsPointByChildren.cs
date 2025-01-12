using Terraria.UI;

namespace ModFolder.UI.Base;

public class UIElementWithContainsPointByChildren : UIElement {
    public override bool ContainsPoint(Vector2 point) {
        foreach (var element in Elements) {
            if (element.ContainsPoint(point)) {
                return true;
            }
        }
        return false;
    }
}
