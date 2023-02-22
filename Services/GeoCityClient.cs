using System.Diagnostics;
using System.Net.Http.Headers;
using System.Linq;
using System.Text.Json;
using Geohash.SpatialIndex.Core;
using NetTopologySuite.Geometries;
using SpacialIndexing.Models;
using NetTopologySuite;

namespace SpacialIndexing.Services;

public class GeoCityClient : IHostedService
{
    private readonly CancellationTokenSource initializationCanceller = new CancellationTokenSource();
    private readonly Task<IEnumerable<GeoCityModel>> geoCities;
    private readonly Task<GeohashSpatialIndex<GeoCityModel>> index;
    private readonly ILogger logger;
    private readonly GeometryFactory geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    public GeoCityClient(ILogger<GeoCityClient> logger)
    {
        geoCities = Task.Run(async () => {
            logger.LogInformation("GeoCity models loading...");
            var r = await GetGeoCitiesAsync(initializationCanceller.Token);
            logger.LogInformation("GeoCity models loaded");
            return r;
        });
        index = geoCities.ContinueWith(x => {
            logger.LogInformation("Index building...");
            var r = CreateIndex(x.Result, geometryFactory, initializationCanceller.Token);
            logger.LogInformation("Index built");

            return r;
        });
        this.logger = logger;
    }

    private static async Task<IEnumerable<GeoCityModel>> GetGeoCitiesAsync(CancellationToken cancellationToken)
    {
        var file = Path.Join("Data", "geocity.json");
        if (!File.Exists(file)) throw new FileNotFoundException(file);

        var geoCities = await JsonSerializer.DeserializeAsync<GeoCityModel[]>(File.OpenRead(file));
        ArgumentNullException.ThrowIfNull(geoCities);

        return geoCities;
    }

    private static GeohashSpatialIndex<GeoCityModel> CreateIndex(IEnumerable<GeoCityModel> geoCities, GeometryFactory geometryFactory, CancellationToken cancellationToken)
    {
        var index = new GeohashSpatialIndex<GeoCityModel>(
            new DefaultGeohasher(), 
            new DefaultTrieMap<GeoCityModel>(), 
            precision: 9);

        foreach (var geoCity in geoCities)
        {
            var point = geometryFactory.CreatePoint(new Coordinate(geoCity.Longitude, geoCity.Latitude));
            index.Insert(point, geoCity);
        }

        index.Query(geometryFactory.CreatePoint().Buffer(100));

        return index;
    }

    private static double MilesBetween(double lat1, double lon1, double lat2, double lon2)
    {
        double rlat1 = Math.PI*lat1/180;
        double rlat2 = Math.PI*lat2/180;
        double theta = lon1 - lon2;
        double rtheta = Math.PI*theta/180;

        // rounding because on linux systems (not mac, not win) for an 
        // identical point, comes out to 1.0000000000000002,
        // which causes the next arc-cosine call to come back as NaN
        double dist =
            Math.Round(
                Math.Sin(rlat1) * Math.Sin(rlat2) 
                    + Math.Cos(rlat1) * Math.Cos(rlat2) * Math.Cos(rtheta)
            , 10); 

        dist = Math.Acos(dist);
        dist = dist*180/Math.PI;
        dist = dist*60*1.1515;

        return dist;
    }

#region IHostedService
    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.Register(initializationCanceller.Cancel);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        initializationCanceller.Cancel();

        return Task.CompletedTask;
    }
#endregion IHostedService

    public Task<IEnumerable<GeoCityModel>> GetGeoCities()
        => geoCities;

    public async Task<GeoCityModel?> GetNearestByBruteForce(Coordinate coordinate)
    {
        return (await geoCities)
            .OrderBy(x => MilesBetween(x.Latitude, x.Longitude, coordinate.Y, coordinate.X))
            .FirstOrDefault();
    }

    public async Task<GeoCityModel?> GetNearestByIndex(Coordinate coordinate)
    {
        var searchArea = geometryFactory.CreatePoint(coordinate);
        var results = (await index)
            .Query(searchArea)!;

        if (!results.Any()) return null;
        
        return results
            .Select(x => x.Value)
            .OrderBy(x => MilesBetween(x.Latitude, x.Longitude, coordinate.Y, coordinate.X))
            .FirstOrDefault();
    }
}
