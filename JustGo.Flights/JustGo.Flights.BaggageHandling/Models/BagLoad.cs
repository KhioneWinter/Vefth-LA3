namespace JustGo.Flights.BaggageHandling.Models;

public class BagLoad
{
    public Guid Id {get;set;}
    public DateTimeOffset? LoadingTimeUtc {get;set;}
    public BagLoadStatus Status {get;set;}
}
