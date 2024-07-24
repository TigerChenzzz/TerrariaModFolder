using Terraria.UI;

namespace ModFolder.UI;

/// <summary>
/// 文件夹系统列表中的一件物品, 可能是文件夹, 可能是模组, 也可能是其它什么东西
/// </summary>
public class UIFolderItem : UIElement {
    #region 构造
    public UIFolderItem() : base() {
        Height.Pixels = 30;
        Width.Percent = 1f;
    }
    #endregion
}
