using JustGo.Flights.OperationsConsole.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Rebus.Config;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<FlightsDbContext>(options => options.UseNpgsql(
    builder.Configuration.GetConnectionString("FlightsDb"),
    npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", FlightsDbContext.Schema)
));

builder.Services.AddRebus(configure => configure.Transport(t => t.UseRabbitMqAsOneWayClient(
    builder.Configuration.GetConnectionString("RabbitMq")
)));

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FlightsDbContext>();
    db.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
