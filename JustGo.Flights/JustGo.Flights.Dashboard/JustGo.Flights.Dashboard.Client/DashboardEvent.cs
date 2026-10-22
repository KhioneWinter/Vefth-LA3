namespace JustGo.Flights.Dashboard.Client;

// What the server pushes to the browser over SignalR for every event it receives from RabbitMQ
public record DashboardEvent(string EventType, Guid FlightId, string Summary, DateTimeOffset ReceivedAt);
