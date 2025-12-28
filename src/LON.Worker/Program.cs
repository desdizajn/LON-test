using LON.Infrastructure;
using LON.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<EventProcessorWorker>();

var host = builder.Build();
host.Run();
