﻿namespace ModFolder.UI.Base;

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

public enum ModLoadedFilter {
    All,
    Loaded,
    Unloaded,
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

public enum MenuShowTypes {
    FolderSystem,
    AllMods,
}

public enum LayoutTypes {
    Stripe,
    Block,
    BlockWithName,
}
