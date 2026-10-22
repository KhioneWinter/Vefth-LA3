namespace JustGo.Flights.Contracts;

public record BoardingCompleted(Guid FlightId, DateTimeOffset CompletedAt);
