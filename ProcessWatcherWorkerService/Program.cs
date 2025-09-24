using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging;
using ProcessMonitorRepository;
using ProcessWatcherMonitor;
using ProcessWatcherWorkerService;
using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

const int Port = 5080; // set port in code as requested



if (Environment.UserInteractive &&
    Debugger.IsAttached &&
    !WindowsServiceHelpers.IsWindowsService())
{

    try
    {
        //Process.Start(new ProcessStartInfo($"http://localhost:{Port}/swagger") { UseShellExecute = true });
        //Process.Start(new ProcessStartInfo($"http://localhost:{Port}") { UseShellExecute = true });
    }
    catch { /* игнорируем ошибки открытия браузера */ }
}



// Ensure service uses the executable directory as content root
Directory.SetCurrentDirectory(AppContext.BaseDirectory);

var builder = WebApplication.CreateBuilder(args);

// Run as Windows Service when started by SCM
builder.Host.UseWindowsService(options => options.ServiceName = "ProcessWatcher");
builder.Logging.AddEventLog(settings => settings.SourceName = "ProcessWatcher");

// Kestrel + explicit URL (no appsettings.json for port)
builder.WebHost.UseKestrel()
               .UseUrls($"http://0.0.0.0:{Port}");

// Register your background worker(s)
builder.Services.AddHostedService<Worker>();
builder.Services.AddSingleton<Repository>();
builder.Services.AddSingleton<ProcessMonitor>();


// Add controllers from the API assembly.
// We avoid any "Marker" classes by referencing an existing controller type.
// If you rename/move controllers' namespace, update the typeof(...) below accordingly.
builder.Services.AddControllers()
    .AddApplicationPart(typeof(ProcessWatcherWebAPI.Controllers.ProcessWatcherController).Assembly);

// Optional: Swagger for development
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opt =>
{
    // Use XML comments if present (from API assembly)
    var apiAsm = typeof(ProcessWatcherWebAPI.Controllers.ProcessWatcherController).Assembly;
    var xmlName = apiAsm.GetName().Name + ".xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlName);
    if (File.Exists(xmlPath))
        opt.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);

    // (Optional) Title/Version
    opt.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "ProcessWatcher API",
        Version = "v1"
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "ProcessWatcher API v1");
    c.RoutePrefix = "swagger"; // UI по адресу /swagger
});

// Static files + Blazor WASM from the referenced client project
app.UseBlazorFrameworkFiles(); // requires Microsoft.AspNetCore.Components.WebAssembly.Server
app.UseStaticFiles();

app.MapControllers();                 // your API routes
app.MapFallbackToFile("index.html");  // Blazor client

app.Run();



