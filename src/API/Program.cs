using API.Middleware;
using Application;
using Infrastructure;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using System.Threading.RateLimiting;

// Bootstrap logger for startup errors
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting application");

    var builder = WebApplication.CreateBuilder(args);

    // Configuration validation (fail fast on missing required settings)
    builder.Services.AddOptions<Application.Settings.DatabaseSettings>()
        .BindConfiguration(Application.Settings.DatabaseSettings.SectionName)
        .ValidateDataAnnotations()
        .ValidateOnStart();

    builder.Services.AddOptions<Application.Settings.JwtSettings>()
        .BindConfiguration(Application.Settings.JwtSettings.SectionName)
        .ValidateDataAnnotations()
        .ValidateOnStart();

    // Configure Serilog
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithEnvironmentName()
        .Enrich.WithProperty("Application", "CleanKissApi")
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
        .WriteTo.File(
            path: "logs/log-.txt",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 7,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}"));

    // Register layers
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddApplication();

    builder.Services.AddControllers();
    builder.Services.AddOpenApi();

    // API Versioning
    builder.Services.AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new Asp.Versioning.ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
        options.ApiVersionReader = Asp.Versioning.ApiVersionReader.Combine(
            new Asp.Versioning.UrlSegmentApiVersionReader(),
            new Asp.Versioning.HeaderApiVersionReader("X-Api-Version"));
    }).AddMvc();

    // Rate limiting
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        // Global fixed window limiter (100 requests per minute per IP)
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return RateLimitPartition.GetFixedWindowLimiter(clientIp, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
        });

        // Named policy for stricter endpoints (e.g., auth, registration)
        options.AddPolicy("strict", context =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                }));

        options.OnRejected = async (context, cancellationToken) =>
        {
            context.HttpContext.Response.ContentType = "application/json";
            await context.HttpContext.Response.WriteAsJsonAsync(
                new { error = "Too many requests. Please try again later." },
                cancellationToken);
        };
    });

    // OpenTelemetry distributed tracing
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource
            .AddService(
                serviceName: "CleanKissApi",
                serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0"))
        .WithTracing(tracing => tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSqlClientInstrumentation()
            .AddConsoleExporter());

    // Health checks (non-dev environments only)
    var isProduction = !builder.Environment.IsDevelopment();

    if (isProduction)
    {
        var healthChecks = builder.Services.AddHealthChecks();

        // SQL Server health check (if configured)
        var sqlConnection = builder.Configuration.GetConnectionString("Default");
        if (!string.IsNullOrEmpty(sqlConnection))
        {
            healthChecks.AddSqlServer(sqlConnection, name: "sqlserver", tags: ["db", "ready"]);
        }

        // Redis health check (if configured)
        var redisConnection = builder.Configuration.GetConnectionString("Redis");
        if (!string.IsNullOrEmpty(redisConnection))
        {
            healthChecks.AddRedis(redisConnection, name: "redis", tags: ["cache", "ready"]);
        }
    }

    var app = builder.Build();

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    // Correlation ID (must be first to enrich all logs)
    app.UseMiddleware<CorrelationIdMiddleware>();

    // Request logging
    app.UseSerilogRequestLogging(options =>
    {
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
            diagnosticContext.Set("ClientIp", httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");

            if (httpContext.Items.TryGetValue("CorrelationId", out var correlationId))
                diagnosticContext.Set("CorrelationId", correlationId);
        };
    });

    // Global exception handling
    app.UseMiddleware<ExceptionMiddleware>();

    // Rate limiting
    app.UseRateLimiter();

    // Idempotency for POST/PUT/PATCH/DELETE
    app.UseMiddleware<IdempotencyMiddleware>();

    app.UseHttpsRedirection();

    app.UseAuthorization();

    app.MapControllers();

    // Health check endpoints (non-dev only)
    if (isProduction)
    {
        app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = _ => false // No checks, just confirms app is running
        });

        app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = "application/json";
                var result = new
                {
                    status = report.Status.ToString(),
                    checks = report.Entries.Select(e => new
                    {
                        name = e.Key,
                        status = e.Value.Status.ToString(),
                        duration = e.Value.Duration.TotalMilliseconds,
                        exception = e.Value.Exception?.Message
                    }),
                    totalDuration = report.TotalDuration.TotalMilliseconds
                };
                await context.Response.WriteAsJsonAsync(result);
            }
        });
    }

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
