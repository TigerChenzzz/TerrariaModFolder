using Terraria.ModLoader.UI;

namespace ModFolder.UI;

/// <summary>
/// 文件夹系统列表中的一个文件夹
/// </summary>
public class UIFolder(string name) : UIFolderItem {
    /// <summary>
    /// 父节点, 如果为空则代表父节点为根节点
    /// </summary>
    public UIFolder? FolderParent { get; set; }
    public List<UIFolderItem> Items { get; set; } = [];
    public string Name { get; set; } = name;
    public string Path => FolderParent == null ? Name : $"{FolderParent.Path}\\{Name}";
    // TODO
    public DateTime LastModified { get; set; }
    public override int CompareTo(object obj) {
        if (obj is UIModItemInFolder) {
            return -1;
        }
        if (obj is not UIFolder other) {
            return 1;
        }
        return UIModFolderMenu.Instance.sortMode switch {
            ModsMenuSortMode.RecentlyUpdated => other.LastModified.CompareTo(LastModified),
            ModsMenuSortMode.DisplayNameAtoZ => string.Compare(Name, other.Name, StringComparison.Ordinal),
            ModsMenuSortMode.DisplayNameZtoA => string.Compare(other.Name, Name, StringComparison.Ordinal),
            _ => base.CompareTo(obj),
        };
    }
}
