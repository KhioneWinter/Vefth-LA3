namespace JustGo.Flights.Notification.Models;

public class Notice
{
    public Guid Id {get;set;}
    public Guid FlightId {get;set;}
    public NoticeType Type {get;set;}
    // Only set for delay notices, a delay notice is unique per flight and new departure time
    public DateTimeOffset? NewDepartureUtc {get;set;}
    public string Reason {get;set;} = "";
    public string Message {get;set;} = "";
    public DateTimeOffset SentUtc {get;set;}
}
