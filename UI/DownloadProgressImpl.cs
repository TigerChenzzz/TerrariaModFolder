using System.Threading;
using System.Threading.Tasks;
using Terraria.ModLoader.Core;
using Terraria.ModLoader.UI.DownloadManager;
using Terraria.ModLoader.UI.ModBrowser;
using Terraria.Social.Steam;

namespace ModFolder.UI;

public class DownloadProgressImpl(ModDownloadItem mod) : IDownloadProgress {
    public ModDownloadItem ModDownloadItem => mod;
    public bool Started { get; private set; }
    public bool Completed { get; private set; }
    public string ModName { get; } = mod.ModName;
    public string? DisplayName { get; private set; }
    public float Progress { get; private set; }
    public long BytesReceived { get; private set; }
    public long TotalBytesNeeded { get; private set; }
    public Task? DownloadTask { get; private set; }
    public int StartTime { get; private set; }
    public int StartTimeRandomized { get; private set; }

    public void TryStart() {
        if (Started) {
            return;
        }
        Started = true;
        DownloadTask = Task.Run(() => {
            try {
                DownloadStarted(mod.DisplayName);
                Utils.LogAndConsoleInfoMessage(Language.GetTextValue("tModLoader.BeginDownload", mod.DisplayName));
                new SteamedWraps.ModDownloadInstance().Download(
                    new(ulong.Parse(mod.PublishId.m_ModPubId)),
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
        StartTime = UIModFolderMenu.Instance.Timer;
        StartTimeRandomized = StartTime - Random.Shared.Next(100000);
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
            ModOrganizer.LocalModsChanged([ModName], isDeletion:false);
        }
        Thread.MemoryBarrier();
        UIModFolderMenu.Instance.ArrrangeRepopulate();
    }
}
