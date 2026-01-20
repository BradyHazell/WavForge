using Velopack;
using Velopack.Sources;

namespace WavForge.Services;

internal sealed class UpdateService
{
    private readonly UpdateManager _manager;
    
    private VelopackAsset? _downloadedUpdate;

    public UpdateService(UpdateManager manager)
    {
        _manager = manager;
    }
    
    public bool IsUpdatePendingRestart => _manager.UpdatePendingRestart is not null;
    
    public async Task<bool> CheckAndDownloadInBackgroundAsync(CancellationToken ct = default)
    {
        if (_manager.UpdatePendingRestart is not null)
        {
            return true;
        }

        UpdateInfo? update = await _manager.CheckForUpdatesAsync();
        if (update is null)
        {
            return false;
        }

        await _manager.DownloadUpdatesAsync(update, cancelToken: ct);

        _downloadedUpdate = update;

        return true;
    }

    public void RestartToApplyUpdate()
    {
        VelopackAsset? update = _downloadedUpdate;

        // If app restarted since download, UpdatePendingRestart may still be true even if _downloadedUpdate is null.
        // You can still apply by calling WaitExitThenApplyUpdates with the same VelopackAsset you checked/downloaded.
        if (update is null)
        {
            return;
        }
        
        _manager.WaitExitThenApplyUpdates(update, restart: true, silent: false);
    }

    public void ApplyOnExitIfReady()
    {
        if (_downloadedUpdate is null)
        {
            return;
        }
        
        _manager.WaitExitThenApplyUpdates(_downloadedUpdate, restart: false, silent: false);
    }
}
