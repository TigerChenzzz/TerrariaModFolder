namespace ModFolder.UI.Base;

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

public enum MenuShowType {
    FolderSystem,
    AllMods,
}
