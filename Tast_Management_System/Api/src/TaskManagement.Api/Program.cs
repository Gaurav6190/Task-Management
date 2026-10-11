using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Api.Infrastructure;
using TaskManagement.Application;
using TaskManagement.Infrastructure;
using TaskManagement.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

builder.Services
    .AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        // Never echo System.Text.Json exception text (type names, byte positions) back to clients.
        o.AllowInputFormatterExceptionMessages = false;
    })
    .ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = ModelStateProblems.Create);

// Every error response (including empty 404/405/415 from routing) gets the standard shape.
builder.Services.AddProblemDetails(o =>
    o.CustomizeProblemDetails = ctx => ApiProblems.Enrich(ctx.ProblemDetails, ctx.HttpContext));
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// JWT bearer auth: every endpoint needs a token except POST /api/auth/login. CORS for the React UI.
builder.Services.AddApiAuth(builder.Configuration);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o => o.AddBearerSecurity());

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Dev convenience only; production uses migration scripts/bundles.
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

app.UseHttpsRedirection();
app.UseCors(AuthSetup.UiCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program { } // enables WebApplicationFactory<Program> in integration tests
