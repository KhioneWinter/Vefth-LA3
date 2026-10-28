using JustGo.Flights.CheckInBoarding.Data;
using JustGo.Flights.CheckInBoarding.Models;
using JustGo.Flights.Contracts;
using Rebus.Handlers;

namespace JustGo.Flights.CheckInBoarding.Handlers;


public class FlightEventHandler(CheckInDbContext db, ILogger<FlightEventHandler> logger) : 
    IHandleMessages<GateAssigned>,
    IHandleMessages<FlightDelayed>,
    IHandleMessages<CheckInOpened>,
    IHandleMessages<BoardingStarted>,
    IHandleMessages<FlightDeparted>,
    IHandleMessages<FlightCancelled>
{
    public async Task Handle(GateAssigned message)
    {
        var flight = await GetOrCreate(message.FlightId);
        if (flight.Gate == message.Gate)
        {
            logger.LogInformation("GateAssigned for {FlightId}: gate already {Gate}, nothing to do", message.FlightId, message.Gate);
            return;
        }

        flight.Gate = message.Gate;
        await db.SaveChangesAsync();
        logger.LogInformation("GateAssigned for {FlightId}: gate set to {Gate}", message.FlightId, message.Gate);

    }

    public async Task Handle(FlightDelayed message)
    {
        var flight = await GetOrCreate(message.FlightId);
        if (flight.DepartureUtc >= message.NewDepartureTime)
        {
            logger.LogInformation("FlightDelayed for {FlightId}: departure already {Departure}, nothing to do", message.FlightId, flight.DepartureUtc);
            return;
        }
        flight.DepartureUtc = message.NewDepartureTime;
        await db.SaveChangesAsync();
        logger.LogInformation("FlightDelayed for {FlightId}: departure moved to {Departure}", message.FlightId, message.NewDepartureTime);
    }

    public async Task Handle(CheckInOpened message)
    {
        var flight = await GetOrCreate(message.FlightId);
        if (flight.Status != FlightStatus.Scheduled)
        {
            logger.LogInformation("CheckInOpened for {FlightId}: status is {Status}, nothing to do", message.FlightId, flight.Status);
            return;
        }

        flight.Status = FlightStatus.CheckInOpen;
        await db.SaveChangesAsync();
        logger.LogInformation("CheckInOpened for {FlightId}: status set to CheckInOpen", message.FlightId);
    }

    public async Task Handle(BoardingStarted message)
    {
        var flight = await GetOrCreate(message.FlightId);
        if (flight.Status is not (FlightStatus.Scheduled or FlightStatus.CheckInOpen))
        {
            logger.LogInformation("BoardingStarted for {FlightId}: status is {Status}, nothing to do", message.FlightId, flight.Status);
            return;
        }

        flight.Status = FlightStatus.Boarding;
        await db.SaveChangesAsync();
        logger.LogInformation("BoardingStarted for {FlightId}: status set to Boarding", message.FlightId);
    }

    public async Task Handle(FlightDeparted message)
    {
        var flight = await GetOrCreate(message.FlightId);
        if (flight.Status is FlightStatus.Closed or FlightStatus.Cancelled)
        {
            logger.LogInformation("FlightDeparted for {FlightId}: status is {Status}, nothing to do", message.FlightId, flight.Status);
            return;
        }

        flight.Status = FlightStatus.Closed;
        await db.SaveChangesAsync();
        logger.LogInformation("FlightDeparted for {FlightId}: status set to Closed", message.FlightId);
    }

    public async Task Handle(FlightCancelled message)
    {
        var flight = await GetOrCreate(message.FlightId);
        if (flight.Status is FlightStatus.Closed or FlightStatus.Cancelled)
        {
            logger.LogInformation("FlightCancelled for {FlightId}: status is {Status}, nothing to do", message.FlightId, flight.Status);
            return;
        }

        flight.Status = FlightStatus.Cancelled;
        await db.SaveChangesAsync();
        logger.LogInformation("FlightCancelled for {FlightId}: status set to Cancelled", message.FlightId);
    }

    private async Task<Flight> GetOrCreate(Guid flightId)
    {
        var flight = await db.Flights.FindAsync(flightId);
        if (flight is null)
        {
            flight = new Flight { Id = flightId, Status = FlightStatus.Scheduled };
            db.Flights.Add(flight);
        }
        return flight;
    }
}
