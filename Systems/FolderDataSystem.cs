using ModFolder.Configs;
using ModFolder.UI.Menu;
using ModFolder.UI.UIFolderItems.Mods;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Runtime.InteropServices;
using Terraria.ModLoader.Core;
using Terraria.Social.Steam;

namespace ModFolder.Systems;

public static class FolderDataSystem {
    #region 类型
    [JsonObject(MemberSerialization.OptIn)]
    public abstract class Node {
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
                TreeChanged();
            }
        }
        /// <summary>
        /// <br/>不会自动调用 <see cref="TreeChanged"/>
        /// <br/>但仍然会处理父子关系
        /// </summary>
        public FolderNode? ParentF {
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

        public abstract DateTime LastModified { get; set; }

        /// <summary>
        /// 将自己挪到最顶上
        /// 返回是否有改变 (如果 <see cref="Parent"/> 为空或者本来就是第一位那么就不改变)
        /// </summary>
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
                    TreeChanged();
                    return true;
                }
            }
            children.Add(node);
            ModFolder.Instance.Logger.Error("parent should contain the child at MoveToTheTop");
            TreeChanged();
            return true;
        }
    }
    public class ModNode : Node {
        public ModNode(LocalMod mod) {
            ModName = mod.Name;
            if (WorkshopHelper.GetPublishIdLocal(mod.modFile, out ulong publishId)) {
                PublishId = publishId;
            }
            LastModified = mod.lastModified;
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
                    PublishIds[ModName] = value;
                }
            }
        }
        public override DateTime LastModified {
            get => LastModifieds.GetValueOrDefault(ModName);
            set {
                if (value == default) {
                    LastModifieds.Remove(ModName);
                }
                else {
                    LastModifieds[ModName] = value;
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
        [Obsolete($"using {nameof(FolderDataSystem)}.{nameof(ReceiveLocalModDataFromUI)} instead", true)]
        /// <summary>
        /// F for no DataChanged call
        /// </summary>
        public void ReceiveDataFromF(UIModItemInFolderLoaded uiMod) {
            ModName = uiMod.ModName;
            PublishId = uiMod.PublishId;
            LastModified = uiMod.LastModified;
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

        public override DateTime LastModified { get; set; }

        public int ChildrenCount { get; set; }
        public int EnabledCount { get; set; }
        public int DisabledCount => ChildrenCount - EnabledCount;
        public int ToEnableCount { get; set; }
        public int ToDisableCount { get; set; }
        public int EnableStatusRandomOffset { get; } = Main.rand.Next(0, 10000);

        #region Refresh Counts
        public static bool NeedToRefreshCounts => CommonConfig.Instance.ShowEnableStatusBackground || CommonConfig.Instance.ShowEnableStatus.ShowAny;
        public void RefreshCounts() {
            ChildrenCount = 0;
            EnabledCount = 0;
            ToEnableCount = 0;
            ToDisableCount = 0;
            foreach (var mod in UnduplicatedModNamesInTree) {
                RefreshCountsBy(mod);
            }
        }
        public void TryRefreshCounts() {
            if (NeedToRefreshCounts) {
                RefreshCounts();
            }
        }
        public void RefreshCountsInThisFolder() {
            foreach (var node in Children) {
                if (node is FolderNode folder) {
                    folder.RefreshCounts();
                }
            }
        }
        public void TryRefreshCountsInThisFolder() {
            if (NeedToRefreshCounts) {
                RefreshCountsInThisFolder();
            }
        }
        /// <summary>
        /// 按照参数刷新内容
        /// </summary>
        /// <param name="inThisFolder">刷新此文件夹内的文件夹, 不递归</param>
        /// <param name="inThePath">刷新此文件夹以及它的所有祖先</param>
        public void RefreshCounts(bool inThisFolder, bool inThePath) {
            // 不刷新路径上的文件夹, 仅刷新此文件夹内的文件夹时...
            if (!inThePath) {
                if (inThisFolder) {
                    RefreshCountsInThisFolder();
                }
                return;
            }
            // 需要刷新路径上的文件夹时...
            _pathModNamesCache.Clear();
            #region 刷新自己的内容, 同时将自己的内容放入路径模组缓存
            ChildrenCount = 0;
            EnabledCount = 0;
            ToEnableCount = 0;
            ToDisableCount = 0;
            if (!inThisFolder) {
                foreach (var mod in ModNodesInTree) {
                    var modName = mod.ModName;
                    if (_pathModNamesCache.Add(modName)) {
                        RefreshCountsBy(modName);
                    }
                }
            }
            else {
                foreach (var node in Children) {
                    if (node is ModNode m && _pathModNamesCache.Add(m.ModName)) {
                        RefreshCountsBy(m.ModName);
                    }
                    else if (node is FolderNode f) {
                        f.ChildrenCount = 0;
                        f.EnabledCount = 0;
                        f.ToEnableCount = 0;
                        f.ToDisableCount = 0;
                        foreach (var modName in f.UnduplicatedModNamesInTree) {
                            f.RefreshCountsBy(modName);
                            if (_pathModNamesCache.Add(modName)) {
                                RefreshCountsBy(modName);
                            }
                        }
                    }
                }
            }
            #endregion
            // 向上遍历自己的祖先, 刷新它们的内容
            for (FolderNode? folder = Parent, previous = this; folder != null; (folder, previous) = (folder.Parent, folder)) {
                // 归零
                folder.ChildrenCount = 0;
                folder.EnabledCount = 0;
                folder.ToEnableCount = 0;
                folder.ToDisableCount = 0;
                // 将下级缓存直接放入
                foreach (var mod in _pathModNamesCache) {
                    folder.RefreshCountsBy(mod);
                }
                // 将剩下的内容放入
                foreach (var node in folder.Children) {
                    if (node is ModNode m && _pathModNamesCache.Add(m.ModName)) {
                        folder.RefreshCountsBy(m.ModName);
                    }
                    else if (node is FolderNode f) {
                        if (f == previous)
                            continue;
                        foreach (var mod in f.ModNodesInTree) {
                            string modName = mod.ModName;
                            if (_pathModNamesCache.Add(modName)) {
                                folder.RefreshCountsBy(modName);
                            }
                        }
                    }
                }
            }
        }
        private void RefreshCountsBy(ModNode modNode) => RefreshCountsBy(modNode.ModName);
        private void RefreshCountsBy(string modName) {
            ChildrenCount += 1;
            if (ModLoader.modsByName.ContainsKey(modName)) {
                EnabledCount += 1;
                if (!ModLoader.EnabledMods.Contains(modName)) {
                    ToDisableCount += 1;
                }
            }
            else if (ModLoader.EnabledMods.Contains(modName)) {
                ToEnableCount += 1;
            }
        }
        #endregion

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

        public IEnumerable<string> UnduplicatedModNamesInTree {
            get {
                _unduplicatedModNamesCache.Clear();
                foreach (var modeNode in ModNodesInTree) {
                    var modName = modeNode.ModName;
                    if (_unduplicatedModNamesCache.Add(modName)) {
                        yield return modName;
                    }
                }
                _unduplicatedModNamesCache.Clear();
            }
        }
        private static readonly HashSet<string> _unduplicatedModNamesCache = [];
        private static readonly HashSet<string> _pathModNamesCache = [];
        #region 树操作
        public void AddChild(Node child) => child.Parent = this;
        public void SetChildAtTheTop(Node child) {
            child.ParentF = null;
            child.ParentPublic = this;
            _children.Insert(0, child);
            TreeChanged();
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
                    TreeChanged();
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
                    TreeChanged();
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
            TreeChanged();
        }
        public void ClearChildren() => _children.Clear();
        public void ClearChildrenF() => _children.Clear();
        #endregion
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
#pragma warning disable IDE0079 // 请删除不必要的忽略
#pragma warning disable CA1822 // 将成员标记为 static
#pragma warning disable IDE0051 // 删除未使用的私有成员
        [JsonProperty]
        private HashSet<string> Favorites => FolderDataSystem.Favorites;
        [JsonProperty]
        private Dictionary<string, ulong> PublishIds => FolderDataSystem.PublishIds;
        [JsonProperty]
        private Dictionary<string, string> DisplayNames => FolderDataSystem.DisplayNames;
        [JsonProperty]
        private Dictionary<string, string> ModAliases => FolderDataSystem.ModAliases;
        [JsonProperty]
        private Dictionary<string, DateTime> LastModifieds => FolderDataSystem.LastModifieds;
#pragma warning restore CA1822 // 将成员标记为 static
#pragma warning restore IDE0051 // 删除未使用的私有成员
#pragma warning restore IDE0079 // 请删除不必要的忽略
    }
    #endregion
    #region 树操作
    /// <summary>
    /// 需要确保所有节点的父节点相同, 且按顺序排列
    /// </summary>
    /// <param name="nodes"></param>
    public static void MoveNodesToTop(List<Node> nodes) {
        int nodesCount = nodes.Count;
        if (nodesCount == 0) {
            return;
        }
        var parent = nodes[0].Parent;
        if (parent == null) {
            return;
        }
        var children = parent.ChildrenPublic;
        int childrenCount = children.Count;
        if (childrenCount < nodesCount) {
            return; // 不应该
        }
        Queue<Node> cache = new(nodesCount);
        int cacheIndex = 0, i = 0;
        for (; i < nodesCount; ++i) {
            cache.Enqueue(children[i]);
            children[i] = nodes[i];
        }
                
        while (true) {
            var self = cache.Dequeue();
            while(self == children[cacheIndex] && cache.TryDequeue(out self)) {
                cacheIndex += 1;
            }
            if (self == null || i >= childrenCount) {
                break;
            }
            cache.Enqueue(children[i]);
            children[i] = self;
            i += 1;
        }
        TreeChanged();
    }
    /// <summary>
    /// 需要确保所有节点的父节点相同, 且按顺序排列
    /// </summary>
    /// <param name="nodes"></param>
    public static void MoveNodesAroundNode(List<Node> nodes, Node node, bool after = false) {
        int nodesCount = nodes.Count;
        if (nodesCount == 0) {
            return;
        }
        var parent = nodes[0].Parent;
        if (parent == null) {
            return;
        }
        var children = parent.ChildrenPublic;
        int childrenCount = children.Count;
        if (childrenCount < nodesCount) {
            return; // 不应该
        }
        //TODO: 算法优化
        int insertIndex = -1;
        int removeIndex = 0;
        int i;
        for (i = 0; i < childrenCount; ++i) {
            var item = children[i];
            if (item == node) {
                if (item == nodes[removeIndex]) {
                    insertIndex = i;
                    goto RemoveChild;
                }
                insertIndex = i + (after ? 1 : 0);
                continue;
            }
            if (removeIndex == nodes.Count || item != nodes[removeIndex]) {
                continue;
            }
        RemoveChild:
            children.RemoveAt(i);
            childrenCount -= 1;
            if (++removeIndex == nodesCount) {
                break;
            }
            i -= 1;
        }
        if (insertIndex == -1) {
            for(; i < childrenCount; ++i) {
                if (children[i] == node) {
                    insertIndex = i + (after ? 1 : 0);
                    break;
                }
            }
        }
        if (insertIndex == -1) {
            var removedSpan = nodes.ToSpan()[..removeIndex];
            if (after) {
                children.AddRange(removedSpan);
            }
            else {
                children.InsertRange(0, removedSpan);
            }
        }
        else {
            children.InsertRange(insertIndex, nodes.ToSpan()[..removeIndex]);
        }
        TreeChanged();
    }
    #endregion
    public static HashSet<string> Favorites { get; private set; } = [];
    public static Dictionary<string, ulong> PublishIds { get; private set; } = [];
    public static Dictionary<string, string> DisplayNames { get; private set; } = [];
    public static Dictionary<string, string> ModAliases { get; private set; } = [];
    public static Dictionary<string, DateTime> LastModifieds { get; private set; } = [];
    // 如果还要添加什么类似的东西:
    // - 在 RootNode 中添加对应的带有 [JsonProperty] 的属性
    // - 在 LoadRoot(...) 中添加其加载
    // - 在 RemoveRedundantData() 中添加
    // - 在 ShareHelper 中检查是否需要添加

    private static RootNode? _root;
    public static RootNode Root => _root ?? Reload();
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
        if (_root != null) {
            File.WriteAllText(DataPath, JsonConvert.SerializeObject(_root));
        }
    }
    public static void TrySaveWhenChanged() {
        if (CommonConfig.Instance.SaveWhenChanged) {
            Save();
        }
    }
    #endregion
    #region 加载
    public static void Clear() {
        Save();
        _root = null;
    }

    public static RootNode Reload() {
        Reload_Inner();
        return _root ??= new();
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
        if (data.TryGetValue(nameof(ModAliases), out var modAliasesToken)) {
            var modAliases = modAliasesToken.ToObject<Dictionary<string, string>>();
            if (modAliases != null) {
                ModAliases = modAliases;
            }
        }
        if (data.TryGetValue(nameof(LastModifieds), out var lastModifiedsToken)) {
            var lastModifieds = lastModifiedsToken.ToObject<Dictionary<string, DateTime>>();
            if (lastModifieds != null) {
                LastModifieds = lastModifieds;
            }
        }
        _root = LoadNode(data) is not FolderNode node ? new() : new(node);
    }
    public static Node? LoadNode(JObject data) {
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
            childNode?.ParentF = folder;
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
            toRemoves.Clear();
        }

        foreach (var mod in ModAliases.Keys) {
            if (!mods.Contains(mod)) {
                toRemoves.Add(mod);
            }
        }
        if (toRemoves.Count != 0) {
            anyRemoved = true;
            foreach (var toRemove in toRemoves) {
                ModAliases.Remove(toRemove);
            }
            toRemoves.Clear();
        }
        
        foreach (var mod in LastModifieds.Keys) {
            if (!mods.Contains(mod)) {
                toRemoves.Add(mod);
            }
        }
        if (toRemoves.Count != 0) {
            anyRemoved = true;
            foreach (var toRemove in toRemoves) {
                LastModifieds.Remove(toRemove);
            }
            toRemoves.Clear();
        }
        if (anyRemoved) {
            DataChanged();
        }
        return anyRemoved;
    }

    /// <summary>
    /// F for no DataChanged call
    /// </summary>
    /// <returns>has any been modified</returns>
    private static bool ReceiveLocalModDataFromUIF(UIModItemInFolderLoaded uiMod) {
        bool modified = false;
        var modName = uiMod.ModName;
        var publishId = uiMod.PublishId;
        if (publishId != 0) {
            ref var current = ref CollectionsMarshal.GetValueRefOrAddDefault(PublishIds, modName, out var exists);
            if (!exists || current != publishId) {
                current = publishId;
                modified = true;
            }
        }
        var lastModified = uiMod.LastModified;
        if (lastModified != default) {
            ref var current = ref CollectionsMarshal.GetValueRefOrAddDefault(LastModifieds, modName, out var exists);
            if (!exists || current != lastModified) {
                current = lastModified;
                modified = true;
            }
        }
        var displayName = uiMod.ModDisplayName;
        if (!string.IsNullOrEmpty(displayName)) {
            ref var current = ref CollectionsMarshal.GetValueRefOrAddDefault(DisplayNames, modName, out var exists);
            if (!exists || current != displayName) {
                current = displayName;
                modified = true;
            }
        }
        return modified;
    }
    public static void ReceiveLocalModDataFromUI(IEnumerable<UIModItemInFolderLoaded> uiMods) {
        bool modified = false;
        foreach (var uiMod in uiMods) {
            modified |= ReceiveLocalModDataFromUIF(uiMod);
        }
        if (modified) {
            DataChanged();
        }
    }

    public static void UpdateLastModified(FolderNode? folder = null) {
        folder ??= Root;
        DateTime latest = default;
        foreach (var child in folder.Children) {
            if (child is FolderNode folderChild) {
                UpdateLastModified(folderChild);
            }
            latest.ClampMinTo(child.LastModified);
        }
        folder.LastModified = latest;
    }

    public static void TreeChanged() {
        DataChanged();
        UIModFolderMenu.Instance.ArrangeGenerate();
    }
    public static void DataChanged() {
        if (CommonConfig.Instance.SaveWhenChanged) {
            Save();
        }
    }
}
