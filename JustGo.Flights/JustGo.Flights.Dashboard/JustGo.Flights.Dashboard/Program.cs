using JustGo.Flights.Contracts;
using JustGo.Flights.Dashboard.Components;
using JustGo.Flights.Dashboard.Handlers;
using JustGo.Flights.Dashboard.Hubs;
using Rebus.Config;
using Rebus.Retry.Simple;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddSignalR();

builder.Services.AddRebusHandler<DashboardEventHandler>();

builder.Services.AddRebus(
    configure =>
    {
        var connectionString = builder.Configuration.GetConnectionString("RabbitMq");

        var rabbitMqSection = builder.Configuration.GetSection("RabbitMq");
        var inputQueueName = rabbitMqSection["InputQueueName"];
        var errorQueueName = rabbitMqSection["ErrorQueueName"];

        configure.Transport(t => t.UseRabbitMq(connectionString, inputQueueName));
        configure.Options(t => t.RetryStrategy(errorQueueName: errorQueueName));

        return configure;
    },
    onCreated: async bus =>
    {
        // Bind our single input queue to each event's exchange (see README event tables)
        await bus.Subscribe<FlightScheduled>();
        await bus.Subscribe<GateAssigned>();
        await bus.Subscribe<FlightDelayed>();
        await bus.Subscribe<TurnaroundRescheduled>();
        await bus.Subscribe<CheckInOpened>();
        await bus.Subscribe<BoardingStarted>();
        await bus.Subscribe<BoardingCompleted>();
        await bus.Subscribe<FlightCancelled>();
        await bus.Subscribe<FlightDeparted>();
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapHub<DashboardHub>(DashboardHub.Path);
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(JustGo.Flights.Dashboard.Client._Imports).Assembly);

app.Run();