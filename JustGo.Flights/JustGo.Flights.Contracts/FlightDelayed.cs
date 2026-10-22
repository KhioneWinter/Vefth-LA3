namespace JustGo.Flights.Contracts;

public record FlightDelayed(Guid FlightId, DateTimeOffset NewDepartureTime, string Reason);
