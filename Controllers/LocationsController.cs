using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using NetTopologySuite.Geometries;
using SpacialIndexing.Models;
using SpacialIndexing.Services;

namespace SpacialIndexing.Controllers;

public class LocationsController : ControllerBase
{
    private readonly GeoCityClient geoCityClient;

    public LocationsController(GeoCityClient geoCityClient)
    {
        this.geoCityClient = geoCityClient;
    }

    [HttpGet("api/locations")]
    public Task<IEnumerable<GeoCityModel>> Get()
    {
        return this.geoCityClient.GetGeoCities();
    }

    [HttpGet("api/locations/find")]
    public async Task<ActionResult<GeoCityModel?>> Find(
        [FromQuery, Required] double lat, 
        [FromQuery, Required] double lng, 
        [FromQuery] string method = "index")
    {
        var coordinate = new Coordinate(lng, lat);

        return method switch {
            "index" => await geoCityClient.GetNearestByIndex(coordinate),
            "brute" => await geoCityClient.GetNearestByBruteForce(coordinate),
            _ => this.BadRequest($"Method '{method}' not recognized")
        };
    }
}
