namespace ModFolder.UI.Base;

public enum FolderMenuSortModes {
    Custom,
    RecentlyUpdated,
    OldlyUpdated,
    DisplayNameAtoZ,
    DisplayNameZtoA,
}

public enum FolderModSortModes {
    Custom,
    FolderFirst,
    ModFirst,
}

public enum ModLoadedFilters {
    All,
    Loaded,
    Unloaded,
}

public enum FolderEnabledFilters {
    All,
    Enabled,
    Disabled,
    ToBeEnabled,
    ToBeDisabled,
    ToToggle,
    WouldBeEnabled,
    WouldBeDisabled,
}

public enum MenuShowTypes {
    FolderSystem,
    AllMods,
}

public enum LayoutTypes {
    Stripe,
    Block,
    BlockWithName,
}

public enum FolderFilterModes {
    DoNotFilter,
    FilterName,
    FilterContent,
    FilterNameAndContent,
}
