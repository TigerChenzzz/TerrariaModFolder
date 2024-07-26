using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Terraria.ModLoader.Core;
using Terraria.Social.Steam;

namespace ModFolder.Systems;

public static class FolderDataSystem {
    #region 类型
    public class Node { }
    [JsonObject(MemberSerialization.OptIn)]
    public class ModNode : Node {
        public ModNode(LocalMod mod) {
            ModName = mod.Name;
            if (WorkshopHelper.GetPublishIdLocal(mod.modFile, out ulong publishId)) {
                PublishId = publishId;
            }
        }
        public ModNode(string modName) => ModName = modName;
        [JsonConstructor]
        private ModNode() : this(string.Empty) { }
        [JsonProperty]
        public string ModName { get; set; }
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public ulong PublishId { get; set; }
        public void ReceiveDataFrom(LocalMod mod) {
            ModName = mod.Name;
            if (WorkshopHelper.GetPublishIdLocal(mod.modFile, out ulong publishId)) {
                PublishId = publishId;
            }
        }
    }
    [JsonObject(MemberSerialization.OptIn)]
    public class FolderNode(string folderName) : Node {
        [JsonConstructor]
        private FolderNode() : this(string.Empty) { }
        [JsonProperty]
        public string FolderName { get; set; } = folderName;
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
    #endregion
    private static FolderNode? _root;
    public static FolderNode Root {
        get {
            if (_root != null) {
                return _root;
            }
            Reload_Inner();
            return _root ??= new("Root");
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
        _root ??= new("Root");
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
            var data = LoadNode(jsonData);
            _root = data as FolderNode;
        }
        catch (Exception e) when (e is JsonReaderException or JsonSerializationException) {
            ModFolder.Instance.Logger.Warn("Load folder data failed!", e);
            // File.Delete(path);
        }
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
