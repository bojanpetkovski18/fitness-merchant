using FitnessMerchant.Api.Common;
using FitnessMerchant.Api.Features.Auth;
using FitnessMerchant.Api.Features.Health;
using FitnessMerchant.Api.Features.Images;
using FitnessMerchant.Api.Infrastructure.Data;
using FitnessMerchant.Api.Infrastructure.Identity;
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
builder.Services.AddValidation();
builder.Services.AddOpenApi();
builder.Services.AddAppRateLimiting(builder.Configuration);
builder.AddAppDatabase();
builder.AddAppAuth();
builder.Services.AddAuthFeature();
builder.Services.AddScoped<GetImageQuery>();

var app = builder.Build();

await app.InitializeDatabaseAsync();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseRateLimiter();
app.UseAppAuth();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
}

app.MapHealthEndpoints();
app.MapImageEndpoints();
app.MapAuthEndpoints();

// Test seam: integration tests register extra endpoints (e.g. an admin-only probe). Production registers none.
foreach (var mapper in app.Services.GetServices<IEndpointMapper>())
{
    mapper.Map(app);
}

await app.RunAsync();
