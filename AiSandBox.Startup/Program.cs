using AiSandBox.ApplicationServices.Configuration;
using AiSandBox.ConsolePresentation.Configuration;
using AiSandBox.Domain.Configuration;
using AiSandBox.Infrastructure.Configuration;
using AiSandBox.WebApi.Configuration;
using ConsolePresentation;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using AiSandBox.Ai.Configuration;

var builder = WebApplication.CreateBuilder(args);

bool isConsoleRunnerEnabled = builder.Configuration.GetValue<bool>("IsConsoleRunnerEnabled");
bool isWebApiEnabled = builder.Configuration.GetValue<bool>("IsWebApiEnabled");


builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddDomainServices();
builder.Services.AddApplicationServices();
builder.Services.AddControllers();
builder.Services.AddAiSandBoxServices();
if (isConsoleRunnerEnabled)
{
    builder.Services.AddConsolePresentationServices(builder.Configuration, builder.Configuration);
    builder.Configuration.AddJsonFile("ConsoleSettings.json", optional: false, reloadOnChange: true);
}


if (isWebApiEnabled)
    builder.Services.AddWebApiPresentationServices();

var app = builder.Build();

if (isConsoleRunnerEnabled)
    app.Services.GetRequiredService<IConsoleRunner>().Run();

if (isWebApiEnabled)
{
    app.MapControllers();
    app.Run();
}





