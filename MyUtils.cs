﻿global using static ModFolder.MyUtils;
using Microsoft.Xna.Framework.Input;
using ReLogic.Content;
using System.Runtime.InteropServices;
using System.Text;
using Terraria.ModLoader.Core;
using Terraria.UI;

namespace ModFolder;

public static class MyUtils {
    public static class MTextures {
        private static readonly Dictionary<string, Asset<Texture2D>> UITextures = [];
        public static Asset<Texture2D> UI(string name) {
            if (UITextures.TryGetValue(name, out var value)) {
                return value;
            }
            value = ModContent.Request<Texture2D>("ModFolder/Assets/Images/UI/" + name);
            UITextures.Add(name, value);
            return value;
        }

        public static Texture2D White => Textures.Colors.White.Value;
        private static readonly Asset<Texture2D> _whiteBox = AssetTextureFromColors(3, 3, [
            Color.White, Color.White      , Color.White,
            Color.White, Color.Transparent, Color.White,
            Color.White, Color.White      , Color.White,
        ]);
        public static Texture2D WhiteBox => _whiteBox.Value;
        public static readonly Asset<Texture2D> ButtonRename = UI("ButtonRename"); // ModContent.Request<Texture2D>("Terraria/Images/UI/ButtonRename");
        public static readonly Asset<Texture2D> ButtonDelete = UI("ButtonDelete"); // ModContent.Request<Texture2D>("Terraria/Images/UI/ButtonDelete");
        public static readonly Asset<Texture2D> ButtonSubscribe = UI("ButtonSubscribe");
        public static readonly Asset<Texture2D> ButtonExport = UI("ButtonExport");
        public static readonly Asset<Texture2D> Deprecated = UI("Deprecated");
        public static readonly Asset<Texture2D> Folder = UI("Folder");
        public static readonly Asset<Texture2D> FolderBack = UI("FolderBack");

        public static Texture2D FromColors(int width, int height, Color[] colors) {
            Texture2D result = new(Main.instance.GraphicsDevice, width, height);
            result.SetData(colors);
            return result;
        }
    }

