global using static ModFolder.MyUtils;
using ReLogic.Content;
using Terraria.UI;

namespace ModFolder;

public static class MyUtils {
    public static class Textures {
        private static readonly Dictionary<string, Asset<Texture2D>> UITextures = [];
        public static Asset<Texture2D> UI(string name) {
            if (UITextures.TryGetValue(name, out var value)) {
                return value;
            }
            value = ModContent.Request<Texture2D>("ModFolder/Assets/Images/UI/" + name);
            UITextures.Add(name, value);
            return value;
        }
        public static readonly Texture2D White = FromColors(1, 1, [Color.White]);
        public static readonly Texture2D WhiteBox = FromColors(3, 3, [
            Color.White, Color.White      , Color.White,
            Color.White, Color.Transparent, Color.White,
            Color.White, Color.White      , Color.White,
        ]);
        public static readonly Asset<Texture2D> ButtonRename = ModContent.Request<Texture2D>("Terraria/Images/UI/ButtonRename");
        public static readonly Asset<Texture2D> ButtonDelete = ModContent.Request<Texture2D>("Terraria/Images/UI/ButtonDelete");

        public static Texture2D FromColors(int width, int height, Color[] colors) {
            Texture2D result = new(Main.instance.GraphicsDevice, width, height);
            result.SetData(colors);
            return result;
        }
    }

