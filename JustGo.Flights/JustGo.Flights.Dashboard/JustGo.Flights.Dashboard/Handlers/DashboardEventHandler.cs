using JustGo.Flights.Contracts;
using JustGo.Flights.Dashboard.Client;
using JustGo.Flights.Dashboard.Hubs;
using Microsoft.AspNetCore.SignalR;
using Rebus.Handlers;

namespace JustGo.Flights.Dashboard.Handlers;

// The Dashboard reacts to every event by forwarding it to the browsers. It stores nothing,
// so a redelivered message is simply shown again.
public class DashboardEventHandler(IHubContext<DashboardHub> hub) :
    IHandleMessages<FlightScheduled>,
    IHandleMessages<GateAssigned>,
    IHandleMessages<FlightDelayed>,
    IHandleMessages<TurnaroundRescheduled>,
    IHandleMessages<CheckInOpened>,
    IHandleMessages<BoardingStarted>,
    IHandleMessages<BoardingCompleted>,
    IHandleMessages<FlightCancelled>,
    IHandleMessages<FlightDeparted>
{
    public Task Handle(FlightScheduled message) => Push(nameof(FlightScheduled), message.FlightId,
        $"{message.FlightNumber} {message.Origin} → {message.Destination}, departs {message.DepartureTime:u}");

    public Task Handle(GateAssigned message) => Push(nameof(GateAssigned), message.FlightId,
        $"Gate {message.Gate}");

    public Task Handle(FlightDelayed message) => Push(nameof(FlightDelayed), message.FlightId,
        $"New departure {message.NewDepartureTime:u} ({message.Reason})");

    public Task Handle(TurnaroundRescheduled message) => Push(nameof(TurnaroundRescheduled), message.FlightId,
        $"New turnaround time {message.NewTurnaroundTime:u}");

    public Task Handle(CheckInOpened message) => Push(nameof(CheckInOpened), message.FlightId,
        $"Check-in opened at {message.OpenedAt:u}");

    public Task Handle(BoardingStarted message) => Push(nameof(BoardingStarted), message.FlightId,
        $"Boarding started at {message.StartedAt:u}");

    public Task Handle(BoardingCompleted message) => Push(nameof(BoardingCompleted), message.FlightId,
        $"Boarding completed at {message.CompletedAt:u}");

    public Task Handle(FlightCancelled message) => Push(nameof(FlightCancelled), message.FlightId,
        $"Cancelled ({message.Reason})");

    public Task Handle(FlightDeparted message) => Push(nameof(FlightDeparted), message.FlightId,
        $"Departed at {message.DepartedAt:u}");

    private Task Push(string eventType, Guid flightId, string summary) =>
        hub.Clients.All.SendAsync(DashboardHub.EventReceived,
            new DashboardEvent(eventType, flightId, summary, DateTimeOffset.UtcNow));
}
