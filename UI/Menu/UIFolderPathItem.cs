using ModFolder.Systems;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader.UI;
using Terraria.UI;

namespace ModFolder.UI.Menu;

public class UIFolderPathItem : UIElement {
    public FolderDataSystem.FolderNode FolderNode { get; private init; }
    public UIFolderPathItem(FolderDataSystem.FolderNode folder) {
        FolderNode = folder;
        OnLeftDoubleClick += (_, _) => {
            UIModFolderMenu.Instance.GotoUpperFolder(folder);
        };

        UIText text = new(folder.FolderName);
        text.Recalculate();
        var textDimensions = text.GetDimensions();
        Width.Pixels = textDimensions.Width + 8;
        PaddingLeft = PaddingRight = 4;
        Height.Percent = 1;
        text.VAlign = 0.5f;
        Append(text);
    }
    public override void DrawSelf(SpriteBatch spriteBatch) {
        var rectangle = _dimensions.ToRectangle();
        if (IsMouseHovering && (UIModFolderMenu.Instance.DraggingTarget == null || UIModFolderMenu.Instance.DraggingTo == this)) {
            spriteBatch.DrawBox(_dimensions.ToRectangle(), Color.White * 0.8f, Color.White * 0.2f);
        }
        //if (folder != Instance.CurrentFolderNode)
        spriteBatch.Draw(UICommon.DividerTexture.Value, new Rectangle(rectangle.X + rectangle.Width + 2, rectangle.Y, rectangle.Height, 2), null, Color.White,
            MathF.PI / 2, Vector2.Zero, SpriteEffects.None, 0);
    }
}
