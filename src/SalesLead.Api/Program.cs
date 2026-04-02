using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SalesLead.Api.Hosting;
using SalesLead.Api.Middleware;
using SalesLead.Api.Services;
using SalesLead.Api.Swagger;
using SalesLead.Infrastructure.Data;
using SalesLead.Infrastructure.Seed;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) =>
    lc.ReadFrom.Configuration(ctx.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console());

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new() { Title = "Sales Lead Management API", Version = "v1" });
    o.OperationFilter<SwaggerHeaderOperationFilter>();

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        o.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
});

string conn;
if (builder.Environment.IsEnvironment("IntegrationTest"))
{
    conn = $"Data Source={Path.GetTempFileName()}";
}
else
{
    var dbPath = Path.Combine(builder.Environment.ContentRootPath, "Data", "saleslead.db");
    Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
    // Cache=Shared + busy timeout: avoids silent stalls when another process holds the DB (e.g. second dotnet run).
    conn = $"Data Source={dbPath};Cache=Shared;Default Timeout=30";
}

builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite(conn));

builder.Services.AddSingleton<IIngestRateLimiter, IngestRateLimiter>();
builder.Services.AddScoped<LeadIngestionService>();
builder.Services.AddScoped<LeadSalesService>();
builder.Services.AddHostedService<OutboxDispatcherHostedService>();

builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>();

var app = builder.Build();

Log.Information("Applying database migrations…");
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await DatabaseSeeder.SeedAsync(db);
}
Log.Information("Database migrations and seed completed.");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<IngestAuthMiddleware>();
app.UseMiddleware<SalesAuthMiddleware>();

app.MapControllers();
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

app.Run();

public partial class Program;
