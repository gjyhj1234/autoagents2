using System.Text.Json;
using PatientApi.Models;
using Xunit;

namespace PatientApi.Tests;

public class PatientModelSerializationTests
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void Patient_SerializesToJson_WithCamelCaseProperties()
    {
        var patient = new Patient
        {
            Id = new Guid("11111111-1111-1111-1111-111111111111"),
            Name = "ZhangSan",
            Gender = "Male",
            DateOfBirth = new DateTime(1990, 5, 15),
            Phone = "13800138000",
            Address = "Beijing Chaoyang",
            CreatedAt = new DateTime(2024, 1, 1),
            UpdatedAt = new DateTime(2024, 1, 1)
        };

        var json = JsonSerializer.Serialize(patient, _options);

        Assert.Contains("\"id\"", json);
        Assert.Contains("\"name\"", json);
        Assert.Contains("\"gender\"", json);
        Assert.Contains("\"dateOfBirth\"", json);
        Assert.Contains("\"phone\"", json);
        Assert.Contains("\"address\"", json);
        Assert.Contains("\"createdAt\"", json);
        Assert.Contains("\"updatedAt\"", json);
        Assert.Contains("ZhangSan", json);
        Assert.Contains("13800138000", json);
    }

    [Fact]
    public void Patient_DeserializesFromJson_WithCamelCaseProperties()
    {
        var json = """
            {
                "id": "22222222-2222-2222-2222-222222222222",
                "name": "李四",
                "gender": "Female",
                "dateOfBirth": "1985-03-20T00:00:00",
                "phone": "13900139000",
                "address": "上海市浦东新区",
                "createdAt": "2024-01-01T00:00:00",
                "updatedAt": "2024-01-01T00:00:00"
            }
            """;

        var patient = JsonSerializer.Deserialize<Patient>(json, _options);

        Assert.NotNull(patient);
        Assert.Equal(new Guid("22222222-2222-2222-2222-222222222222"), patient.Id);
        Assert.Equal("李四", patient.Name);
        Assert.Equal("Female", patient.Gender);
        Assert.Equal("13900139000", patient.Phone);
        Assert.Equal("上海市浦东新区", patient.Address);
    }

    [Fact]
    public void CreatePatientRequest_DeserializesFromJson()
    {
        var json = """
            {
                "name": "王五",
                "gender": "Male",
                "dateOfBirth": "1995-07-10T00:00:00",
                "phone": "13700137000",
                "address": "广州市天河区"
            }
            """;

        var request = JsonSerializer.Deserialize<CreatePatientRequest>(json, _options);

        Assert.NotNull(request);
        Assert.Equal("王五", request.Name);
        Assert.Equal("Male", request.Gender);
        Assert.Equal("13700137000", request.Phone);
        Assert.Equal("广州市天河区", request.Address);
    }

    [Fact]
    public void UpdatePatientRequest_DeserializesFromJson()
    {
        var json = """
            {
                "name": "赵六",
                "gender": "Female",
                "dateOfBirth": "2000-12-01T00:00:00",
                "phone": "13600136000"
            }
            """;

        var request = JsonSerializer.Deserialize<UpdatePatientRequest>(json, _options);

        Assert.NotNull(request);
        Assert.Equal("赵六", request.Name);
        Assert.Equal("Female", request.Gender);
        Assert.Equal("13600136000", request.Phone);
        Assert.Null(request.Address);
    }

    [Fact]
    public void Patient_SerializeAndDeserialize_RoundTrip()
    {
        var original = new Patient
        {
            Id = Guid.NewGuid(),
            Name = "测试患者",
            Gender = "Male",
            DateOfBirth = new DateTime(1980, 1, 1),
            Phone = "10000000000",
            Address = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(original, _options);
        var deserialized = JsonSerializer.Deserialize<Patient>(json, _options);

        Assert.NotNull(deserialized);
        Assert.Equal(original.Id, deserialized.Id);
        Assert.Equal(original.Name, deserialized.Name);
        Assert.Equal(original.Gender, deserialized.Gender);
        Assert.Equal(original.Phone, deserialized.Phone);
        Assert.Null(deserialized.Address);
    }
}
