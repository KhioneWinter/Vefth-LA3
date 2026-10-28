using JustGo.Flights.BaggageHandling.Data;
using JustGo.Flights.BaggageHandling.Models;
using JustGo.Flights.Contracts;
using Rebus.Handlers;

namespace JustGo.Flights.BaggageHandling.Handlers;

public class BagLoadEventHandler(BaggageDbContext db, ILogger<BagLoadEventHandler> logger) :
    IHandleMessages<TurnaroundRescheduled>,
    IHandleMessages<BoardingStarted>,
    IHandleMessages<BoardingCompleted>,
    IHandleMessages<FlightCancelled>
{
    public async Task Handle(TurnaroundRescheduled message)
    {
        var bagLoad = await GetOrCreate(message.FlightId);
        if (IsFinal(bagLoad) || bagLoad.LoadingTimeUtc >= message.NewTurnaroundTime)
        {
            logger.LogInformation("TurnaroundRescheduled for {FlightId}: loading time already {LoadingTime} or bag load is {Status}, nothing to do", message.FlightId, bagLoad.LoadingTimeUtc, bagLoad.Status);
            return;
        }

        bagLoad.LoadingTimeUtc = message.NewTurnaroundTime;
        await db.SaveChangesAsync();
        logger.LogInformation("TurnaroundRescheduled for {FlightId}: bag loading rescheduled to {LoadingTime}", message.FlightId, message.NewTurnaroundTime);
    }

    public async Task Handle(BoardingStarted message)
    {
        var bagLoad = await GetOrCreate(message.FlightId);
        if (bagLoad.Status != BagLoadStatus.Scheduled)
        {
            logger.LogInformation("BoardingStarted for {FlightId}: bag load is {Status}, nothing to do", message.FlightId, bagLoad.Status);
            return;
        }

        bagLoad.Status = BagLoadStatus.Verified;
        await db.SaveChangesAsync();
        logger.LogInformation("BoardingStarted for {FlightId}: all tagged bags verified as loaded", message.FlightId);
    }

    public async Task Handle(BoardingCompleted message)
    {
        var bagLoad = await GetOrCreate(message.FlightId);
        if (IsFinal(bagLoad))
        {
            logger.LogInformation("BoardingCompleted for {FlightId}: bag load is {Status}, nothing to do", message.FlightId, bagLoad.Status);
            return;
        }

        bagLoad.Status = BagLoadStatus.Finalized;
        await db.SaveChangesAsync();
        logger.LogInformation("BoardingCompleted for {FlightId}: baggage load confirmation finalized", message.FlightId);
    }

    public async Task Handle(FlightCancelled message)
    {
        var bagLoad = await GetOrCreate(message.FlightId);
        if (IsFinal(bagLoad))
        {
            logger.LogInformation("FlightCancelled for {FlightId}: bag load is {Status}, nothing to do", message.FlightId, bagLoad.Status);
            return;
        }

        bagLoad.Status = BagLoadStatus.Offloaded;
        await db.SaveChangesAsync();
        logger.LogInformation("FlightCancelled for {FlightId}: loaded bags offloaded", message.FlightId);
    }

    private static bool IsFinal(BagLoad bagLoad) =>
        bagLoad.Status is BagLoadStatus.Finalized or BagLoadStatus.Offloaded;

    private async Task<BagLoad> GetOrCreate(Guid flightId)
    {
        var bagLoad = await db.BagLoads.FindAsync(flightId);
        if (bagLoad is null)
        {
            bagLoad = new BagLoad { Id = flightId, Status = BagLoadStatus.Scheduled };
            db.BagLoads.Add(bagLoad);
        }
        return bagLoad;
    }
}
