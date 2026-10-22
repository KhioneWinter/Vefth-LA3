namespace JustGo.Flights.Contracts;

public record FlightScheduled(Guid FlightId, string FlightNumber, string Origin, string Destination, DateTimeOffset DepartureTime);
