using PassFilterResults = ModFolder.UI.UIFolderItems.UIFolderItem.PassFilterResults;

namespace ModFolder.UI.Base;

public class UIFolderItemFilterResults {
    /// <summary>
    /// 返回是否真的被过滤掉了
    /// </summary>
    public bool AddFiltered(PassFilterResults result) {
        if (result.HasFlag(PassFilterResults.FilteredBySearch)) {
            FilteredBySearch += 1;
        }
        if (result.HasFlag(PassFilterResults.FilteredByModSide)) {
            FilteredByModSide += 1;
        }
        if (result.HasFlag(PassFilterResults.FilteredByEnabled)) {
            FilteredByEnabled += 1;
        }
        if (result.HasFlag(PassFilterResults.FilteredByLoaded)) {
            FilteredByLoaded += 1;
        }
        if (result.HasFlag(PassFilterResults.FilteredByContent)) {
            FilteredByContent += 1;
        }
        if (result != PassFilterResults.NotFiltered) {
            FilteredTotal += 1;
            return true;
        }
        return false;
    }
    public int FilteredTotal { get; private set; }
    public int FilteredBySearch { get; private set; }
    public int FilteredByModSide { get; private set; }
    public int FilteredByEnabled { get; private set; }
    public int FilteredByLoaded { get; private set; }
    public int FilteredByContent { get; private set; }
    public bool AnyFiltered => FilteredTotal > 0;
}