    #region Draw...
    public static void DrawDashedOutline(this SpriteBatch self, Rectangle destination, Color color, Color innerColor = default, int width = 1, int dashed = 5, int dashedInterval = 3, int start = 0) {
        start %= dashed + dashedInterval;
        if (start > dashedInterval) {
            start -= dashed + dashedInterval;
        }
        else if (start <= -dashed) {
            start += dashed + dashedInterval;
        }
        for (; start < destination.Width; start += dashed + dashedInterval) {
            var r = NewRectangleByXY(destination.X + Math.Max(0, start), destination.Y,
                destination.X + Math.Min(destination.Width, start + dashed), destination.Y + width);
            self.Draw(MTextures.White, r, color);
        }
        start -= destination.Width;
        if (start > dashedInterval) {
            start -= dashed + dashedInterval;
        }
        for(; start < destination.Height - 2 * width; start += dashed + dashedInterval) {
            var r = NewRectangleByXY(destination.X + destination.Width - width, destination.Y + width + Math.Max(0, start),
                destination.X  + destination.Width, destination.Y + width + Math.Min(destination.Height - 2 * width, start + dashed));
            self.Draw(MTextures.White, r, color);
        }
        start -= destination.Height - 2 * width;
        if (start > dashedInterval) {
            start -= dashed + dashedInterval;
        }
        for (; start < destination.Width; start += dashed + dashedInterval) {
            var r = NewRectangleByXY(destination.X + destination.Width - Math.Min(destination.Width, start + dashed), destination.Y + destination.Height - width,
                destination.X + destination.Width - Math.Max(0, start), destination.Y + destination.Height);
            self.Draw(MTextures.White, r, color);
        }
        start -= destination.Width;
        if (start > dashedInterval) {
            start -= dashed + dashedInterval;
        }
        for(; start < destination.Height - 2 * width; start += dashed + dashedInterval) {
            var r = NewRectangleByXY(destination.X, destination.Y + destination.Height - width - Math.Min(destination.Height - 2 * width, start + dashed),
                destination.X  + width, destination.Y + destination.Height - width - Math.Max(0, start));
            self.Draw(MTextures.White, r, color);
        }
        if (innerColor != default) {
            self.Draw(MTextures.White, new Rectangle(destination.X + width, destination.Y + width, destination.Width - 2 * width, destination.Height - 2 * width), innerColor);
        }
    }
    #region Draw9Piece
    public static void Draw9Piece(this SpriteBatch self, Texture2D texture, Rectangle destination, Color color, int corner) => Draw9Piece(self, texture, null, destination, color, corner);
    public static void Draw9Piece(this SpriteBatch self, Texture2D texture, Rectangle? source, Rectangle destination, Color color, int corner) {
        Rectangle s = source ?? new(0, 0, texture.Width, texture.Height);
        int cor = corner;
        int sx1 = s.X, sx2 = s.X + cor, sx3 = s.X + s.Width - cor;
        int sy1 = s.Y, sy2 = s.Y + cor, sy3 = s.Y + s.Height - cor;
        int sdx = s.Width - 2 * cor, sdy = s.Height - 2 * cor;
        int dx1 = destination.X, dx2 = destination.X + cor, dx3 = destination.X + destination.Width - cor;
        int dy1 = destination.Y, dy2 = destination.Y + cor, dy3 = destination.Y + destination.Height - cor;
        int ddx = destination.Width - 2 * cor, ddy = destination.Height - 2 * cor;

        // 四角
        self.Draw(texture, dx1, dy1, cor, cor, sx1, sy1, cor, cor, color);
        self.Draw(texture, dx3, dy1, cor, cor, sx3, sy1, cor, cor, color);
        self.Draw(texture, dx1, dy3, cor, cor, sx1, sy3, cor, cor, color);
        self.Draw(texture, dx3, dy3, cor, cor, sx3, sy3, cor, cor, color);
        // 四边
        self.Draw(texture, dx2, dy1, ddx, cor, sx2, sy1, sdx, cor, color);
        self.Draw(texture, dx2, dy3, ddx, cor, sx2, sy3, sdx, cor, color);
        self.Draw(texture, dx1, dy2, cor, ddy, sx1, sy2, cor, sdy, color);
        self.Draw(texture, dx3, dy2, cor, ddy, sx3, sy2, cor, sdy, color);
        // 中心
        self.Draw(texture, dx2, dy2, ddx, ddy, sx2, sy2, sdx, sdy, color);
    }
    public static void Draw9PieceI(this SpriteBatch self, Texture2D texture, Texture2D textureInner, Rectangle destination, Color color, int corner) => Draw9PieceI(self, texture, null, textureInner, null, destination, color, corner);
    public static void Draw9PieceI(this SpriteBatch self, Texture2D texture, Rectangle? source, Texture2D textureInner, Rectangle? innerSource, Rectangle destination, Color color, int corner) {
        Rectangle s = source ?? new(0, 0, texture.Width, texture.Height);
        int cor = corner;
        int sx1 = s.X, sx2 = s.X + cor, sx3 = s.X + s.Width - cor;
        int sy1 = s.Y, sy2 = s.Y + cor, sy3 = s.Y + s.Height - cor;
        int sdx = s.Width - 2 * cor, sdy = s.Height - 2 * cor;
        int dx1 = destination.X, dx2 = destination.X + cor, dx3 = destination.X + destination.Width - cor;
        int dy1 = destination.Y, dy2 = destination.Y + cor, dy3 = destination.Y + destination.Height - cor;
        int ddx = destination.Width - 2 * cor, ddy = destination.Height - 2 * cor;

        // 四角
        self.Draw(texture, dx1, dy1, cor, cor, sx1, sy1, cor, cor, color);
        self.Draw(texture, dx3, dy1, cor, cor, sx3, sy1, cor, cor, color);
        self.Draw(texture, dx1, dy3, cor, cor, sx1, sy3, cor, cor, color);
        self.Draw(texture, dx3, dy3, cor, cor, sx3, sy3, cor, cor, color);
        // 四边
        self.Draw(texture, dx2, dy1, ddx, cor, sx2, sy1, sdx, cor, color);
        self.Draw(texture, dx2, dy3, ddx, cor, sx2, sy3, sdx, cor, color);
        self.Draw(texture, dx1, dy2, cor, ddy, sx1, sy2, cor, sdy, color);
        self.Draw(texture, dx3, dy2, cor, ddy, sx3, sy2, cor, sdy, color);
        // 中心
        self.Draw(textureInner, new Rectangle(dx2, dy2, ddx, ddy), innerSource, color);
    }
    #endregion
    public static void Draw(this SpriteBatch spriteBatch, Texture2D texture, int dx, int dy, int dw, int dh, int sx, int sy, int sw, int sh, Color color)
        => spriteBatch.Draw(texture, new Rectangle(dx, dy, dw, dh), new Rectangle(sx, sy, sw, sh), color);
    #endregion
    public static Rectangle NewRectangleByXY(int x, int y, int xMax, int yMax) {
        return new(x, y, xMax - x, yMax - y);
    }

