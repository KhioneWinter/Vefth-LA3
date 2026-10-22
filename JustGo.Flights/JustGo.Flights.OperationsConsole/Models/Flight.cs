namespace JustGo.Flights.OperationsConsole.Models;

public class Flight
{
    public Guid Id {get;set;}
    public string FlightNumber {get;set;} = "";
    public string Origin {get;set;} = "";
    public string Destination {get;set;} ="";
    public DateTimeOffset DepartureUtc {get;set;}
    public string? Gate {get;set;}
    public FlightStatus Status {get;set;}
}