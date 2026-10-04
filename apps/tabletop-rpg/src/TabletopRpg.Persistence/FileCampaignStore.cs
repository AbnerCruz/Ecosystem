using System.Collections.Concurrent;
using TabletopRpg.Core;

namespace TabletopRpg.Persistence;

public enum CampaignLoadSource
{
    Primary,
    Backup
}

public sealed record CampaignLoadResult(
    Campaign Campaign,
    CampaignLoadSource Source,
    bool RepairedPrimary);

public sealed class CampaignRecoveryException : Exception
{
    public CampaignRecoveryException(string message, Exception? primaryError, Exception? backupError)
        : base(message, backupError ?? primaryError)
    {
        PrimaryError = primaryError;
        BackupError = backupError;
    }

    public Exception? PrimaryError { get; }
    public Exception? BackupError { get; }
}

public sealed class FileCampaignStore
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> DirectoryGates =
        new(StringComparer.Ordinal);

    private readonly string _rootDirectory;
    private readonly CampaignCodec _codec;
    private readonly SemaphoreSlim _ioGate;

    public FileCampaignStore(string rootDirectory, CampaignCodec? codec = null)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
            throw new ArgumentException("Root directory is required.", nameof(rootDirectory));

        _rootDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootDirectory));
        _codec = codec ?? new CampaignCodec();
        _ioGate = DirectoryGates.GetOrAdd(_rootDirectory, static _ => new SemaphoreSlim(1, 1));
    }

    public string RootDirectory => _rootDirectory;

    public async Task SaveAsync(
        Campaign campaign,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(campaign);

        await _ioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await SaveCoreAsync(campaign, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ioGate.Release();
        }
    }

    public async Task<CampaignLoadResult> LoadAsync(
        CampaignId campaignId,
        CancellationToken cancellationToken = default)
    {
        if (campaignId.Value == Guid.Empty)
            throw new ArgumentException("Campaign id cannot be empty.", nameof(campaignId));

        await _ioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await LoadCoreAsync(campaignId, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ioGate.Release();
        }
    }

    public Task ExportAsync(
        Campaign campaign,
        Stream destination,
        CancellationToken cancellationToken = default) =>
        _codec.ExportAsync(campaign, destination, cancellationToken);

    public async Task<Campaign> ImportAsync(
        Stream source,
        bool saveToStore = true,
        CancellationToken cancellationToken = default)
    {
        var campaign = await _codec.ImportAsync(source, cancellationToken).ConfigureAwait(false);
        if (saveToStore)
            await SaveAsync(campaign, cancellationToken).ConfigureAwait(false);

        return campaign;
    }

    private async Task SaveCoreAsync(
        Campaign campaign,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_rootDirectory);

        var primaryPath = PrimaryPath(campaign.Id);
        var backupPath = BackupPath(campaign.Id);
        var bytes = _codec.Encode(campaign);

        if (File.Exists(primaryPath))
        {
            try
            {
                var previous = await File.ReadAllBytesAsync(primaryPath, cancellationToken).ConfigureAwait(false);
                var previousCampaign = _codec.Decode(previous);
                EnsureExpectedCampaign(campaign.Id, previousCampaign);

                await WriteAtomicallyAsync(
                    backupPath,
                    previous,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (CampaignFormatException)
            {
                // Never overwrite a known-good backup with a corrupt primary.
            }
        }

        await WriteAtomicallyAsync(primaryPath, bytes, cancellationToken).ConfigureAwait(false);
    }

    private async Task<CampaignLoadResult> LoadCoreAsync(
        CampaignId campaignId,
        CancellationToken cancellationToken)
    {
        var primaryPath = PrimaryPath(campaignId);
        var backupPath = BackupPath(campaignId);

        Exception? primaryError = null;
        if (File.Exists(primaryPath))
        {
            try
            {
                var primaryBytes = await File.ReadAllBytesAsync(primaryPath, cancellationToken).ConfigureAwait(false);
                var campaign = _codec.Decode(primaryBytes);
                EnsureExpectedCampaign(campaignId, campaign);
                return new CampaignLoadResult(campaign, CampaignLoadSource.Primary, false);
            }
            catch (Exception exception) when (IsRecoverableReadFailure(exception))
            {
                primaryError = exception;
            }
        }

        Exception? backupError = null;
        if (File.Exists(backupPath))
        {
            try
            {
                var backupBytes = await File.ReadAllBytesAsync(backupPath, cancellationToken).ConfigureAwait(false);
                var recovered = _codec.Decode(backupBytes);
                EnsureExpectedCampaign(campaignId, recovered);

                await WriteAtomicallyAsync(primaryPath, backupBytes, cancellationToken).ConfigureAwait(false);
                return new CampaignLoadResult(recovered, CampaignLoadSource.Backup, true);
            }
            catch (Exception exception) when (IsRecoverableReadFailure(exception))
            {
                backupError = exception;
            }
        }

        if (!File.Exists(primaryPath) && !File.Exists(backupPath))
            throw new FileNotFoundException($"Campaign {campaignId} was not found.", primaryPath);

        throw new CampaignRecoveryException(
            $"Campaign {campaignId} could not be loaded from primary or backup.",
            primaryError,
            backupError);
    }

    private string PrimaryPath(CampaignId campaignId) =>
        Path.Combine(_rootDirectory, $"{campaignId.Value:N}.campaign.json");

    private string BackupPath(CampaignId campaignId) =>
        PrimaryPath(campaignId) + ".bak";

    private static async Task WriteAtomicallyAsync(
        string destinationPath,
        ReadOnlyMemory<byte> bytes,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(destinationPath)
            ?? throw new InvalidOperationException("Destination directory is unavailable.");
        Directory.CreateDirectory(directory);

        var tempPath = destinationPath + ".tmp";

        try
        {
            var options = new FileStreamOptions
            {
                Access = FileAccess.Write,
                Mode = FileMode.Create,
                Share = FileShare.None,
                BufferSize = 4096,
                Options = FileOptions.Asynchronous | FileOptions.WriteThrough
            };

            await using (var stream = new FileStream(tempPath, options))
            {
                await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            File.Move(tempPath, destinationPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }

    private static void EnsureExpectedCampaign(CampaignId expectedId, Campaign campaign)
    {
        if (campaign.Id != expectedId)
            throw new CampaignFormatException(
                $"Campaign file identity mismatch: expected {expectedId}, found {campaign.Id}.");
    }

    private static bool IsRecoverableReadFailure(Exception exception) =>
        exception is CampaignFormatException
            or IOException
            or UnauthorizedAccessException;
}
