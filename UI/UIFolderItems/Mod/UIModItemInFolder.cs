using ModFolder.Systems;
using ModFolder.UI.Menu;
using System.Diagnostics.CodeAnalysis;

namespace ModFolder.UI.UIFolderItems.Mod;

/// <summary>
/// 文件夹系统列表中的一个模组
/// </summary>
public abstract class UIModItemInFolder : UIFolderItem {
    #region 名字, 显示名与别名
    public abstract string ModName { get; }

    public abstract string ModDisplayName { get; }
    private string? _modDisplayNameClean;
    /// <summary>
    ///  No chat tags: for search and sort functionality.
    /// </summary>
    public string ModDisplayNameClean => _modDisplayNameClean ??= Utils.CleanChatTags(ModDisplayName);

    [MemberNotNullWhen(true, nameof(Alias))]
    public bool HasAlias => FolderDataSystem.ModAliases.ContainsKey(ModName);
    private string? _alias;
    public string? Alias {
        get {
            var alias = FolderDataSystem.ModAliases.TryGetValue(ModName, out var value) ? value : null;
            if (_alias == alias) {
                return _alias;
            }
            _alias = alias;
            _aliasClean = _alias != null ? Utils.CleanChatTags(_alias) : null;
            return _alias;
        }
    }
    private string? _aliasClean;
    public string? AliasClean {
        get {
            _ = Alias;
            return _aliasClean;
        }
    }

    public override string NameToSort => AliasClean ?? ModDisplayNameClean;

    protected override string GetRenameText() => Alias ?? ModDisplayName;
    protected override string GetRenameHintText() => ModDisplayNameClean;
    protected override bool TryRename(string newName) {
        var alias = Alias;
        var displayName = ModDisplayName;
        var modName = ModName;
        // 有别名的情况下...
        if (alias != null) {
            // 如果取名与别名相同, 则没有变化, 直接返回.
            if (alias == newName) {
                return false;
            }
            // 如果取名为空或与原名相同, 则去除别名.
            if (string.IsNullOrEmpty(newName) || newName == displayName) {
                FolderDataSystem.ModAliases.Remove(modName);
            }
            // 否则设置新别名
            else {
                FolderDataSystem.ModAliases[modName] = newName;
            }
            goto NameChanged;
        }
        // 在没有别名的情况下, 若取名为空或与原名相同则没有变化直接返回, 否则设置新别名.
        if (string.IsNullOrEmpty(newName) || newName == displayName) {
            return false;
        }
        FolderDataSystem.ModAliases[modName] = newName;

    NameChanged:
        UIModFolderMenu.Instance.ArrangeGenerate();
        FolderDataSystem.DataChanged();
        return true;
    }
    #endregion
    #region Draw
    public override void DrawSelf(SpriteBatch spriteBatch) {
        base.DrawSelf(spriteBatch);
        if (UIModFolderMenu.Instance.Downloads.TryGetValue(ModName, out var progress)) {
            DrawDownloadStatus(spriteBatch, progress);
        }
    }
    #region 画下载状态
    private void DrawDownloadStatus(SpriteBatch spriteBatch, DownloadProgressImpl progress) {
        Rectangle rectangle = GetDimensions().ToRectangle();
        Rectangle progressRectangle;
        Rectangle progressRectangleOuter;
        int size;
        if (BlockWithNameLayout) {
            progressRectangle = new(rectangle.X + 1, rectangle.Y + 1, rectangle.Width - 2, (int)((rectangle.Height - 2) * progress.Progress));
            progressRectangleOuter = new(rectangle.X, rectangle.Y, rectangle.Width, progressRectangle.Height + 2);
            size = rectangle.Height;
        }
        else {
            progressRectangle = new(rectangle.X + 1, rectangle.Y + 1, (int)((rectangle.Width - 2) * progress.Progress), rectangle.Height - 2);
            progressRectangleOuter = new(rectangle.X, rectangle.Y, progressRectangle.Width + 2, rectangle.Height);
            size = rectangle.Width;
        }

        spriteBatch.DrawBox(rectangle, Color.White * 0.5f);
        spriteBatch.Draw(MTextures.White, progressRectangle, Color.White * 0.2f);

        int timePassed = UIModFolderMenu.Instance.Timer - progress.CreateTimeRandomized;
        int realTimePassed = UIModFolderMenu.Instance.Timer - progress.CreateTime;
        int totalWidthToPass = size * 4;
        int goThroughWidth = size * 2 / 3;
        int passSpeed = 12 * size / 400;
        int end = timePassed * passSpeed % totalWidthToPass;
        if (end < 0) {
            end += totalWidthToPass;
        }
        if (end > realTimePassed * passSpeed) {
            return;
        }
        int start = end - goThroughWidth;
        
        DrawParallelogramByLayout(LayoutType, spriteBatch, rectangle, start, end, Color.White * 0.8f, default);
        DrawParallelogramByLayout(LayoutType, spriteBatch, progressRectangleOuter, start, end, default, Color.White * 0.3f);
    }
    #endregion
    #endregion
}
