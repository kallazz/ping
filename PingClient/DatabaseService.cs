using Npgsql;

namespace PingClient
{
    public class DatabaseService : IDatabaseService
    {
        private readonly string _connectionString;

        public DatabaseService()
        {
            DotNetEnv.Env.Load();
            _connectionString = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING")
                                ?? throw new InvalidOperationException("DATABASE_CONNECTION_STRING is not set in the environment variables.");
        }

        public async Task<bool> InsertUserIntoDatabase(string username, string email, string hashedPassword)
        {
            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string query = "INSERT INTO users (username, email, password) VALUES (@Username, @Email, @Password)";
                    using (var command = new NpgsqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Username", username);
                        command.Parameters.AddWithValue("@Email", email);
                        command.Parameters.AddWithValue("@Password", hashedPassword);

                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        public async Task<string?> GetPasswordForUserByUsername(string username)
        {
            return await GetPasswordForUser("username", username);
        }

        public async Task<string?> GetPasswordForUserByEmail(string email)
        {
            return await GetPasswordForUser("email", email);
        }

        private async Task<string?> GetPasswordForUser(string fieldName, string value)
        {
            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string query = $"SELECT password FROM users WHERE {fieldName} = @Value";
                    using (var command = new NpgsqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Value", value);

                        object? result = await command.ExecuteScalarAsync();
                        return result?.ToString();
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
