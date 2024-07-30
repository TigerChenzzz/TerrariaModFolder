namespace ModFolder.UI; 

public enum FolderMenuSortMode {
    Custom,
    RecentlyUpdated,
    OldlyUpdated,
    DisplayNameAtoZ,
    DisplayNameZtoA,
}

public enum FolderModSortMode {
    Custom,
    FolderFirst,
    ModFirst,
}

public enum FolderEnabledFilter {
    All,
    Enabled,
    Disabled,
    ToBeEnabled,
    ToBeDisabled,
    ToToggle,
    WouldBeEnabled,
    WouldBeDisabled,
}
