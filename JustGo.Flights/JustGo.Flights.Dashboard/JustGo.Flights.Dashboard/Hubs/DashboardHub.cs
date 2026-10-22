using Microsoft.AspNetCore.SignalR;

namespace JustGo.Flights.Dashboard.Hubs;

// Push-only hub: the server sends "EventReceived" to all connected browsers (see DashboardEventHandler)
public class DashboardHub : Hub
{
    public const string Path = "/dashboardhub";
    public const string EventReceived = "EventReceived";
}
