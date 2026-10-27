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


builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


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
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapControllers();
app.Run();
