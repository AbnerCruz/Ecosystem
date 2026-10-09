using Urbe.Core;

namespace Urbe.UI;

/// <summary>
/// Host filesystem boundary. Urbe.UI never knows about Android SAF, OS paths,
/// WebView storage or a second canonical database for the user's documents.
/// </summary>
public interface IVaultHost
{
    bool IsAvailable { get; }
    bool IsConnected { get; }
    string? DisplayName { get; }

    Task<VaultSnapshot?> PickAsync(CancellationToken cancellationToken = default);
    Task<VaultSnapshot?> RestoreAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(WorkspaceSession session, CancellationToken cancellationToken = default);
}

/// <summary>
/// Web preview cannot silently pretend its in-memory session is persisted.
/// A separate browser folder adapter can replace this registration at UC-23.
/// </summary>
public sealed class PreviewVaultHost : IVaultHost
{
    public bool IsAvailable => false;
    public bool IsConnected => false;
    public string? DisplayName => null;

    public Task<VaultSnapshot?> PickAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<VaultSnapshot?>(null);

    public Task<VaultSnapshot?> RestoreAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<VaultSnapshot?>(null);

    public Task SaveAsync(WorkspaceSession session, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Esta prévia Web ainda não possui acesso a uma pasta física.");
}
