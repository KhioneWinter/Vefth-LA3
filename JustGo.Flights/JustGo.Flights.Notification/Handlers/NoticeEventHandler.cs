using JustGo.Flights.Contracts;
using JustGo.Flights.Notification.Data;
using JustGo.Flights.Notification.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Rebus.Handlers;

namespace JustGo.Flights.Notification.Handlers;

public class NoticeEventHandler(NotificationDbContext db, ILogger<NoticeEventHandler> logger) :
    IHandleMessages<FlightDelayed>,
    IHandleMessages<FlightCancelled>
{
    public async Task Handle(FlightDelayed message)
    {
        var alreadySent = await db.Notices.AnyAsync(n =>
            n.FlightId == message.FlightId &&
            n.Type == NoticeType.Delay &&
            n.NewDepartureUtc == message.NewDepartureTime);
        if (alreadySent)
        {
            logger.LogInformation("FlightDelayed for {FlightId}: delay notice for {Departure} already sent, nothing to do", message.FlightId, message.NewDepartureTime);
            return;
        }

        var text = $"Flight {message.FlightId} is delayed. New departure {message.NewDepartureTime:u}. Reason: {message.Reason}";
        await Send(new Notice
        {
            Id = Guid.NewGuid(),
            FlightId = message.FlightId,
            Type = NoticeType.Delay,
            NewDepartureUtc = message.NewDepartureTime,
            Reason = message.Reason,
            Message = text,
            SentUtc = DateTimeOffset.UtcNow
        }, nameof(FlightDelayed));
    }

    public async Task Handle(FlightCancelled message)
    {
        var alreadySent = await db.Notices.AnyAsync(n =>
            n.FlightId == message.FlightId &&
            n.Type == NoticeType.Cancellation);
        if (alreadySent)
        {
            logger.LogInformation("FlightCancelled for {FlightId}: cancellation notice already sent, nothing to do", message.FlightId);
            return;
        }

        var text = $"Flight {message.FlightId} is cancelled. Reason: {message.Reason}";
        await Send(new Notice
        {
            Id = Guid.NewGuid(),
            FlightId = message.FlightId,
            Type = NoticeType.Cancellation,
            Reason = message.Reason,
            Message = text,
            SentUtc = DateTimeOffset.UtcNow
        }, nameof(FlightCancelled));
    }

    // Stores the notice first, the unique index makes sure the same notice is never stored (and sent) twice
    private async Task Send(Notice notice, string eventName)
    {
        db.Notices.Add(notice);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            logger.LogInformation("{Event} for {FlightId}: notice already sent, nothing to do", eventName, notice.FlightId);
            return;
        }

        logger.LogWarning("NOTICE TO STAFF: {Message}", notice.Message);
        logger.LogInformation("{Event} for {FlightId}: {Type} notice sent", eventName, notice.FlightId, notice.Type);
    }
}
