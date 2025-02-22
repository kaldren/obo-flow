using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using dfunc1;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Functions.Worker;

var builder = Host.CreateDefaultBuilder(args)
    .ConfigureFunctionsWebApplication(app =>
    {
        // No need to call UseAuthentication or UseAuthorization here
        //app.UseAuthentication();
        //app.UseAuthorization();
    })
    .ConfigureAppConfiguration((context, config) =>
    {
        config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
        config.AddEnvironmentVariables();
    })
    .ConfigureServices(services =>
    {
        var configuration = services.BuildServiceProvider().GetService<IConfiguration>();
        var logger = services.BuildServiceProvider().GetService<ILoggerFactory>();

        services.AddScoped<IEntraIDJwtBearerValidation>(provider =>
            new EntraIDJwtBearerValidation(
                configuration,
                logger
            ));

        // Configure Azure AD authentication
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddMicrosoftIdentityWebApi(configuration.GetSection("AzureAd"));

        // Configure authorization with scopes
        services.AddAuthorization(options =>
        {
            options.AddPolicy("DFunc1Scope", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireScope("dfunc1.readwrite");
            });
        });

        services
            .AddApplicationInsightsTelemetryWorkerService()
            .ConfigureFunctionsApplicationInsights();
    });

builder.Build().Run();