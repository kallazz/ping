using Npgsql;

namespace PingServer
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
                System.Console.WriteLine("Inserting user into database");
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
            catch (Exception ex)
            {
                System.Console.WriteLine("An error occurred while inserting user into database");
                System.Console.WriteLine(ex.Message);
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

        public async Task<List<string>> GetFriendsList(string username)
        {
            var friendsList = new List<string>();
        
            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
        
                    string userIdQuery = "SELECT id FROM users WHERE username = @Username";
                    int userId;
        
                    using (var userIdCommand = new NpgsqlCommand(userIdQuery, connection))
                    {
                        userIdCommand.Parameters.AddWithValue("@Username", username);
                        userId = (int)(await userIdCommand.ExecuteScalarAsync() ?? throw new Exception("User not found"));
                    }
        
                    string friendsQuery = @"
                        SELECT u.username
                        FROM friends f
                        JOIN users u ON u.id = f.friend
                        WHERE f.id = @UserId";
        
                    using (var friendsCommand = new NpgsqlCommand(friendsQuery, connection))
                    {
                        friendsCommand.Parameters.AddWithValue("@UserId", userId);
        
                        using (var reader = await friendsCommand.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                friendsList.Add(reader.GetString(0));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while retrieving friends list: {ex.Message}");
            }
        
            return friendsList;
        }

        public Task<string?> GetUserIdByUsername(string username)
        {
            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();

                    string query = "SELECT id FROM users WHERE username = @Username";
                    using (var command = new NpgsqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Username", username);

                        object? result = command.ExecuteScalar();
                        return Task.FromResult(result?.ToString());
                    }
                }
            }
            catch (Exception)
            {
                return Task.FromResult<string?>(null);
            }
        }

        public Task<string?> GetUsernamesByUserId(string userId)
        {
            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();

                    string query = "SELECT username FROM users WHERE id = @UserId";
                    using (var command = new NpgsqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@UserId", userId);

                        object? result = command.ExecuteScalar();
                        return Task.FromResult(result?.ToString());
                    }
                }
            }
            catch (Exception)
            {
                return Task.FromResult<string?>(null);
            }
        }
    }
}
