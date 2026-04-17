using System.Text.Json.Serialization;
using PatientApi.Models;

namespace PatientApi;

[JsonSerializable(typeof(Patient))]
[JsonSerializable(typeof(List<Patient>))]
[JsonSerializable(typeof(CreatePatientRequest))]
[JsonSerializable(typeof(UpdatePatientRequest))]
[JsonSerializable(typeof(object))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}
