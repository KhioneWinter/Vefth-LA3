namespace JustGo.Flights.Contracts;

public record CheckInOpened(Guid FlightId, DateTimeOffset OpenedAt);
