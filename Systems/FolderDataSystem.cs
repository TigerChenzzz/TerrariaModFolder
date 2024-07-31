using ModFolder.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Terraria.ModLoader.Core;
using Terraria.Social.Steam;

namespace ModFolder.Systems;

public static class FolderDataSystem {
    #region 类型
    [JsonObject(MemberSerialization.OptIn)]
    public class Node { }
    public class ModNode : Node {
        public ModNode(LocalMod mod) {
            ModName = mod.Name;
            if (WorkshopHelper.GetPublishIdLocal(mod.modFile, out ulong publishId)) {
                PublishId = publishId;
            }
            DisplayName = mod.DisplayName;
        }
        public ModNode(string modName) => ModName = modName;
        [JsonConstructor]
        private ModNode() : this(string.Empty) { }
        [JsonProperty]
        public string ModName { get; set; }
        public ulong PublishId {
            get => PublishIds.GetValueOrDefault(ModName);
            set {
                if (value == 0) {
                    PublishIds.Remove(ModName);
                }
                else {
                    PublishIds.TryAdd(ModName, value);
                }
            }
        }
        public bool Favorite {
            get => Favorites.Contains(ModName);
            set {
                if (value) {
                    Favorites.Add(ModName);
                }
                else {
                   Favorites.Remove(ModName);
                }
            }
        }
        public string DisplayName {
            get => DisplayNames.GetValueOrDefault(ModName, ModName);
            set {
                if (!DisplayNames.TryAdd(ModName, value)) {
                    DisplayNames[ModName] = value;
                }
            }
        }
        public void ReceiveDataFrom(UIModItemInFolder uiMod) {
            ModName = uiMod.ModName;
            PublishId = uiMod.PublishId;
            DisplayName = uiMod.TheLocalMod.DisplayName;
        }
    }
    public class FolderNode(string folderName) : Node {
        [JsonConstructor]
        private FolderNode() : this(string.Empty) { }
        [JsonProperty]
        public string FolderName { get; set; } = folderName;
        // TODO: 优化: 在 Children.Count == 0 时不序列化 Children 字段
        [JsonProperty]
        public List<Node> Children { get; set; } = [];
        public IEnumerable<ModNode> ModNodesInTree {
            get {
                foreach (var child in Children) {
                    if (child is ModNode m) {
                        yield return m;
                    }
                    else if (child is FolderNode f) {
                        foreach (var fm in f.ModNodesInTree) {
                            yield return fm;
                        }
                    }
                }
            }
        }
    }
    public class RootNode : FolderNode {
        [JsonConstructor]
        public RootNode() : base("Root") { }
        public RootNode(FolderNode folder) : base("Root") {
            Children = folder.Children;
        }
#pragma warning disable CA1822 // 将成员标记为 static
#pragma warning disable IDE0051 // 删除未使用的私有成员
        [JsonProperty]
        private HashSet<string> Favorites => FolderDataSystem.Favorites;
        [JsonProperty]
        private Dictionary<string, ulong> PublishIds => FolderDataSystem.PublishIds;
        [JsonProperty]
        private Dictionary<string, string> DisplayNames => FolderDataSystem.DisplayNames;
#pragma warning restore CA1822 // 将成员标记为 static
#pragma warning restore IDE0051 // 删除未使用的私有成员
    }
    public static HashSet<string> Favorites { get; private set; } = [];
    public static Dictionary<string, ulong> PublishIds { get; private set; } = [];
    public static Dictionary<string, string> DisplayNames { get; private set; } = [];
    #endregion
    private static RootNode? _root;
    public static RootNode Root {
        get {
            if (_root != null) {
                return _root;
            }
            Reload_Inner();
            return _root ??= new();
        }
    }
    private static string DataPath {
        get {
            Directory.CreateDirectory(ModOrganizer.modPath);
            return Path.Combine(ModOrganizer.modPath, "ModFolderData.json");
        }
    }
    public static void Save() {
        File.WriteAllText(DataPath, JsonConvert.SerializeObject(_root));
    }
    public static void Reload() {
        Reload_Inner();
        _root ??= new();
    }
    private static void Reload_Inner() {
        string path = DataPath;
        if (!File.Exists(path)) {
            return;
        }
        using var _ = new Logging.QuietExceptionHandle();
        try {
            var json = JsonConvert.DeserializeObject(File.ReadAllText(path));
            if (json is not JObject jsonData) {
                return;
            }
            LoadRoot(jsonData);
        }
        catch (Exception e) when (e is JsonReaderException or JsonSerializationException) {
            ModFolder.Instance.Logger.Warn("Load folder data failed!", e);
            // TODO: 备份
            // File.Delete(path);
        }
    }
    private static void LoadRoot(JObject data) {
        if (data.TryGetValue(nameof(Favorites), out var favoritesToken)) {
            var favorites = favoritesToken.ToObject<HashSet<string>>();
            if (favorites != null) {
                Favorites = favorites;
            }
        }
        if (data.TryGetValue(nameof(PublishIds), out var publishIdsToken)) {
            var publishIds = publishIdsToken.ToObject<Dictionary<string, ulong>>();
            if (publishIds != null) {
                PublishIds = publishIds;
            }
        }
        if (data.TryGetValue(nameof(DisplayNames), out var displayNamesToken)) {
            var displayNames = displayNamesToken.ToObject<Dictionary<string, string>>();
            if (displayNames != null) {
                DisplayNames = displayNames;
            }
        }
        _root = LoadNode(data) is not FolderNode node ? new() : new(node);
    }
    private static Node? LoadNode(JObject data) {
        if (data.TryGetValue("FolderName", out var folderNameToken) && folderNameToken is JValue folderNameValue && folderNameValue.Value?.ToString() is string folderNameString) {
            FolderNode folderNode = new(folderNameString);
            if (data.TryGetValue("Children", out var children)) {
                LoadFolderNodeFromChildren(folderNode, children);
            }
            return folderNode;
        }
        else if (data.TryGetValue("ModName", out var modNameToken) && modNameToken is JValue modNameValue) {
            ModNode modNode = new(modNameValue.ToString());
            if (data.TryGetValue("PublishId", out var publishIdToken) && publishIdToken is JValue publishIdValue && publishIdValue.Type == JTokenType.Integer) {
                modNode.PublishId = Convert.ToUInt64(publishIdValue.Value);
            }
            return modNode;
        }
        return null;
    }
    private static void LoadFolderNodeFromChildren(FolderNode folder, JToken children) {
        foreach (var child in children.Children<JObject>()) {
            var childNode = LoadNode(child);
            if (childNode != null) {
                folder.Children.Add(childNode);
            }
        }
    }
}
