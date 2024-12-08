using ModFolder.Systems;
using ModFolder.UI.Menu;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReLogic.OS;
using System.IO;
using static ModFolder.Systems.FolderDataSystem;
using Platform = ReLogic.OS.Platform;

namespace ModFolder.Helpers;

public static class ShareHelper {
    public static IClipboard Clipboard => Platform.Get<IClipboard>();
    public static string? ClipboardValue {
        get => Clipboard.Value;
        set => Clipboard.Value = value;
    }
    public static void Export(FolderNode folder, bool includeDisplayNames, bool includeAliases, bool includeFavorites) {
        var obj = new ShareFormatClass(folder, includeDisplayNames, includeAliases, includeFavorites);
        ClipboardValue = JsonConvert.SerializeObject(obj);
    }
    public enum ImportResult {
        Success,
        InvalidClipboard,
    }
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
        if (!data.TryGetValue(nameof(ShareFormatClass.Folder), out var folderToken)) {
            return ImportResult.InvalidClipboard;
        }
        if (folderToken is JObject folderData) {
            var node = LoadNode(folderData);
            if (node != null) {
                currentFolder.AddChild(node);
            }
        }
        if (data.TryGetValue(nameof(ShareFormatClass.PublishIds), out var publishIdsToken)) {
            var publishIds = publishIdsToken.ToObject<Dictionary<string, ulong>>();
            SetData(publishIds, PublishIds, replace);
        }
        if (data.TryGetValue(nameof(ShareFormatClass.DisplayNames), out var displayNamesToken)) {
            var displayNames = displayNamesToken.ToObject<Dictionary<string, string>>();
            SetData(displayNames, DisplayNames, replace);
        }
        if (data.TryGetValue(nameof(ShareFormatClass.ModAliases), out var modAliasesToken)) {
            var modAliases = modAliasesToken.ToObject<Dictionary<string, string>>();
            SetData(modAliases, ModAliases, replace);
        }
        if (includeFavorites && data.TryGetValue(nameof(ShareFormatClass.Favorites), out var favoritesToken)) {
            var favorites = favoritesToken.ToObject<HashSet<string>>();
            if (favorites != null) {
                Favorites.AddRange(favorites);
            }
        }
        return ImportResult.Success;
    }
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
        public Dictionary<string, ulong> PublishIds { get; private set; } = [];
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, string>? DisplayNames { get; private set; } = [];
        [JsonProperty(DefaultValueHandling=DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, string>? ModAliases { get; private set; } = [];
        [JsonProperty(DefaultValueHandling=DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
        public HashSet<string>? Favorites { get; private set; } = [];
        // LastModifieds 不分享
        public FolderNode Folder { get; private set; }

        public ShareFormatClass(FolderNode folder, bool includeDisplayNames, bool includeAliases, bool includeFavorites) {
            Folder = folder;
            foreach (var mod in Folder.ModNodesInTree) {
                if (FolderDataSystem.PublishIds.TryGetValue(mod.ModName, out var publishId)) {
                    PublishIds.TryAdd(mod.ModName, publishId);
                }
                if (includeDisplayNames && FolderDataSystem.DisplayNames.TryGetValue(mod.ModName, out var displayName)) {
                    DisplayNames.TryAdd(mod.ModName, displayName);
                }
                if (includeAliases && FolderDataSystem.ModAliases.TryGetValue(mod.ModName, out var alias)) {
                    ModAliases.TryAdd(mod.ModName, alias);
                }
                if (includeFavorites && FolderDataSystem.Favorites.Contains(mod.ModName)) {
                    Favorites.Add(mod.ModName);
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
