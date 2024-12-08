namespace ModFolder.UI.Menu.Notification;

public class MenuNotificationsTracker {
    private readonly static List<IMenuNotification> _notifications = [];
    
    public static void Clear() {
        _notifications.Clear();
    }
    public static void AddNotification(IMenuNotification notification) {
        _notifications.Add(notification);
    }

    public static void Draw(SpriteBatch sb) {
        Vector2 position = new(Main.screenWidth - 20, Main.screenHeight - 20);
        foreach (var notification in _notifications) {
            notification.Draw(sb, position);
            notification.PushAnchor(ref position);
            if (position.Y < -100)
                break;
        }
    }

	public static void Update()
	{
		for (int i = 0; i < _notifications.Count; i++) {
			_notifications[i].Update();
			if (_notifications[i].ShouldBeRemoved) {
				_notifications.RemoveAt(i);
				i--;
			}
		}
	}
}
