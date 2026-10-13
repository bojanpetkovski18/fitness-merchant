using FitnessMerchant.Api.Common;
using FitnessMerchant.Api.Features.Health;
using FitnessMerchant.Api.Infrastructure.Data;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.AddDevelopmentDotEnv();

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
builder.AddAppDatabase();

var app = builder.Build();

await app.InitializeDatabaseAsync();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseMiddleware<SecurityHeadersMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapHealthEndpoints();

await app.RunAsync();
