using JustGo.Flights.Contracts;
using JustGo.Flights.OperationsConsole.Data;
using JustGo.Flights.OperationsConsole.Models;
using Microsoft.AspNetCore.Mvc;
using Rebus.Bus;

namespace JustGo.Flights.OperationsConsole.Controllers;

[ApiController]
[Route("[controller]")]
public class FlightSchedulingController(FlightsDbContext db, IBus bus) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateFlightRequest req)
    {
        var flight = new Flight
        {
            Id = Guid.NewGuid(),
            FlightNumber = req.FlightNumber,
            Origin = req.Origin,
            Destination = req.Destination,
            DepartureUtc = req.DepartureUtc,
            Status = FlightStatus.Scheduled
        };

        db.Flights.Add(flight);
        await db.SaveChangesAsync();
        await bus.Publish(new FlightScheduled(flight.Id, flight.FlightNumber, flight.Origin, flight.Destination, flight.DepartureUtc));

        return Accepted(new {flightId = flight.Id});
    }

    [HttpPost("{flightId:guid}/gate")]
    public async Task<IActionResult> AssignGate(Guid flightId, AssignGateRequest req)
    {
        var flight = await db.Flights.FindAsync(flightId);
        // returns code 404
        if (flight is null) return NotFound();
        // returns code 409
        if (flight.Status is FlightStatus.Cancelled or FlightStatus.Departed)
            return Conflict();
        // returns code 200
        if (flight.Gate == req.Gate) return Ok();

        flight.Gate = req.Gate;
        await db.SaveChangesAsync();
        await bus.Publish(new GateAssigned(flight.Id, flight.Gate));
        // returns code 202
        return Accepted();
    }

    [HttpPost("{flightId:guid}/delay")]
    public async Task<IActionResult> Delay(Guid flightId, DelayFlightRequest req)
    {
        var flight = await db.Flights.FindAsync(flightId);
        // returns code 404
        if (flight is null) return NotFound();
        // returns code 409
        if (flight.Status is FlightStatus.Cancelled or FlightStatus.Departed)
            return Conflict();
        // returns code 409
        if (req.NewDepartureUtc < flight.DepartureUtc) 
            return Conflict();
        // returns code 200
        if (req.NewDepartureUtc == flight.DepartureUtc) return Ok();

        flight.DepartureUtc = req.NewDepartureUtc;
        await db.SaveChangesAsync();
        await bus.Publish(new FlightDelayed(flight.Id, flight.DepartureUtc, req.Reason));
        // returns code 202
        return Accepted();
    }

    [HttpPost("{flightId:guid}/checkin/open")]
    public async Task<IActionResult> CheckIn(Guid flightId)
    {
        var flight = await db.Flights.FindAsync(flightId);
        // returns code 404
        if (flight is null) return NotFound();
        // returns code 409
        if (flight.Status is FlightStatus.Cancelled) return Conflict();
        // returns code 200
        if (flight.Status is not FlightStatus.Scheduled) return Ok();

        flight.Status = FlightStatus.CheckInOpen;
        await db.SaveChangesAsync();
        await bus.Publish(new CheckInOpened(flight.Id, DateTimeOffset.UtcNow));
        // returns code 202
        return Accepted();
    }

    [HttpPost("{flightId:guid}/boarding/start")]
    public async Task<IActionResult> BoardingStart(Guid flightId)
    {
        var flight = await db.Flights.FindAsync(flightId);
        // returns code 404
        if (flight is null) return NotFound();
        // returns code 200
        if (flight.Status is FlightStatus.Boarding) return Ok();
        // returns code 409
        if (flight.Status is not FlightStatus.CheckInOpen) return Conflict();


        flight.Status = FlightStatus.Boarding;
        await db.SaveChangesAsync();
        await bus.Publish(new BoardingStarted(flight.Id, DateTimeOffset.UtcNow));
        // returns code 202
        return Accepted();
    }

    [HttpPost("{flightId:guid}/boarding/complete")]
    public async Task<IActionResult> BoardingComplete(Guid flightId)
    {
        var flight = await db.Flights.FindAsync(flightId);
        // returns code 404
        if (flight is null) return NotFound();
        // returns code 200
        if (flight.Status is FlightStatus.BoardingCompleted or FlightStatus.Departed) 
            return Ok();
        // returns code 409
        if (flight.Status is not FlightStatus.Boarding) return Conflict();


        flight.Status = FlightStatus.BoardingCompleted;
        await db.SaveChangesAsync();
        await bus.Publish(new BoardingCompleted(flight.Id, DateTimeOffset.UtcNow));
        // returns code 202
        return Accepted();
    }

    [HttpPost("{flightId:guid}/depart")]
    public async Task<IActionResult> Departed(Guid flightId)
    {
        var flight = await db.Flights.FindAsync(flightId);
        // returns code 404
        if (flight is null) return NotFound();
        // returns code 200
        if (flight.Status is FlightStatus.Departed) return Ok();
        // returns code 409
        if (flight.Status is not FlightStatus.BoardingCompleted) return Conflict();


        flight.Status = FlightStatus.Departed;
        await db.SaveChangesAsync();
        await bus.Publish(new FlightDeparted(flight.Id, DateTimeOffset.UtcNow));
        // returns code 202
        return Accepted();
    }

    [HttpPost("{flightId:guid}/cancel")]
    public async Task<IActionResult> CancelFlight(Guid flightId, CancelFlightRequest req)
    {
        var flight = await db.Flights.FindAsync(flightId);
        // returns code 404
        if (flight is null) return NotFound();
        // returns code 200
        if (flight.Status is FlightStatus.Cancelled) return Ok();
        // returns code 409
        if (flight.Status is FlightStatus.BoardingCompleted or FlightStatus.Departed) 
            return Conflict();


        flight.Status = FlightStatus.Cancelled;
        await db.SaveChangesAsync();
        await bus.Publish(new FlightCancelled(flight.Id, req.Reason));
        // returns code 202
        return Accepted();

    }
}