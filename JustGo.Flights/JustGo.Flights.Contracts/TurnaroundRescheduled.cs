namespace JustGo.Flights.Contracts;

public record TurnaroundRescheduled(Guid FlightId, DateTimeOffset NewTurnaroundTime);
