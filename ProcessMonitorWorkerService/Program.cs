using ProcessMonitorWorkerService;
using ProcessMonitorRepository;



var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton<Repository>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();

