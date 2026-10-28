namespace JustGo.Flights.GroundOperations.Models;

public class Turnaround
{
    public Guid Id {get;set;}
    public DateTimeOffset? DepartureUtc {get;set;}
    public string? Gate {get;set;}
    public bool GateCleared {get;set;}
    public bool GateReleased {get;set;}
    public bool CrewAccessScheduled {get;set;}
    public TurnaroundStatus Status {get;set;}
    public List<TurnaroundTask> Tasks {get;set;} = [];
}
