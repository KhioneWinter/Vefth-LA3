namespace JustGo.Flights.OperationsConsole.Models;

public record CreateFlightRequest(string FlightNumber, string Origin, string Destination, DateTimeOffset DepartureUtc);

