namespace ACRCBridge.Lib.Dto;

/// <summary>
/// Configuration binding model for a single track.
/// This is used instead of ValueTuples because the configuration binder doesn't reliably bind tuples.
/// </summary>
public record TrackReferencePoints(ReferencePoint Point0, ReferencePoint Point1);
