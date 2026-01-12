using ACRCBridge.Lib.Dto;

namespace ACRCBridge.Lib.Coordinates;

public class GeoConvertersCollection
{
    private readonly Dictionary<string, GeoConverter> _converters = new();

    public GeoConvertersCollection(IDictionary<string, TrackReferencePoints> tracks)
    {
        foreach (var (trackName, points) in tracks)
        {
            if (points.Point0 is null || points.Point1 is null)
            {
                throw new InvalidOperationException($"Track '{trackName}' must define Point0 and Point1.");
            }
        
            var converter = GeoConverter.FromTwoReferencePoints(points.Point0, points.Point1);
            _converters[trackName] = converter;
        }
    }
    
    public void AddConverter(string trackConfig, GeoConverter converter)
    {
        _converters[trackConfig] = converter;
    }

    public GeoConverter GetConverter(string trackName)
    {
        _converters.TryGetValue(trackName.Replace("%", ""), out var converter);
        return converter ??
               throw new NotSupportedException($"No coordinate converter available for track '{trackName}'.");
    }
}
