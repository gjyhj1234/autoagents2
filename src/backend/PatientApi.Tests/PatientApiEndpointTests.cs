using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace PatientApi.Tests;

/// <summary>
/// Custom factory that provides a dummy connection string so the application
/// starts without throwing, without modifying the process-wide environment.
/// </summary>
public sealed class PatientApiFactory : WebApplicationFactory<Program>
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration(config =>
        {
            // Supply a dummy value so the app does not throw on startup.
            // Tests that actually reach the database will receive a 500.
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CONNECTION_STRING"] = "Host=localhost;Database=test;Username=test;Password=test"
            });
        });
        return base.CreateHost(builder);
    }
}

public class PatientApiEndpointTests : IClassFixture<PatientApiFactory>
{
    private readonly HttpClient _client;

    public PatientApiEndpointTests(PatientApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthCheck_ReturnsOk()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreatePatient_MissingName_ReturnsBadRequest()
    {
        var payload = new
        {
            name = "",
            gender = "Male",
            dateOfBirth = "1990-01-01",
            phone = "13800138000"
        };

        var response = await _client.PostAsJsonAsync("/api/patients", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreatePatient_MissingGender_ReturnsBadRequest()
    {
        var payload = new
        {
            name = "ZhangSan",
            gender = "",
            dateOfBirth = "1990-01-01",
            phone = "13800138000"
        };

        var response = await _client.PostAsJsonAsync("/api/patients", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreatePatient_MissingPhone_ReturnsBadRequest()
    {
        var payload = new
        {
            name = "ZhangSan",
            gender = "Male",
            dateOfBirth = "1990-01-01",
            phone = ""
        };

        var response = await _client.PostAsJsonAsync("/api/patients", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdatePatient_MissingRequiredFields_ReturnsBadRequest()
    {
        var id = Guid.NewGuid();
        var payload = new
        {
            name = "",
            gender = "",
            dateOfBirth = "1990-01-01",
            phone = ""
        };

        var response = await _client.PutAsJsonAsync($"/api/patients/{id}", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetPatientById_WithInvalidGuidFormat_ReturnsNotFound()
    {
        // The {id:guid} route constraint means the route won't match for non-GUID values,
        // so ASP.NET Core returns 404 without ever touching the database.
        var response = await _client.GetAsync("/api/patients/not-a-valid-guid");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
