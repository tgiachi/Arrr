using Arrr.Service;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders().AddSerilog();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
