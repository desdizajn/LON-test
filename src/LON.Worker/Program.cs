using LON.Infrastructure;
using LON.Worker;
using LON.Worker.Jobs;

var builder = Host.CreateApplicationBuilder(args);

// Infrastructure registers IAlertRuleEvaluator + IAlertEvaluatorRunner so
// the hosted job below resolves them via DI without needing per-host wiring.
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHostedService<EventProcessorWorker>();
builder.Services.AddHostedService<AlertEvaluatorJob>();
builder.Services.AddHostedService<SoftDeleteRetentionJob>();

var host = builder.Build();
host.Run();
