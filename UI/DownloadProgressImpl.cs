using ModFolder.UI;
using System.Threading;
using System.Threading.Tasks;
using Terraria.ModLoader.Core;
using Terraria.ModLoader.UI.DownloadManager;
using Terraria.ModLoader.UI.ModBrowser;
using Terraria.Social.Steam;

namespace ModFolder.UI;

public class DownloadProgressImpl : IDownloadProgress {
    public ModDownloadItem ModDownloadItem { get; private init; }
    public bool Completed { get; private set; }
    public string? DisplayName { get; private set; }
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

    private bool started;
    public void TryStart() {
        if (started) {
            return;
        }
        started = true;
        // tartTime = UIModFolderMenu.Instance.Timer;
        // tartTimeRandomized = StartTime - Random.Shared.Next(100000);
        DownloadTask = Task.Run(() => {
            try {
                DownloadStarted(ModDownloadItem.DisplayName);
                Utils.LogAndConsoleInfoMessage(Language.GetTextValue("tModLoader.BeginDownload", ModDownloadItem.DisplayName));
                new SteamedWraps.ModDownloadInstance().Download(
                    new(ulong.Parse(ModDownloadItem.PublishId.m_ModPubId)),
                    this,
                    true /* mod.NeedUpdate || !SteamedWraps.IsWorkshopItemInstalled(publishId) */);
                DownloadSucceeded();
            }
            finally {
                DownloadCompleted();
            }
        });
    }

    public void DownloadStarted(string displayName) {
        DisplayName = displayName;
    }

    public void UpdateDownloadProgress(float progress, long bytesReceived, long totalBytesNeeded) {
        Progress = progress;
        BytesReceived = bytesReceived;
        TotalBytesNeeded = totalBytesNeeded;
    }

    public void DownloadCompleted() {
        Completed = true;
    }

    private static readonly object localModsChangedLock = new();

    public void DownloadSucceeded() {
        lock (localModsChangedLock) {
            ModOrganizer.LocalModsChanged([ModDownloadItem.ModName], isDeletion:false);
        }
        Thread.MemoryBarrier();
        UIModFolderMenu.Instance.ArrrangeRepopulate();
    }
}
