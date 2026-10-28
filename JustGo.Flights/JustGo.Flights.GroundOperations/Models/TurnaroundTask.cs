namespace JustGo.Flights.GroundOperations.Models;

public class TurnaroundTask
{
    public Guid Id {get;set;}
    public Guid TurnaroundId {get;set;}
    public TurnaroundTaskType Type {get;set;}
    public DateTimeOffset ScheduledUtc {get;set;}
    public TurnaroundTaskStatus Status {get;set;}
}
