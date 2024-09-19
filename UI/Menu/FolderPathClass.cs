using System.Collections;
using ModFolder.UI.Base;
using FolderNode = ModFolder.Systems.FolderDataSystem.FolderNode;

namespace ModFolder.UI.Menu;

/// <summary>
/// 当 FolderPath 改变时同步修改 folderPathList 中的元素
/// </summary>
public class FolderPathClass : IList<FolderNode> {
    private static UIHorizontalList HList => UIModFolderMenu.Instance.folderPathList;
    private readonly List<FolderNode> data = [];
    public FolderNode this[int index] {
        get => data[index];
        set {
            data[index] = value;
            HList.Items[index].Parent = null;
            HList.Items[index] = new UIFolderPathItem(value);
            HList.MarkItemsModified();
        }
    }
    public int Count => data.Count;
    public bool IsReadOnly => false;
    public void Add(FolderNode item) {
        data.Add(item);
        HList.Items.Add(new UIFolderPathItem(item));
        HList.MarkItemsModified();
    }
    public void Insert(int index, FolderNode item) {
        data.Insert(index, item);
        HList.Items.Insert(index, new UIFolderPathItem(item));
        HList.MarkItemsModified();
    }

    public bool Remove(FolderNode item) {
        for (int i = Count - 1; i >= 0; i--) {
            if (data[i] == item) {
                RemoveAt(i);
                return true;
            }
        }
        return false;
    }
    public void RemoveAt(int index) {
        data.RemoveAt(index);
        HList.Items[index].Parent = null;
        HList.Items.RemoveAt(index);
        HList.MarkItemsModified();
    }
    public void RemoveRange(int index, int count) {
        data.RemoveRange(index, count);
        for (int i = index; i < index + count; ++i) {
            HList.Items[i].Parent = null;
        }
        HList.Items.RemoveRange(index, count);
        HList.MarkItemsModified();
    }
    public void Clear() {
        data.Clear();
        foreach (var item in HList.Items) {
            item.Parent = null;
        }
        HList.Items.Clear();
        HList.MarkItemsModified();
    }

    public bool Contains(FolderNode item) {
        return data.Contains(item);
    }
    public int IndexOf(FolderNode item) {
        return data.IndexOf(item);
    }

    public void CopyTo(FolderNode[] array, int arrayIndex) {
        data.CopyTo(array, arrayIndex);
    }
    public IEnumerator<FolderNode> GetEnumerator() {
        return data.GetEnumerator();
    }
    IEnumerator IEnumerable.GetEnumerator() {
        return data.GetEnumerator();
    }
}