    #region IEnumerable 拓展
    public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> self) {
        foreach (var i in self) {
            if (i is not null) {
                yield return i;
            }
        }
    }
    public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> self, Func<T, bool> predicate) {
        foreach (var i in self) {
            if (i is not null && predicate(i)) {
                yield return i;
            }
        }
    }
    #endregion

    public static void Reverse<T>(this IList<T> list) {
        int m = list.Count / 2;
        for (int i = 0; i < m; ++i) {
            (list[i], list[list.Count - i - 1]) = (list[list.Count - i - 1], list[i]);
        }
    }

    /// <summary>
    /// 返回被替换掉的元素
    /// </summary>
    /// <param name="remove">是否将 <paramref name="element"/> 从原父节点移除</param>
    public static UIElement ReplaceChildrenByIndex(this UIElement self, int index, UIElement element, bool remove = false) {
        if (remove) {
            if (element.Parent == self) {
                return element;
            }
            var result = self.Elements[index];
            result.Parent = null;
            element.Remove();
            self.Elements[index] = element;
            element.Parent = self;
            element.Recalculate();
            return result;
        }
        else {
            var result = self.Elements[index];
            if (result == element) {
                return element;
            }
            self.Elements[index] = element;
            element.Parent = self;
            element.Recalculate();
            return result;
        }
    }

    public static void ReplaceChildren(this UIElement self, UIElement from, UIElement to, bool forceAdd) {
        
        for (int i = 0; i < self.Elements.Count; ++i) {
            if (self.Elements[i] == from) {
                from.Parent = null;
                to.Remove();
                self.Elements[i] = to;
                to.Parent = self;
                to.Recalculate();
                return;
            }
        }
        if (forceAdd) {
            self.Append(to);
        }
    }
    public static int AppendAndGetIndex(this UIElement self, UIElement child) {
        int index = self.Elements.Count;
        self.Append(child);
        return index;
    }
    private static bool? _developerMode;
    public static bool IsTMLDeveloperMode => _developerMode ??= ModCompile.DeveloperMode;

    public static bool PressingAlt(this KeyboardState state) {
        return state.IsKeyDown(Keys.LeftAlt) || state.IsKeyDown(Keys.RightAlt);
    }

    public static bool ToBoolean(this int self) => self != 0;

    public static unsafe string? OpenExplorerToGetFolderPath(string? defaultPath) {
        byte* intPtr = nativefiledialog.Utf8EncodeNullable(defaultPath);
        nativefiledialog.nfdresult_t result = nativefiledialog.INTERNAL_NFD_PickFolder(intPtr, out var outPath2);
        Marshal.FreeHGlobal((IntPtr)intPtr);
        // freePtr 由 true 改为 false, 不然要崩
        // 虽然说 nativefiledialog.NFD_OpenDialog 是这么用的, 而且那个可以用
        // 就是不知道有没有内存泄漏的问题...
        string outPath = nativefiledialog.UTF8_ToManaged(outPath2, freePtr: false);
        if (result == nativefiledialog.nfdresult_t.NFD_OKAY)
            return outPath;
        return null;
    }
    public static string? OpenExplorerToGetFilePath(string filterList, string? defaultPath) {
		if (nativefiledialog.NFD_OpenDialog(filterList, defaultPath, out var outPath) == nativefiledialog.nfdresult_t.NFD_OKAY)
			return outPath;

		return null;
    }

    public static StringBuilder SharedStringBuilder { get; } = new();
}
