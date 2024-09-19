using ModFolder.Systems;

namespace ModFolder.UI.UIFolderItems.Mod;

/// <summary>
/// 文件夹系统列表中的一个模组
/// </summary>
public abstract class UIModItemInFolder : UIFolderItem {
    public abstract string ModName { get; }
    public abstract string ModDisplayName { get; }
    private string? _modDisplayNameClean;
    /// <summary>
    ///  No chat tags: for search and sort functionality.
    /// </summary>
    public string ModDisplayNameClean => _modDisplayNameClean ??= Utils.CleanChatTags(ModDisplayName);
    public string? GetAlias() => FolderDataSystem.ModAliases.TryGetValue(ModName, out var value) ? value : null;
    public override string NameToSort => GetAlias() ?? ModDisplayNameClean;
    /// <summary>
    /// 考虑别名
    /// </summary>
    public virtual string GetModDisplayName() {
        return GetAlias() ?? ModDisplayName;
    }
}
