using Npgsql;
using PatientApi.Models;

namespace PatientApi.Data;

public class PatientRepository
{
    private readonly string _connectionString;

    public PatientRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<List<Patient>> GetAllAsync()
    {
        var patients = new List<Patient>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            "SELECT id, name, gender, date_of_birth, phone, address, created_at, updated_at FROM patients ORDER BY created_at DESC",
            conn);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            patients.Add(MapPatient(reader));
        }

        return patients;
    }

    public async Task<Patient?> GetByIdAsync(Guid id)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            "SELECT id, name, gender, date_of_birth, phone, address, created_at, updated_at FROM patients WHERE id = @id",
            conn);
        cmd.Parameters.AddWithValue("id", id);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapPatient(reader);
        }

        return null;
    }

    public async Task<Patient> CreateAsync(CreatePatientRequest request)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            @"INSERT INTO patients (name, gender, date_of_birth, phone, address)
              VALUES (@name, @gender, @dateOfBirth, @phone, @address)
              RETURNING id, name, gender, date_of_birth, phone, address, created_at, updated_at",
            conn);

        cmd.Parameters.AddWithValue("name", request.Name);
        cmd.Parameters.AddWithValue("gender", request.Gender);
        cmd.Parameters.AddWithValue("dateOfBirth", request.DateOfBirth.Date);
        cmd.Parameters.AddWithValue("phone", request.Phone);
        cmd.Parameters.AddWithValue("address", (object?)request.Address ?? DBNull.Value);

        await using var reader = await cmd.ExecuteReaderAsync();
        await reader.ReadAsync();
        return MapPatient(reader);
    }

    public async Task<Patient?> UpdateAsync(Guid id, UpdatePatientRequest request)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            @"UPDATE patients
              SET name = @name, gender = @gender, date_of_birth = @dateOfBirth,
                  phone = @phone, address = @address, updated_at = now()
              WHERE id = @id
              RETURNING id, name, gender, date_of_birth, phone, address, created_at, updated_at",
            conn);

        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("name", request.Name);
        cmd.Parameters.AddWithValue("gender", request.Gender);
        cmd.Parameters.AddWithValue("dateOfBirth", request.DateOfBirth.Date);
        cmd.Parameters.AddWithValue("phone", request.Phone);
        cmd.Parameters.AddWithValue("address", (object?)request.Address ?? DBNull.Value);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapPatient(reader);
        }

        return null;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            "DELETE FROM patients WHERE id = @id",
            conn);
        cmd.Parameters.AddWithValue("id", id);

        var affected = await cmd.ExecuteNonQueryAsync();
        return affected > 0;
    }

    private static Patient MapPatient(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetGuid(0),
        Name = reader.GetString(1),
        Gender = reader.GetString(2),
        DateOfBirth = reader.GetDateTime(3),
        Phone = reader.GetString(4),
        Address = reader.IsDBNull(5) ? null : reader.GetString(5),
        CreatedAt = reader.GetDateTime(6),
        UpdatedAt = reader.GetDateTime(7),
    };
}
