namespace JustGo.Flights.OperationsConsole.Models;

public record DelayFlightRequest(DateTimeOffset NewDepartureUtc, string Reason);