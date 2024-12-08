using ModFolder.UI.Menu;
using System.Threading.Tasks;
using Terraria.ModLoader.UI;
using Terraria.ModLoader.UI.ModBrowser;

namespace ModFolder.Helpers;

public static class DownloadHelper {
    /// <summary>
    /// 可能会对 <paramref name="mods"/> 作出改动
    /// </summary>
    public static ModDownloadItem[] GetFullDownloadList(HashSet<ModDownloadItem> mods) {
        Interface.modBrowser.SocialBackend.GetDependenciesRecursive(mods);
        return ModDownloadItem.NeedsInstallOrUpdate(mods).ToArray();
    }
    /// <summary>
    /// 可能会对 <paramref name="mods"/> 作出改动
    /// </summary>
    public static IEnumerable<ModDownloadItem> GetFullDownloadEnumerable(HashSet<ModDownloadItem> mods) {
        Interface.modBrowser.SocialBackend.GetDependenciesRecursive(mods);
        return ModDownloadItem.NeedsInstallOrUpdate(mods);
    }
    /// <summary>
    /// 可能会对 <paramref name="mods"/> 作出改动
    /// </summary>
    public static async Task DownloadMods(HashSet<ModDownloadItem> mods) {
        // 取自 UIModBrowser
        #region 查找依赖
        var fullList = GetFullDownloadList(mods);
        if (fullList.Length == 0)
            return;
        #endregion
        #region 下载模组
        // 取自 UIModBrowser
        try {
            foreach (var mod in fullList) {
                await Task.Yield();
                if (UIModFolderMenu.Instance.Downloads.ContainsKey(mod.ModName)) {
                    continue;
                }
                bool wasInstalled = mod.IsInstalled;

                if (ModLoader.TryGetMod(mod.ModName, out var loadedMod)) {
                    loadedMod.Close();
                    // We must clear the Installed reference in ModDownloadItem to facilitate downloading, in addition to disabling - Solxan
                    mod.Installed = null;
                    UIModFolderMenu.Instance.ForceRoadRequired();
                }

                #region 下载模组 (摘自 WorkshopBrowserModule.DownloadItem)
                mod.UpdateInstallState();
                UIModFolderMenu.Instance.AddDownload(mod.ModName, new(mod));
                #endregion
            }
        }
        catch (Exception e) {
            UIModFolderMenu.PopupInfoByKey("UI.PopupInfos.DownloadModError");
            ModFolder.Instance.Logger.Error("Downloading mod error!", e);
        }
        #endregion
    }
}
