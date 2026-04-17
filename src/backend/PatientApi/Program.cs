using PatientApi;
using PatientApi.Data;
using PatientApi.Models;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

// CORS - allow all origins for demo purposes
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors();

var connectionString = app.Configuration["CONNECTION_STRING"]
    ?? throw new InvalidOperationException("CONNECTION_STRING is not set.");

var repo = new PatientRepository(connectionString);

// Health check
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

// GET /api/patients
app.MapGet("/api/patients", async () =>
{
    var patients = await repo.GetAllAsync();
    return Results.Ok(patients);
});

// GET /api/patients/{id}
app.MapGet("/api/patients/{id:guid}", async (Guid id) =>
{
    var patient = await repo.GetByIdAsync(id);
    return patient is not null ? Results.Ok(patient) : Results.NotFound();
});

// POST /api/patients
app.MapPost("/api/patients", async (CreatePatientRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Name) ||
        string.IsNullOrWhiteSpace(request.Gender) ||
        string.IsNullOrWhiteSpace(request.Phone))
    {
        return Results.BadRequest("Name, Gender, and Phone are required.");
    }

    var patient = await repo.CreateAsync(request);
    return Results.Created($"/api/patients/{patient.Id}", patient);
});

// PUT /api/patients/{id}
app.MapPut("/api/patients/{id:guid}", async (Guid id, UpdatePatientRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Name) ||
        string.IsNullOrWhiteSpace(request.Gender) ||
        string.IsNullOrWhiteSpace(request.Phone))
    {
        return Results.BadRequest("Name, Gender, and Phone are required.");
    }

    var patient = await repo.UpdateAsync(id, request);
    return patient is not null ? Results.Ok(patient) : Results.NotFound();
});

// DELETE /api/patients/{id}
app.MapDelete("/api/patients/{id:guid}", async (Guid id) =>
{
    var deleted = await repo.DeleteAsync(id);
    return deleted ? Results.NoContent() : Results.NotFound();
});

app.Run();

// Expose Program class for WebApplicationFactory in test projects
public partial class Program { }

