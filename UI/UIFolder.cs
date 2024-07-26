using Microsoft.Xna.Framework.Input;
using ModFolder.Systems;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.GameInput;
using Terraria.ModLoader.UI;
using Terraria.UI;

namespace ModFolder.UI;

/// <summary>
/// 文件夹系统列表中的一个文件夹
/// </summary>
public class UIFolder : UIFolderItem {
    public FolderDataSystem.FolderNode? Node;
    public string? Name { get; set; }
    // TODO
    public DateTime LastModified { get; set; }
    public override int CompareTo(object obj) {
        if (obj is UIModItemInFolder) {
            return -1;
        }
        if (obj is not UIFolder other) {
            return 1;
        }
        return UIModFolderMenu.Instance.sortMode switch {
            ModsMenuSortMode.RecentlyUpdated => other.LastModified.CompareTo(LastModified),
            ModsMenuSortMode.DisplayNameAtoZ => string.Compare(Name, other.Name, StringComparison.Ordinal),
            ModsMenuSortMode.DisplayNameZtoA => string.Compare(other.Name, Name, StringComparison.Ordinal),
            _ => base.CompareTo(obj),
        };
    }

    private UIImage _folderIcon = null!;
    private UIText _folderName = null!;
    private UIFocusInputTextFieldPro _renameText = null!;
    private UIImage _deleteButton =  null!;
    private UIImage? _renameButton;

    public UIFolder(FolderDataSystem.FolderNode folderNode) {
        Node = folderNode;
        Name = folderNode.FolderName;
    }
    public UIFolder(string name) {
        Name = name;
    }

    private void ReplaceChildren(UIElement from, UIElement to, bool forceAdd = true) {
        for (int i = 0; i < Elements.Count; ++i) {
            if (Elements[i] == from) {
                from.Parent = null;
                Elements[i] = to;
                to.Parent = this;
                to.Recalculate();
                return;
            }
        }
        if (forceAdd) {
            Append(to);
        }
    }
    private bool replaceToFolderName;
    private bool replaceToRenameText;
    public void SetReplaceToRenameText() => replaceToRenameText = true;

    public override void OnInitialize() {
        #region 删除按钮
        int rightRowOffset = -30;
        _deleteButton = new UIImage(TextureAssets.Trash) {
            Width = { Pixels = 24 },
            Height = { Pixels = 24 },
            Left = { Pixels = rightRowOffset, Precent = 1 },
            Top = { Pixels = -12, Percent = 0.5f },
            ScaleToFit = true,
            AllowResizingDimensions = false,
        };
        rightRowOffset -= 24;
        _deleteButton.OnLeftClick += (_, _) => {
            if (Node != null) {
                UIModFolderMenu.Instance.CurrentFolderNode.Children.Remove(Node);
                UIModFolderMenu.Instance.SetUpdateNeeded();
            }
        };
        Append(_deleteButton);
        #endregion
        #region 重命名按钮
        if (Node != null) {
            _renameButton = new UIImage(TextureAssets.Star[2]) {
                Width = { Pixels = 24 },
                Height = { Pixels = 24 },
                Left = { Pixels = rightRowOffset - 2, Precent = 1 },
                Top = { Pixels = -12, Percent = 0.5f },
                ScaleToFit = true,
                AllowResizingDimensions = false,
            };
            _renameButton.OnLeftClick += (_, _) => {
                replaceToRenameText = true;
            };
            Append(_renameButton);
        }
        rightRowOffset -= 24;
        #endregion
        #region 文件夹图标
        _folderIcon = new(UICommon.ButtonOpenFolder) {
            Left = { Pixels = 1 },
            Top = { Pixels = 1 },
            Width = { Pixels = 28 },
            Height = { Pixels = 28 },
            ScaleToFit = true,
            AllowResizingDimensions = false,
        };
        Append(_folderIcon);
        #endregion
        #region 名称
        _folderName = new(Name ?? string.Empty);
        _folderName.Left.Pixels = 30;
        _folderName.Top.Pixels = 7;
        Append(_folderName);
        #endregion
        #region 重命名输入框
        // TODO: 本地化
        _renameText = new("新名字");
        _renameText.Left.Pixels = 30;
        _renameText.Top.Pixels = 5;
        _renameText.Height.Set(-5, 1);
        _renameText.Width.Set(-30 + rightRowOffset, 1);
        _renameText.OnUnfocus += (_, _) => {
            var newName = _renameText.CurrentString;
            if (Node == null || newName == ".." || newName == string.Empty) {
                replaceToFolderName = true;
                return;
            }
            Node.FolderName = newName;
            Name = newName;
            _folderName.SetText(newName);
            replaceToFolderName = true;
        };
        _renameText.UnfocusOnTab = true;
        #endregion
        #region 双击进入文件夹
        OnLeftDoubleClick += (_, target) => {
            if (Name == null) {
                return;
            }
            if (Name == "..") {
                UIModFolderMenu.Instance.GotoUpperFolder();
            }
            UIModFolderMenu.Instance.EnterFolder(Name);
        };
        #endregion
    }

