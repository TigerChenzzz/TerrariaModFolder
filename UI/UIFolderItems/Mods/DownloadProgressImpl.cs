using ModFolder.UI.Menu;
using System.Threading.Tasks;
using Terraria.ModLoader.UI.DownloadManager;
using Terraria.ModLoader.UI.ModBrowser;
using Terraria.Social.Steam;

namespace ModFolder.UI.UIFolderItems.Mods;

public class DownloadProgressImpl : IDownloadProgress {
    public ModDownloadItem ModDownloadItem { get; private init; }
    public bool Completed { get; private set; }
    public float Progress { get; private set; }
    public long BytesReceived { get; private set; }
    public long TotalBytesNeeded { get; private set; }
    public Task? DownloadTask { get; private set; }
    public int CreateTime { get; private init; }
    public int CreateTimeRandomized { get; private init; }

    public DownloadProgressImpl(ModDownloadItem mod) {
        ModDownloadItem = mod;
        CreateTime = UIModFolderMenu.Instance.Timer;
        CreateTimeRandomized = CreateTime - Random.Shared.Next(100000);
    }

    public event Action? OnDownloadSucceeded;
    public event Action? OnDownloadCompleted;

    private bool started;
    public bool Succeeded { get; private set; }
    public void TryStart() {
        if (started) {
            return;
        }
        started = true;
        DownloadTask = Task.Run(() => {
            try {
                DownloadStarted(ModDownloadItem.DisplayName);
                Utils.LogAndConsoleInfoMessage(Language.GetTextValue("tModLoader.BeginDownload", ModDownloadItem.DisplayName));
                new SteamedWraps.ModDownloadInstance().Download(
                    new(ulong.Parse(ModDownloadItem.PublishId.m_ModPubId)),
                    this,
                    true /* mod.NeedUpdate || !SteamedWraps.IsWorkshopItemInstalled(publishId) */);
                Succeeded = true;
                OnDownloadSucceeded?.Invoke();
            }
            finally {
                Completed = true;
                OnDownloadCompleted?.Invoke();
            }
        });
    }
    
    public void DownloadStarted(string displayName) {
#if DEBUG
        UIModFolderMenu.PopupInfo("Download started: " + displayName);
#endif
    }

    public void UpdateDownloadProgress(float progress, long bytesReceived, long totalBytesNeeded) {
        Progress = progress;
        BytesReceived = bytesReceived;
        TotalBytesNeeded = totalBytesNeeded;
    }
}
