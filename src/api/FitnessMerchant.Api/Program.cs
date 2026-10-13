using FitnessMerchant.Api.Common;
using FitnessMerchant.Api.Features.Health;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
if (builder.Environment.IsDevelopment())
{
    builder.Logging.AddSimpleConsole(options => options.SingleLine = true);
}
else
{
    builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);
}

builder.Services.AddApiProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseMiddleware<SecurityHeadersMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapHealthEndpoints();

app.Run();
