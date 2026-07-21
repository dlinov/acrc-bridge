using ACRCBridge.Lib.Dto;

namespace ACRCBridge.Lib.AssettoCorsa.SharedMemory;

/// <summary>
/// Provides on-demand snapshots of Assetto Corsa shared-memory physics data.
/// Implementations must be safe for single-reader use.
/// </summary>
public interface IPhysicsSnapshotSource
{
    /// <summary>
    /// Attempts to read the latest physics snapshot.
    /// </summary>
    /// <param name="extras">The populated snapshot when the call succeeds; otherwise the default value.</param>
    /// <returns><c>true</c> when a snapshot was read; <c>false</c> when the shared memory is unavailable.</returns>
    bool TryGetSnapshot(out AcPhysicsExtras extras);
}
