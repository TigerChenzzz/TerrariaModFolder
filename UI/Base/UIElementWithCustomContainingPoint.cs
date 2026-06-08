using Terraria.UI;

namespace ModFolder.UI.Base;

public class UIElementWithCustomContainingPoint : UIElement {
    private readonly HashSet<Func<Vector2, bool>> _customContainingPoints = [];
    public void AddCustomContainingPoint(Func<Vector2, bool> func) => _customContainingPoints.Add(func);
    public void RemoveCustomContainingPoint(Func<Vector2, bool> func) => _customContainingPoints.Remove(func);
    public override bool ContainsPoint(Vector2 point) {
        var baseResult = base.ContainsPoint(point);
        if (baseResult)
            return true;
        foreach (var func in _customContainingPoints) {
            if (func(point))
                return true;
        }
        return false;
    }
}
