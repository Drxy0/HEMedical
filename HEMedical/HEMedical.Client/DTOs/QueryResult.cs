namespace HEMedical.Client.DTOs;

public record QueryResult(string MeasurementName, double Value, double StdDev, string UnitOfMeasurement);
