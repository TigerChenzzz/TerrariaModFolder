using ModFolder.Configs;
using ModFolder.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Terraria.ModLoader.Core;
using Terraria.Social.Steam;

namespace ModFolder.Systems;

public static class FolderDataSystem {
    #region 类型
    [JsonObject(MemberSerialization.OptIn)]
    public class Node {
        protected FolderNode? _parent;
        public FolderNode? Parent {
            get => _parent;
            set {
                if (_parent == value) {
                    return;
                }
                _parent?.ChildrenPublic.Remove(this);
                _parent = value;
                value?.ChildrenPublic.Add(this);
            }
        }
        /// <summary>
        /// 危险! 不要随意修改它的值
        /// </summary>
        public FolderNode? ParentPublic { get => _parent; set => _parent = value; }
        /// <summary>
        /// 返回是否有改变 (如果 <see cref="Parent"/> 为空或者本来就是第一位那么就不改变)
        /// </summary>
        /// <returns></returns>
        public bool MoveToTheTop() {
            if (Parent == null) {
                return false;
            }
            var children = Parent.ChildrenPublic;
            if (children.Count == 0 || children[0] == this) {
                return false;
            }
            Node node = this;
            for (int i = 0; i < children.Count; ++i) {
                (node, children[i]) = (children[i], node);
                if (node == this) {
                    return true;
                }
            }
            children.Add(node);
            ModFolder.Instance.Logger.Error("parent should contain the child at MoveToTheTop");
            return true;
        }
    }
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
        public ModNode(ModNode other) : this(other.ModName) { }
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
        /// <summary>
        /// 危险! 不要随意修改它的值
        /// </summary>
        [JsonProperty(PropertyName = "Children")]
        public List<Node> ChildrenPublic { get => _children; set => _children = value; }
        public IReadOnlyList<Node> Children => _children;
        protected List<Node> _children = [];
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
        public void SetChildAtTheTop(Node child) {
            child.Parent = null;
            child.ParentPublic = this;
            _children.Insert(0, child);
        }
        /// <summary>
        /// 返回是否有改变
        /// </summary>
        public bool MoveChildBeforeChild(Node child, Node target) {
            if (child.Parent != this || target.Parent != this) {
                ModFolder.Instance.Logger.Error("child's parent sould be self at MoveChildAfterChild");
                return false;
            }
            if (child == target) {
                return false;
            }
            for (int i = 0; i < _children.Count; ++i) {
                if (_children[i] == target) {
                    if (i > 0 && _children[i - 1] == child) {
                        return false;
                    }
                    if (!_children.Remove(child)) {
                        ModFolder.Instance.Logger.Error("parent should contain the child at MoveChildAfterChild");
                        return false;
                    }
                    if (i > 0 && _children[i - 1] == target) {
                        _children.Insert(i - 1, child);
                    }
                    else {
                        _children.Insert(i, child);
                    }
                    return true;
                }
            }
            ModFolder.Instance.Logger.Error("parent should contain the target child at MoveChildAfterChild");
            return false;
        }
        public bool MoveChildAfterChild(Node child, Node target) {
            if (child.Parent != this || target.Parent != this) {
                ModFolder.Instance.Logger.Error("child's parent sould be self at MoveChildAfterChild");
                return false;
            }
            if (child == target) {
                return false;
            }
            for (int i = 0; i < _children.Count; ++i) {
                if (_children[i] == target) {
                    if (i + 1 < _children.Count && _children[i + 1] == child) {
                        return false;
                    }
                    if (!_children.Remove(child)) {
                        ModFolder.Instance.Logger.Error("parent should contain the child at MoveChildAfterChild");
                        return false;
                    }
                    if (i > 0 && _children[i - 1] == target) {
                        _children.Insert(i, child);
                    }
                    else {
                        _children.Insert(i + 1, child);
                    }
                    return true;
                }
            }
            ModFolder.Instance.Logger.Error("parent should contain the target child at MoveChildAfterChild");
            return false;
        }
        /// <summary>
        /// 将自己删除, 而将所有子节点放入父节点的相应位置
        /// </summary>
        public void Crash() {
            if (Parent == null) {
                ModFolder.Instance.Logger.Error("root should not be crash at " + nameof(Crash));
                return;
            }
            var index = Parent._children.FindIndex(n => n == this);
            if (index == -1) {
                ModFolder.Instance.Logger.Error("parent should contain the child at " + nameof(Crash));
                return;
            }
            foreach (var child in _children) {
                child.ParentPublic = Parent;
            }
            Parent._children.RemoveAt(index);
            Parent._children.InsertRange(index, _children);
            _parent = null;
        }
        public void ClearChildren() => _children.Clear();
    }
    public class RootNode : FolderNode {
        [JsonConstructor]
        public RootNode() : base("Root") { }
        public RootNode(FolderNode folder) : base("Root") {
            _children = folder.ChildrenPublic;
            foreach (var child in folder.ChildrenPublic) {
                child.ParentPublic = this;
            }
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
    #endregion
    public static HashSet<string> Favorites { get; private set; } = [];
    public static Dictionary<string, ulong> PublishIds { get; private set; } = [];
    public static Dictionary<string, string> DisplayNames { get; private set; } = [];
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
            string? pathFromConfig = CommonConfig.Instance.DataSavePath;
            if (!string.IsNullOrWhiteSpace(pathFromConfig)) {
                try {
                    Directory.CreateDirectory(pathFromConfig);
                    return Path.Combine(pathFromConfig, "ModFolderData.json");
                }
                catch {
                    CommonConfig.Instance.DataSavePath = string.Empty;
                    CommonConfig.Instance.Save();
                }
            }
            Directory.CreateDirectory(ModOrganizer.modPath);
            return Path.Combine(ModOrganizer.modPath, "ModFolderData.json");
        }
    }
    #region 保存
    public static void Save() {
        File.WriteAllText(DataPath, JsonConvert.SerializeObject(_root));
    }
    public static void TrySaveWhenChanged() {
        if (CommonConfig.Instance.SaveWhenChanged) {
            Save();
        }
    }
    #endregion
    #region 加载
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
                childNode.Parent = folder;
            }
        }
    }
    #endregion
    public static bool RemoveRedundantData() {
        // TODO: 算法优化
        bool anyRemoved = false;
        HashSet<string> mods = [];
        foreach (var modNode in Root.ModNodesInTree) {
            mods.Add(modNode.ModName);
        }
        List<string> toRemoves = [];
        foreach (var mod in Favorites) {
            if (!mods.Contains(mod)) {
                toRemoves.Add(mod);
            }
        }
        if (toRemoves.Count != 0) {
            anyRemoved = true;
            foreach (var toRemove in toRemoves) {
                Favorites.Remove(toRemove);
            }
            toRemoves.Clear();
        }
        foreach (var mod in PublishIds.Keys) {
            if (!mods.Contains(mod)) {
                toRemoves.Add(mod);
            }
        }
        if (toRemoves.Count != 0) {
            anyRemoved = true;
            foreach (var toRemove in toRemoves) {
                PublishIds.Remove(toRemove);
            }
            toRemoves.Clear();
        }
        foreach (var mod in DisplayNames.Keys) {
            if (!mods.Contains(mod)) {
                toRemoves.Add(mod);
            }
        }
        if (toRemoves.Count != 0) {
            anyRemoved = true;
            foreach (var toRemove in toRemoves) {
                DisplayNames.Remove(toRemove);
            }
        }
        return anyRemoved;
    }
}
