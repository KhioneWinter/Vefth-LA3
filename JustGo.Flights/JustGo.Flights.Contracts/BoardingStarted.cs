namespace JustGo.Flights.Contracts;

public record BoardingStarted(Guid FlightId, DateTimeOffset StartedAt);
