using ReLogic.Graphics;
using System.Diagnostics.CodeAnalysis;
using Terraria.GameContent;
using Terraria.UI;

namespace ModFolder.Systems;

#if DEBUG
public class TestSystem : ModSystem {
    #region data
    private static readonly Dictionary<string, object> data = [];
    public static Dictionary<string, object> Data => data;

    private class DataOfSpecialType<T> {
        public static Dictionary<string, T> data = [];
    }
    public static T? TryGetData<T>(string key) {
        DataOfSpecialType<T>.data.TryGetValue(key, out T? value);
        return value;
    }

    public static T GetOrAddData<T>(string key, Func<T> valueToAdd) {
        if (DataOfSpecialType<T>.data.TryGetValue(key, out T? value)) {
            return value;
        }
        value = valueToAdd();
        SetData(key, value);
        return value;
    }
    [return: NotNullIfNotNull(nameof(defaultValue))]
    public static T? TryGetData<T>(string key, T? defaultValue) => DataOfSpecialType<T>.data.TryGetValue(key, out T? value) ? value : defaultValue;
    public static T GetData<T>(string key) => DataOfSpecialType<T>.data[key];
    public static void GetData<T>(string key, out T value) => value = DataOfSpecialType<T>.data[key];
    public static void SetData<T>(string key, T value) => DataOfSpecialType<T>.data[key] = value;
    public static void ClearData<T>() => DataOfSpecialType<T>.data.Clear();
    public static void RemoveData<T>(string key) => DataOfSpecialType<T>.data.Remove(key);
    #endregion
    #region Update
    public override void PreUpdatePlayers() {
        if (doOnceFlag) {
            doOnceFlag = !doOnceFlag;
            DoOnce();
        }
    }
    public override void PostUpdatePlayers() {

    }
    public override void PostUpdateInput() {

    }
    public override void PostUpdateEverything() {

    }
    public override void UpdateUI(GameTime gameTime) {

    }
    #endregion

    #region DoOnce
    private static bool doOnceFlag;
    private static void DoOnce() {

    }
    #endregion

    #region Draw
    private static void Draw(SpriteBatch spriteBatch) {
        _ = spriteBatch;
    }
    public static string TestText { get; set; } = "";
    private static void DrawUI(SpriteBatch spriteBatch) {
        #region set and draw test text
        TestText = $"""

            """;
        var size = FontAssets.MouseText.Value.MeasureString(TestText);
        spriteBatch.DrawString(FontAssets.MouseText.Value, TestText, new Vector2(10, Main.screenHeight - 10), Color.White, 0f, new Vector2(0, size.Y), 1f, default, 0f);
        #endregion
    }

    public static void DrawLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, float width = 1, Color? color = null) {
        color ??= Color.White;
        float distance = Vector2.Distance(start, end);
        Vector2 scale = new(distance, width);
        float rotation = (end - start).ToRotation();
        Vector2 origin = new(0, 0.5f);
        spriteBatch.Draw(DummyTexture, start, null, color.Value, rotation, origin, scale, SpriteEffects.None, 0);
    }
    public static void DrawLineInWorld(SpriteBatch spriteBatch, Vector2 start, Vector2 end, float width = 1, Color? color = null) {
        DrawLine(spriteBatch, start - Main.screenPosition, end - Main.screenPosition, width, color);
    }
    public static void DrawRectWithCenter(SpriteBatch spriteBatch, Vector2 center, float width = 1, float height = 1, float rotation = 0f, Color? color = null) {
        color ??= Color.White;
        spriteBatch.Draw(DummyTexture, center, null, color.Value, rotation, new Vector2(0.5f), new Vector2(width, height), SpriteEffects.None, 0);
    }
    public static void DrawRect(SpriteBatch spriteBatch, Vector2 position, float width = 1, float height = 1, float rotation = 0f, Color? color = null) {
        color ??= Color.White;
        spriteBatch.Draw(DummyTexture, position, null, color.Value, rotation, Vector2.Zero, new Vector2(width, height), SpriteEffects.None, 0);
    }
    #endregion

    #region 实现
    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
        int MouseTextIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
        if (MouseTextIndex != -1) {
            layers.Insert(MouseTextIndex, new LegacyGameInterfaceLayer(
               ModName + ": Test System",
               delegate {
                   Draw(Main.spriteBatch);
                   return true;
               },
               InterfaceScaleType.Game)
           );
            layers.Insert(MouseTextIndex, new LegacyGameInterfaceLayer(
               ModName + ": Test System UI",
               delegate {
                   DrawUI(Main.spriteBatch);
                   return true;
               },
               InterfaceScaleType.UI)
           );
        }
    }
    #endregion

    public static string ModName => nameof(ModFolder);
    #region Dummy Texture
    private static Texture2D? _dummyTexture;
    /// <summary>
    /// A 1x1 pixel white texture.
    /// </summary>
    public static Texture2D DummyTexture {
        get {
            if (_dummyTexture == null) {
                _dummyTexture = new Texture2D(Main.instance.GraphicsDevice, 1, 1);
                _dummyTexture.SetData(new Color[] { Color.White });
            }
            return _dummyTexture;
        }
    }
    #endregion
}
#endif