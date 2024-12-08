namespace ModFolder.UI.Menu.Notification;

public interface IMenuNotification {
	bool ShouldBeRemoved { get; }
	void Update();
	void Draw(SpriteBatch spriteBatch, Vector2 bottomRightPosition);
	void PushAnchor(ref Vector2 positionAnchor);
}