    #region Draw...
    public static void DrawBox(this SpriteBatch self, Rectangle destination, Color color, Color innerColor, int width = 1) {
        int size = Math.Min(destination.Width, destination.Height);
        if ((size + 1) / 2 <= width) {
            self.Draw(Textures.White, destination, color);
            return;
        }
        int dx1 = destination.X, dx2 = dx1 + width, dx3 = dx1 + destination.Width - width;
        int dy1 = destination.Y, dy2 = dy1 + width, dy3 = dy1 + destination.Height - width;
        int dw = destination.Width, ddx = dw - 2 * width, ddy = destination.Height - 2 * width;
        self.Draw(Textures.White, new Rectangle(dx1, dy1, dw, width), color);
        self.Draw(Textures.White, new Rectangle(dx1, dy3, dw, width), color);
        self.Draw(Textures.White, new Rectangle(dx1, dy2, width, ddy), color);
        self.Draw(Textures.White, new Rectangle(dx3, dy2, width, ddy), color);
        if (innerColor != default)
            self.Draw(Textures.White, new Rectangle(dx2, dy2, ddx, ddy), innerColor);
    }
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
            self.Draw(Textures.White, r, color);
        }
        start -= destination.Width;
        if (start > dashedInterval) {
            start -= dashed + dashedInterval;
        }
        for(; start < destination.Height - 2 * width; start += dashed + dashedInterval) {
            var r = NewRectangleByXY(destination.X + destination.Width - width, destination.Y + width + Math.Max(0, start),
                destination.X  + destination.Width, destination.Y + width + Math.Min(destination.Height - 2 * width, start + dashed));
            self.Draw(Textures.White, r, color);
        }
        start -= destination.Height - 2 * width;
        if (start > dashedInterval) {
            start -= dashed + dashedInterval;
        }
        for (; start < destination.Width; start += dashed + dashedInterval) {
            var r = NewRectangleByXY(destination.X + destination.Width - Math.Min(destination.Width, start + dashed), destination.Y + destination.Height - width,
                destination.X + destination.Width - Math.Max(0, start), destination.Y + destination.Height);
            self.Draw(Textures.White, r, color);
        }
        start -= destination.Width;
        if (start > dashedInterval) {
            start -= dashed + dashedInterval;
        }
        for(; start < destination.Height - 2 * width; start += dashed + dashedInterval) {
            var r = NewRectangleByXY(destination.X, destination.Y + destination.Height - width - Math.Min(destination.Height - 2 * width, start + dashed),
                destination.X  + width, destination.Y + destination.Height - width - Math.Max(0, start));
            self.Draw(Textures.White, r, color);
        }
        if (innerColor != default) {
            self.Draw(Textures.White, new Rectangle(destination.X + width, destination.Y + width, destination.Width - 2 * width, destination.Height - 2 * width), innerColor);
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

    public static void SaveTexture(Asset<Texture2D> texture, string path = "C:\\Users\\Administrator\\Documents\\My Games\\Terraria\\tModLoader\\Assets") {
        if (!texture.IsLoaded) {
            return;
        }
        string fullPath = Path.Join(path, texture.Name.Replace(".", "\\") + ".png");
        if (Path.Exists(fullPath)) {
            return;
        }
        string? directory = Path.GetDirectoryName(fullPath);
        if (directory != null) {
            Directory.CreateDirectory(directory);
        }
        using var file = File.Open(fullPath, FileMode.Create);
        texture.Value.SaveAsPng(file, texture.Width(), texture.Height());
    }
    public static void SaveTextureExtra(Asset<Texture2D> texture, string path = "C:\\Users\\Administrator\\Documents\\My Games\\Terraria\\tModLoader\\AssetsExtra") {
        SaveTexture(texture, path);
    }

    #region 堆排序
    public static List<T> HeapSort<T>(this List<T> list, Func<T, T, int> comparer) {
        HeapSortInner(list, comparer, 0, list.Count);
        return list;
    }
    private static void HeapSortInner<T>(this List<T> list, Func<T, T, int> comparer, int left, int right) {
        int length = right - left;
        if (length <= 1) {
            return;
        }
        if (length == 2) {
            if (comparer(list[left], list[left + 1]) > 0) {
                (list[left], list[left + 1]) = (list[left + 1], list[left]);
            }
            return;
        }
        if (length == 3) {
            if (comparer(list[left], list[left + 1]) <= 0) {
                if (comparer(list[left + 1], list[left + 2]) <= 0) {
                    return;
                }
                if (comparer(list[left], list[left + 2]) <= 0) {
                    (list[left + 2], list[left + 1]) = (list[left + 1], list[left + 2]);
                    return;
                }
                (list[left], list[left + 1], list[left + 2]) = (list[left + 2], list[left], list[left + 1]);
                return;
            }
            if (comparer(list[left], list[left + 2]) <= 0) {
                (list[left], list[left + 1]) = (list[left + 1], list[left]);
                return;
            }
            if (comparer(list[left + 1], list[left + 2]) > 0) {
                (list[left], list[left + 2]) = (list[left + 2], list[left]);
                return;
            }
            (list[left], list[left + 1], list[left + 2]) = (list[left + 1], list[left + 2], list[left]);
            return;
        }
        int middle = (left + right) / 2;
        HeapSortInner(list, comparer, left, middle);
        HeapSortInner(list, comparer, middle, right);
        T[] lefts = new T[middle - left];
        list.CopyTo(left, lefts, 0, middle - left);
        int indexLeft = 0, indexRight = middle;
        bool rightMoved = false;
        for (int index = left; index < right; ++index) {
            if (comparer(lefts[indexLeft], list[indexRight]) <= 0) {
                if (rightMoved) {
                    list[index] = lefts[indexLeft];
                }
                indexLeft += 1;
                if (indexLeft == middle - left) {
                    return;
                }
                continue;
            }
            rightMoved = true;
            list[index] = list[indexRight];
            indexRight += 1;
            if (indexRight == right) {
                for (; indexLeft < middle - left; ++indexLeft) {
                    list[++index] = lefts[indexLeft];
                }
                return;
            }
        }
    }
    #endregion

    public static void ReplaceChildren(this UIElement self, UIElement from, UIElement to, bool forceAdd) {
        
        for (int i = 0; i < self.Elements.Count; ++i) {
            if (self.Elements[i] == from) {
                from.Parent = null;
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
}
