using System.Text.Json.Serialization;
using ACRCBridge.Lib.Dto;

namespace ACRCBridge.App.LearnTrack;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Dictionary<string, TrackReferencePoints>))]
internal partial class LearnTrackJsonContext : JsonSerializerContext
{
}

