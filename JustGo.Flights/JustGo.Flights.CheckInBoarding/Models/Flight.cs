namespace JustGo.Flights.CheckInBoarding.Models;

public class Flight
{
    public Guid Id {get;set;}
    public DateTimeOffset? DepartureUtc {get;set;}
    public string? Gate {get;set;}
    public FlightStatus Status {get;set;}
}