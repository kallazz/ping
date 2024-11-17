using System.Data.SqlClient;

namespace PingClient
{
    public class DatabaseService : IDatabaseService
    {
        private const string ConnectionString = "";

        public async Task<bool> InsertUserIntoDatabase(string email, string username, string hashedPassword)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(ConnectionString))
                {
                    await connection.OpenAsync();

                    string query = "INSERT INTO users (username, email, password) VALUES (@Username, @Email, @Password)";
                    using (SqlCommand command = new SqlCommand(query, connection))
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
                using (SqlConnection connection = new SqlConnection(ConnectionString))
                {
                    await connection.OpenAsync();

                    string query = $"SELECT password FROM users WHERE {fieldName} = @Value";
                    using (SqlCommand command = new SqlCommand(query, connection))
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
