namespace ModFolder.UI.Base;

public class UIFolderItemFilterResults {
    public int FilteredBySearch { get; set; }
    public int FilteredByModSide { get; set; }
    public int FilteredByEnabled { get; set; }
    public int FilteredByLoaded { get; set; }
    public bool AnyFiltered => FilteredBySearch > 0 || FilteredByModSide > 0 || FilteredByEnabled > 0 || FilteredByLoaded > 0;
}
