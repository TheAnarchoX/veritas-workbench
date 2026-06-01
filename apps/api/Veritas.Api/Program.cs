using Microsoft.EntityFrameworkCore;
using Veritas.Api.Endpoints;
using Veritas.Infrastructure;
using Veritas.Infrastructure.Persistence;
using Veritas.Infrastructure.Seed;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddPolicy("web", policy =>
    {
        policy.AllowAnyHeader()
            .AllowAnyMethod()
            .WithOrigins(
                "http://localhost:5173",
                "http://127.0.0.1:5173",
                "http://localhost:4173",
                "http://127.0.0.1:4173");
    });
});
builder.Services.AddVeritasInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("web");
app.MapVeritasEndpoints();

if (app.Configuration.GetValue("Database:ApplyMigrations", false))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<VeritasDbContext>();
    await db.Database.MigrateAsync();
}

if (app.Configuration.GetValue("Database:EnsureCreated", false))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<VeritasDbContext>();
    await db.Database.EnsureCreatedAsync();
}

if (app.Configuration.GetValue("Demo:SeedData", false))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<VeritasDbContext>();
    await DemoSeeder.SeedAsync(db, CancellationToken.None);
}

app.Run();

public partial class Program;
