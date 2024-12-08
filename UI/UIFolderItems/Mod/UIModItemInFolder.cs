using ModFolder.Systems;
using ModFolder.UI.Base;
using ModFolder.UI.Menu;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;

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

    /// <summary>
    /// 考虑别名
    /// </summary>
    public virtual string GetModDisplayName() {
        return Alias ?? ModDisplayName;
    }
    #endregion
    #region 重命名
    protected void OnInitialize_ProcessName(UIText modName, UIFocusInputTextFieldPro renameText) {
        uiModName = modName;
        _modNameIndex = this.AppendAndGetIndex(uiModName);
        uiRenameText = renameText;
        uiRenameText.OnUnfocus += OnUnfocus_TryRename;
    }
    protected void OnInitialize_ProcessRenameButton(UIElement renameButton) {
        renameButton.OnLeftClick += (_, _) => SetReplaceToRenameText();
        Append(renameButton);
    }

    private bool replaceToModName;
    private bool replaceToRenameText;
    private void SetReplaceToRenameText() => replaceToRenameText = true;
    private UIText uiModName = null!;
    private UIFocusInputTextFieldPro uiRenameText = null!;
    private int _modNameIndex;
    private void CheckReplace() {
        if (replaceToModName) {
            replaceToModName = false;
            this.ReplaceChildrenByIndex(_modNameIndex, uiModName);
            UpdateUIModName();
        }
        if (replaceToRenameText) {
            replaceToRenameText = false;
            uiRenameText.CurrentString = Alias ?? ModDisplayName; // 这里不要用可能会被重写的 GetModDisplayName
            this.ReplaceChildrenByIndex(_modNameIndex, uiRenameText);
            uiRenameText.Focused = true;
        }
    }
    private void UpdateUIModName() => UpdateUIModName(GetModDisplayName());
    private void UpdateUIModName(string? name) {
        if (uiModName.Text == name) {
            return;
        }
        uiModName.SetText(name);
        RecalculateChildren();
    }
    private void OnUnfocus_TryRename(object sender, EventArgs e) {
        var newName = uiRenameText.CurrentString;
        replaceToModName = true;
        if (string.IsNullOrEmpty(newName) || newName == ModDisplayName) {
            FolderDataSystem.ModAliases.Remove(ModName);
        }
        else {
            FolderDataSystem.ModAliases[ModName] = newName;
        }
        UIModFolderMenu.Instance.ArrangeGenerate();
        FolderDataSystem.TrySaveWhenChanged();
    }
    #endregion
    public override void DrawSelf(SpriteBatch spriteBatch) {
        CheckReplace();
        base.DrawSelf(spriteBatch);
        if (UIModFolderMenu.Instance.Downloads.TryGetValue(ModName, out var progress)) {
            DrawDownloadStatus(spriteBatch, progress);
        }
    }
    #region 画下载状态
    private void DrawDownloadStatus(SpriteBatch spriteBatch, DownloadProgressImpl progress) {
        Rectangle rectangle = GetDimensions().ToRectangle();
        Rectangle progressRectangle = new(rectangle.X + 1, rectangle.Y + 1, (int)((rectangle.Width - 2) * progress.Progress), rectangle.Height - 2);
        Rectangle progressRectangleOuter = new(rectangle.X, rectangle.Y, progressRectangle.Width + 2, rectangle.Height);

        spriteBatch.DrawBox(rectangle, Color.White * 0.5f);
        spriteBatch.Draw(MTextures.White, progressRectangle, Color.White * 0.2f);

        int timePassed = UIModFolderMenu.Instance.Timer - progress.CreateTimeRandomized;
        int realTimePassed = UIModFolderMenu.Instance.Timer - progress.CreateTime;
        int totalWidthToPass = rectangle.Width * 3;
        int goThroughWidth = rectangle.Width * 2 / 3;
        int passSpeed = 12;
        int end = timePassed * passSpeed % totalWidthToPass;
        if (end < 0) {
            end += totalWidthToPass;
        }
        if (end > realTimePassed * passSpeed) {
            return;
        }
        int start = end - goThroughWidth;

        DrawParallelogram(spriteBatch, rectangle, start, end, Color.White * 0.8f, default);
        DrawParallelogram(spriteBatch, progressRectangleOuter, start, end, default, Color.White * 0.3f);
    }
    #endregion
}
