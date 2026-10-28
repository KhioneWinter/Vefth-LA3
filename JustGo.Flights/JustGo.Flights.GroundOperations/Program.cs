using JustGo.Flights.Contracts;
using JustGo.Flights.GroundOperations.Data;
using Microsoft.EntityFrameworkCore;
using Rebus.Config;
using Rebus.Retry.Simple;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<GroundOpsDbContext>(options => options.UseNpgsql(
    builder.Configuration.GetConnectionString("FlightsDb"),
    npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", GroundOpsDbContext.Schema)
));

builder.Services.AutoRegisterHandlersFromAssemblyOf<Program>();

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
        await bus.Subscribe<FlightScheduled>();
        await bus.Subscribe<GateAssigned>();
        await bus.Subscribe<FlightDelayed>();
        await bus.Subscribe<CheckInOpened>();
        await bus.Subscribe<BoardingStarted>();
        await bus.Subscribe<BoardingCompleted>();
        await bus.Subscribe<FlightDeparted>();
        await bus.Subscribe<FlightCancelled>();
    });

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GroundOpsDbContext>();
    db.Database.Migrate();
}

host.Run();
