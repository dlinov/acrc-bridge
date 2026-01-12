using ACRCBridge.Lib.Dto;

namespace ACRCBridge.App.Configuration.Tracks;

internal sealed record TrackConfig(ReferencePoint Point0, ReferencePoint Point1)
{
    public TrackReferencePoints AsDto => new(Point0, Point1);
}
