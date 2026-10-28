using JustGo.Flights.Contracts;
using JustGo.Flights.Notification.Data;
using Microsoft.EntityFrameworkCore;
using Rebus.Config;
using Rebus.Retry.Simple;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<NotificationDbContext>(options => options.UseNpgsql(
    builder.Configuration.GetConnectionString("FlightsDb"),
    npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", NotificationDbContext.Schema)
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
        await bus.Subscribe<FlightDelayed>();
        await bus.Subscribe<FlightCancelled>();
    });

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
    db.Database.Migrate();
}

host.Run();
