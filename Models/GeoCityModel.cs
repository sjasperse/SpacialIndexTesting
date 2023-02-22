namespace SpacialIndexing.Models;

public record GeoCityModel(
    int GeoCodeID,
    string City,
    string State,
    double Latitude,
    double Longitude
);
