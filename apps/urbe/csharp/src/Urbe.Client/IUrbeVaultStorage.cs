using Urbe.Core;

namespace Urbe.Client;

/// <summary>Native-platform vault boundary. The Core remains the canonical parser/writer;
/// clients receive immutable snapshots, never filesystem or Android APIs.</summary>
public interface IUrbeVaultStorage
{
    bool IsConnected { get; }
    Task<VaultSnapshot?> PickAsync(CancellationToken cancellationToken = default);
    Task<VaultSnapshot?> RestoreAsync(CancellationToken cancellationToken = default);
    /// <summary>First safe slice: update one EXISTING Markdown file, with conflict checks,
    /// recovery copy, byte verification and no modifications to other vault entries.</summary>
    Task SaveExistingNoteAsync(string path, string content, CancellationToken cancellationToken = default);
    /// <summary>Create a new Markdown note in the selected vault root, never overwrite existing data.
    /// Returns an updated canonical snapshot only after filesystem verification.</summary>
    Task<VaultSnapshot> CreateNoteAsync(string path, string content, CancellationToken cancellationToken = default);
}
