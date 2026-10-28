using JustGo.Flights.Contracts;
using JustGo.Flights.GroundOperations.Data;
using JustGo.Flights.GroundOperations.Models;
using Microsoft.EntityFrameworkCore;
using Rebus.Bus;
using Rebus.Handlers;

namespace JustGo.Flights.GroundOperations.Handlers;

public class TurnaroundEventHandler(GroundOpsDbContext db, IBus bus, ILogger<TurnaroundEventHandler> logger) :
    IHandleMessages<FlightScheduled>,
    IHandleMessages<GateAssigned>,
    IHandleMessages<FlightDelayed>,
    IHandleMessages<CheckInOpened>,
    IHandleMessages<BoardingStarted>,
    IHandleMessages<BoardingCompleted>,
    IHandleMessages<FlightDeparted>,
    IHandleMessages<FlightCancelled>
{
    public async Task Handle(FlightScheduled message)
    {
        var turnaround = await GetOrCreate(message.FlightId);
        if (turnaround.DepartureUtc is not null)
        {
            logger.LogInformation("FlightScheduled for {FlightId}: departure already known ({Departure}), nothing to do", message.FlightId, turnaround.DepartureUtc);
            return;
        }

        turnaround.DepartureUtc = message.DepartureTime;
        var created = CreateTasks(turnaround, message.DepartureTime);
        await db.SaveChangesAsync();
        logger.LogInformation("FlightScheduled for {FlightId}: departure set to {Departure}, created tasks: {Created}", message.FlightId, message.DepartureTime, created);
    }

    public async Task Handle(GateAssigned message)
    {
        var turnaround = await GetOrCreate(message.FlightId);
        if (IsFinal(turnaround))
        {
            logger.LogInformation("GateAssigned for {FlightId}: turnaround is {Status}, nothing to do", message.FlightId, turnaround.Status);
            return;
        }
        if (turnaround.Gate == message.Gate)
        {
            logger.LogInformation("GateAssigned for {FlightId}: gate already {Gate}, nothing to do", message.FlightId, message.Gate);
            return;
        }

        turnaround.Gate = message.Gate;
        await db.SaveChangesAsync();
        logger.LogInformation("GateAssigned for {FlightId}: gate set to {Gate}", message.FlightId, message.Gate);
    }

    public async Task Handle(FlightDelayed message)
    {
        var turnaround = await GetOrCreate(message.FlightId);

        // First event with a departure time: create the tasks from the delayed time
        if (turnaround.DepartureUtc is null)
        {
            turnaround.DepartureUtc = message.NewDepartureTime;
            CreateTasks(turnaround, message.NewDepartureTime);
        }
        else if (message.NewDepartureTime <= turnaround.DepartureUtc)
        {
            logger.LogInformation("FlightDelayed for {FlightId}: departure already {Departure}, nothing to do", message.FlightId, turnaround.DepartureUtc);
            return;
        }
        else
        {
            var difference = message.NewDepartureTime - turnaround.DepartureUtc.Value;
            turnaround.DepartureUtc = message.NewDepartureTime;

            // A finalized turnaround is locked, so its tasks are not moved
            if (turnaround.Status == TurnaroundStatus.Open)
            {
                foreach (var task in turnaround.Tasks.Where(t => t.Status == TurnaroundTaskStatus.Pending))
                    task.ScheduledUtc += difference;
            }
        }

        var pending = turnaround.Status == TurnaroundStatus.Open
            ? turnaround.Tasks.Where(t => t.Status == TurnaroundTaskStatus.Pending).ToList()
            : [];

        await db.SaveChangesAsync();

        if (pending.Count == 0)
        {
            logger.LogInformation("FlightDelayed for {FlightId}: departure moved to {Departure}, no pending tasks so nothing published", message.FlightId, message.NewDepartureTime);
            return;
        }

        var newTurnaroundTime = pending.Min(t => t.ScheduledUtc);
        await bus.Publish(new TurnaroundRescheduled(message.FlightId, newTurnaroundTime));
        logger.LogInformation("FlightDelayed for {FlightId}: departure moved to {Departure}, published TurnaroundRescheduled ({TurnaroundTime})", message.FlightId, message.NewDepartureTime, newTurnaroundTime);
    }

    public async Task Handle(CheckInOpened message)
    {
        var turnaround = await GetOrCreate(message.FlightId);
        if (IsFinal(turnaround) || turnaround.CrewAccessScheduled)
        {
            logger.LogInformation("CheckInOpened for {FlightId}: crew access already scheduled or turnaround is {Status}, nothing to do", message.FlightId, turnaround.Status);
            return;
        }

        turnaround.CrewAccessScheduled = true;
        await db.SaveChangesAsync();
        logger.LogInformation("CheckInOpened for {FlightId}: cleaning/catering crew access scheduled", message.FlightId);
    }

    public async Task Handle(BoardingStarted message)
    {
        var turnaround = await GetOrCreate(message.FlightId);
        if (turnaround.Status != TurnaroundStatus.Open)
        {
            logger.LogInformation("BoardingStarted for {FlightId}: turnaround is {Status}, nothing to do", message.FlightId, turnaround.Status);
            return;
        }

        turnaround.Status = TurnaroundStatus.Finalized;
        await db.SaveChangesAsync();
        logger.LogInformation("BoardingStarted for {FlightId}: turnaround set to Finalized", message.FlightId);
    }

    public async Task Handle(BoardingCompleted message)
    {
        var turnaround = await GetOrCreate(message.FlightId);
        var pending = turnaround.Tasks.Where(t => t.Status == TurnaroundTaskStatus.Pending).ToList();
        if (turnaround.Status == TurnaroundStatus.Cancelled || (pending.Count == 0 && turnaround.GateCleared))
        {
            logger.LogInformation("BoardingCompleted for {FlightId}: tasks already completed or turnaround is {Status}, nothing to do", message.FlightId, turnaround.Status);
            return;
        }

        foreach (var task in pending)
            task.Status = TurnaroundTaskStatus.Completed;
        turnaround.GateCleared = true;
        await db.SaveChangesAsync();
        logger.LogInformation("BoardingCompleted for {FlightId}: {Count} tasks completed, gate cleared for departure", message.FlightId, pending.Count);
    }

    public async Task Handle(FlightDeparted message)
    {
        var turnaround = await GetOrCreate(message.FlightId);
        if (IsFinal(turnaround))
        {
            logger.LogInformation("FlightDeparted for {FlightId}: turnaround is {Status}, nothing to do", message.FlightId, turnaround.Status);
            return;
        }

        turnaround.GateReleased = true;
        turnaround.Status = TurnaroundStatus.Closed;
        await db.SaveChangesAsync();
        logger.LogInformation("FlightDeparted for {FlightId}: gate released, turnaround set to Closed", message.FlightId);
    }

    public async Task Handle(FlightCancelled message)
    {
        var turnaround = await GetOrCreate(message.FlightId);
        if (IsFinal(turnaround))
        {
            logger.LogInformation("FlightCancelled for {FlightId}: turnaround is {Status}, nothing to do", message.FlightId, turnaround.Status);
            return;
        }

        var pending = turnaround.Tasks.Where(t => t.Status == TurnaroundTaskStatus.Pending).ToList();
        foreach (var task in pending)
            task.Status = TurnaroundTaskStatus.Cancelled;
        turnaround.GateReleased = true;
        turnaround.Status = TurnaroundStatus.Cancelled;
        await db.SaveChangesAsync();
        logger.LogInformation("FlightCancelled for {FlightId}: {Count} tasks cancelled, gate released, turnaround set to Cancelled", message.FlightId, pending.Count);
    }

    private static bool IsFinal(Turnaround turnaround) =>
        turnaround.Status is TurnaroundStatus.Closed or TurnaroundStatus.Cancelled;

    // Tasks are only created once, and not for a turnaround that is already closed or cancelled
    private static bool CreateTasks(Turnaround turnaround, DateTimeOffset departure)
    {
        if (turnaround.Tasks.Count > 0 || IsFinal(turnaround))
            return false;

        turnaround.Tasks.Add(NewTask(TurnaroundTaskType.Fueling, departure.AddMinutes(-60)));
        turnaround.Tasks.Add(NewTask(TurnaroundTaskType.Catering, departure.AddMinutes(-45)));
        turnaround.Tasks.Add(NewTask(TurnaroundTaskType.Cleaning, departure.AddMinutes(-30)));
        return true;
    }

    private static TurnaroundTask NewTask(TurnaroundTaskType type, DateTimeOffset scheduledUtc) => new()
    {
        Id = Guid.NewGuid(),
        Type = type,
        ScheduledUtc = scheduledUtc,
        Status = TurnaroundTaskStatus.Pending
    };

    private async Task<Turnaround> GetOrCreate(Guid flightId)
    {
        var turnaround = await db.Turnarounds
            .Include(t => t.Tasks)
            .FirstOrDefaultAsync(t => t.Id == flightId);
        if (turnaround is null)
        {
            turnaround = new Turnaround { Id = flightId, Status = TurnaroundStatus.Open };
            db.Turnarounds.Add(turnaround);
        }
        return turnaround;
    }
}
