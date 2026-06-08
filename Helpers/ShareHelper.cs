using ModFolder.Systems;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReLogic.OS;
using static ModFolder.Systems.FolderDataSystem;
using Platform = ReLogic.OS.Platform;

namespace ModFolder.Helpers;

public static class ShareHelper {
    public static IClipboard Clipboard => Platform.Get<IClipboard>();
    public static string? ClipboardValue {
        get => Clipboard.Value;
        set => Clipboard.Value = value;
    }
    public static void Export(List<Node> nodes, bool includeDisplayNames, bool includeAliases, bool includeFavorites) {
        var obj = new ShareFormatClass(ModFolder.Instance.Version.ToString(), nodes, includeDisplayNames, includeAliases, includeFavorites);
        ClipboardValue = JsonConvert.SerializeObject(obj);
    }
    #region Import
    public enum ImportResult {
        Success,
        InvalidClipboard,
        NotImplemented,
        InvalidVersion,
    }
    public record struct ImportArgs(FolderNode CurrentFolder, JObject Data, bool Replace, bool IncludeFavorites);
    public static ImportResult Import(FolderNode currentFolder, bool replace, bool includeFavorites) {
        JObject data;
        using (new Logging.QuietExceptionHandle()) {
            try {
                if (ClipboardValue == null) {
                    return ImportResult.InvalidClipboard;
                }
                var json = JsonConvert.DeserializeObject(ClipboardValue);
                if (json is not JObject jObject) {
                    return ImportResult.InvalidClipboard;
                }
                data = jObject;
            }
            catch (Exception e) when (e is JsonReaderException or JsonSerializationException) {
                ModFolder.Instance.Logger.Warn("Load folder data failed!", e);
                return ImportResult.InvalidClipboard;
            }
        }
        ImportArgs args = new(currentFolder, data, replace, includeFavorites);
        if (!data.TryGetValue("Version", out var versionToken)) {
            return ImportVersions[0].Import(args);
        }
        var importVersionString = versionToken.ToObject<string>();
        if (!Version.TryParse(importVersionString, out var importVersion)) {
            return ImportResult.InvalidVersion;
        }
        for (int i = ImportVersions.Count - 1; i >= 0; --i) {
            var (version, f) = ImportVersions[i];
            if (importVersion >= version) {
                return f(args);
            }
        }
        return ImportResult.NotImplemented;
    }

    // 元组的第一项代表是在哪个版本更新的
    private static readonly List<(Version Version, Func<ImportArgs, ImportResult> Import)> ImportVersions = [
        (new(0, 0, 0), ImportV0),
        (new(0, 7, 0), ImportV1),
    ];
    #region Import Versions
    private static ImportResult ImportV1(ImportArgs args) {
        var (currentFolder, data, replace, includeFavorites) = args;
        #region Nodes
        if (!data.TryGetValue("Nodes", out var nodesToken)) {
            return ImportResult.InvalidClipboard;
        }
        if (nodesToken is not JArray nodesArray) {
            return ImportResult.InvalidClipboard;
        }
        var nodeJsons = nodesArray.ToObject<List<JObject>>();
        if (nodeJsons != null) {
            bool modified = false;
            foreach (var nodeJson in nodeJsons) {
                var node = LoadNode(nodeJson);
                if (node != null) {
                    currentFolder.AddChildF(node);
                    modified = true;
                }
            }
            if (modified) {
                TreeChanged();
            }
        }
        else {
            return ImportResult.InvalidClipboard;
        }
        #endregion
        ImportInfosV0(data, replace, includeFavorites);
        return ImportResult.Success;
    }
    private static ImportResult ImportV0(ImportArgs args) {
        var (currentFolder, data, replace, includeFavorites) = args;
        #region Folder
        if (!data.TryGetValue("Folder", out var folderToken)) {
            return ImportResult.InvalidClipboard;
        }
        if (folderToken is JObject folderData) {
            var node = LoadNode(folderData);
            if (node != null) {
                currentFolder.AddChild(node);
            }
        }
        #endregion
        ImportInfosV0(data, replace, includeFavorites);
        return ImportResult.Success;
    }
    #region FuncComponents
    private static void ImportInfosV0(JObject data, bool replace, bool includeFavorites) {
        if (data.TryGetValue("PublishIds", out var publishIdsToken)) {
            var publishIds = publishIdsToken.ToObject<Dictionary<string, ulong>>();
            SetData(publishIds, PublishIds, replace);
        }
        if (data.TryGetValue("DisplayNames", out var displayNamesToken)) {
            var displayNames = displayNamesToken.ToObject<Dictionary<string, string>>();
            SetData(displayNames, DisplayNames, replace);
        }
        if (data.TryGetValue("ModAliases", out var modAliasesToken)) {
            var modAliases = modAliasesToken.ToObject<Dictionary<string, string>>();
            SetData(modAliases, ModAliases, replace);
        }
        if (includeFavorites && data.TryGetValue("Favorites", out var favoritesToken)) {
            var favorites = favoritesToken.ToObject<HashSet<string>>();
            if (favorites != null) {
                Favorites.AddRange(favorites);
            }
        }
    }
    #endregion
    #endregion
    #endregion
    private static void SetData<T>(Dictionary<string, T>? from, Dictionary<string, T> to, bool replace) {
        if (from == null) {
            return;
        }
        foreach (var (key, value) in from) {
            to.Set(key, value, replace);
        }
    }
    [JsonObject]
    class ShareFormatClass {
        public string? Version { get; private set; }
        public Dictionary<string, ulong> PublishIds { get; private set; } = [];
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, string>? DisplayNames { get; private set; } = [];
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, string>? ModAliases { get; private set; } = [];
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
        public HashSet<string>? Favorites { get; private set; } = [];
        // LastModifieds 不分享
        public List<Node> Nodes { get; private set; }

        public ShareFormatClass(string version, List<Node> nodes, bool includeDisplayNames, bool includeAliases, bool includeFavorites) {
            Version = version;
            Nodes = nodes;
            HashSet<string> modNames = [];
            foreach (var node in nodes) {
                if (node is ModNode m) {
                    modNames.Add(m.ModName);
                }
                else if (node is FolderNode f) {
                    foreach (var mod in f.ModNodesInTree) {
                        modNames.Add(mod.ModName);
                    }
                }
            }
            foreach (var modName in modNames) {
                if (FolderDataSystem.PublishIds.TryGetValue(modName, out var publishId)) {
                    PublishIds.TryAdd(modName, publishId);
                }
            }
            if (includeDisplayNames) {
                foreach (var modName in modNames) {
                    if (FolderDataSystem.DisplayNames.TryGetValue(modName, out var displayName)) {
                        DisplayNames.TryAdd(modName, displayName);
                    }
                }
            }
            if (includeAliases) {
                foreach (var modName in modNames) {
                    if (FolderDataSystem.ModAliases.TryGetValue(modName, out var alias)) {
                        ModAliases.TryAdd(modName, alias);
                    }
                }
            }
            if (includeFavorites) {
                foreach (var modName in modNames) {
                    if (FolderDataSystem.Favorites.Contains(modName)) {
                        Favorites.Add(modName);
                    }
                }
            }

            if (DisplayNames.Count == 0) {
                DisplayNames = null;
            }
            if (ModAliases.Count == 0) {
                ModAliases = null;
            }
            if (Favorites.Count == 0) {
                Favorites = null;
            }
        }
    }
}
