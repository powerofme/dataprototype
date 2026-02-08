using Microsoft.AspNetCore.SignalR;
using Orleans;
using Orleans.Hosting;
using RiskDataPlatform.Api.Endpoints;
using RiskDataPlatform.Api.Hubs;
using RiskDataPlatform.Api.Middleware;
using RiskDataPlatform.Core.Diagnostics;
using RiskDataPlatform.Core.Interfaces;
using RiskDataPlatform.Infrastructure.Storage;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults
builder.AddServiceDefaults();

// Add Orleans co-hosting
builder.UseOrleans(siloBuilder =>
{
    siloBuilder.UseLocalhostClustering();
    
    if (builder.Environment.IsDevelopment())
    {
        siloBuilder.UseDevelopmentClustering(options =>
        {
            options.PrimarySiloEndpoint = new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 11111);
        });
    }
    
    siloBuilder.AddMemoryGrainStorage("Default");
    siloBuilder.AddMemoryGrainStorage("PubSubStore");
});

// Add services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();

// Register infrastructure services
builder.Services.AddSingleton<IS3Store, InMemoryS3Store>();
builder.Services.AddSingleton<TestDataSeeder>();

// Add OpenTelemetry sources
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddSource(Telemetry.Api.Name);
        tracing.AddSource(Telemetry.Grains.Name);
        tracing.AddSource(Telemetry.Query.Name);
        tracing.AddSource(Telemetry.Arrow.Name);
        tracing.AddSource(Telemetry.Storage.Name);
    });

var app = builder.Build();

// Seed test data in development
if (app.Environment.IsDevelopment())
{
    var seeder = app.Services.GetRequiredService<TestDataSeeder>();
    await seeder.SeedAsync("test-bucket", "default");
    
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Configure middleware pipeline (order matters!)
app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseMiddleware<TenantMiddleware>();
app.UseMiddleware<AuthPlaceholderMiddleware>();
app.UseMiddleware<RequestSanitizerMiddleware>();
app.UseMiddleware<EventJournalMiddleware>();

app.UseHttpsRedirection();

// Map SignalR hub
app.MapHub<RiskHub>("/hubs/risk");

// Map API endpoints
var apiV1 = app.MapGroup("/api/v1");

// Execution endpoints
var executions = apiV1.MapGroup("/executions")
    .WithTags("Executions");
executions.MapExecutionEndpoints();
executions.MapDataQueryEndpoints();
executions.MapDebugInfoEndpoints();
executions.MapValuationErrorEndpoints();

// Desk endpoints
apiV1.MapDeskExecutionEndpoints()
    .WithTags("Desks");

apiV1.MapIncrementalEndpoints()
    .WithTags("Incremental");

// Comparison endpoints
var comparison = apiV1.MapGroup("/compare")
    .WithTags("Comparison");
comparison.MapComparisonEndpoints();

// External endpoints
var external = apiV1.MapGroup("/external")
    .WithTags("External");
external.MapExternalEndpoints();

// Config endpoints
apiV1.MapConfigEndpoints()
    .WithTags("Config");

// Replay endpoints
var replay = apiV1.MapGroup("/replay")
    .WithTags("Replay");
replay.MapReplayEndpoints();

// Admin endpoints
var admin = apiV1.MapGroup("/admin")
    .WithTags("Admin");
admin.MapAdminEndpoints();

// Health endpoints
var health = app.MapGroup("/health")
    .WithTags("Health");
health.MapHealthEndpoints();

// Map default Aspire endpoints
app.MapDefaultEndpoints();

app.Run();