    private string? _tooltip;
    public override void Update(GameTime gameTime) {
        base.Update(gameTime);
    }
    public override void DrawSelf(SpriteBatch spriteBatch) {
        base.DrawSelf(spriteBatch);
        if (replaceToFolderName) {
            replaceToFolderName = false;
            ReplaceChildren(_renameText, _folderName, false);
        }
        if (replaceToRenameText) {
            replaceToRenameText = false;
            _renameText.CurrentString = string.Empty;
            ReplaceChildren(_folderName, _renameText, false);
            _renameText.Focused = true;
        }
        #region 当鼠标在某些东西上时显示些东西
        // 更多信息按钮
        // 删除按钮
        if (_deleteButton.IsMouseHovering) {
            _tooltip = Language.GetTextValue("UI.Delete");
        }
        else if (_renameButton?.IsMouseHovering == true) {
            // TODO: 本地化
            _tooltip = "重命名";
        }
        #endregion
    }
    public override void Draw(SpriteBatch spriteBatch) {
        _tooltip = null;
        base.Draw(spriteBatch);
        if (!string.IsNullOrEmpty(_tooltip)) {
            UICommon.TooltipMouseText(_tooltip);
        }
    }
}

public class UIFocusInputTextFieldPro(string hintText) : UIElement {
    public delegate void EventHandler(object sender, EventArgs e);
    public bool Focused;
    public string CurrentString = "";
    private readonly string _hintText = hintText;
    private int _textBlinkerCount;
    private int _textBlinkerState;

    public bool UnfocusOnTab {
        get;
        set;
    }
    public event EventHandler? OnTextChange;
    public event EventHandler? OnUnfocus;
    public event EventHandler? OnTab;
    public void SetText(string text) {
        text ??= "";

        if (CurrentString != text) {
            CurrentString = text;
            OnTextChange?.Invoke(this, new());
        }
    }

    public override void LeftClick(UIMouseEvent evt) {
        Main.clrInput();
        Focused = true;
    }

    public override void Update(GameTime gameTime) {
        Vector2 point = new(Main.mouseX, Main.mouseY);
        if (!ContainsPoint(point) && Main.mouseLeft) {
            Focused = false;
            OnUnfocus?.Invoke(this, new());
        }

        base.Update(gameTime);
    }
    private static bool JustPressed(Keys key) {
        if (Main.inputText.IsKeyDown(key)) {
            return !Main.oldInputText.IsKeyDown(key);
        }

        return false;
    }
    public override void DrawSelf(SpriteBatch spriteBatch) {
        if (Focused) {
            PlayerInput.WritingText = true;
            Main.instance.HandleIME();
            string inputText = Main.GetInputText(CurrentString);
            if (Main.inputTextEscape) {
                Main.inputTextEscape = false;
                Focused = false;
                OnUnfocus?.Invoke(this, new());
            }

            if (!inputText.Equals(CurrentString)) {
                CurrentString = inputText;
                OnTextChange?.Invoke(this, new());
            }
            else {
                CurrentString = inputText;
            }

            if (JustPressed(Keys.Tab)) {
                if (UnfocusOnTab) {
                    Focused = false;
                    OnUnfocus?.Invoke(this, new());
                }

                OnTab?.Invoke(this, new());
            }
            if (JustPressed(Keys.Enter)) {
                OnUnfocus?.Invoke(this, new());
            }
            if (++_textBlinkerCount >= 20) {
                _textBlinkerState = (_textBlinkerState + 1) % 2;
                _textBlinkerCount = 0;
            }
        }

        string text = CurrentString;
        if (_textBlinkerState == 1 && Focused) {
            text += "|";
        }

        CalculatedStyle dimensions = GetDimensions();
        if (CurrentString.Length == 0 && !Focused) {
            Utils.DrawBorderString(spriteBatch, _hintText, new Vector2(dimensions.X, dimensions.Y), Color.Gray);
        }
        else {
            Utils.DrawBorderString(spriteBatch, text, new Vector2(dimensions.X, dimensions.Y), Color.White);
        }
    }
}