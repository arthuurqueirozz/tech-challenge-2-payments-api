using FCG.Payments.Api;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) =>
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

builder.Services.AddPaymentsApi(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/", () => TypedResults.Ok(new
{
    service = "FCG Payments API",
    status = "running"
}))
.WithName("GetServiceStatus")
.WithOpenApi();

app.MapHealthChecks("/health");

await app.RunAsync();

public partial class Program;
