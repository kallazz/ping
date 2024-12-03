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

        public Task<string?> GetUsernameByUserId(int userId)
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

        public async Task<List<string>?> GetFriendUsernameListFromUsername(string username)
        {
            var friendUsernames = new List<string>();

            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string query = @"
                        SELECT u2.username
                        FROM users u1
                        JOIN friends f ON u1.id = f.user_id
                        JOIN users u2 ON f.friend_id = u2.id
                        WHERE u1.username = @Username";

                    using (var command = new NpgsqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Username", username);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                friendUsernames.Add(reader.GetString(0));
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }

            Console.WriteLine($"I got {friendUsernames}");
            return friendUsernames;
        }

        public async Task<bool> IsUsersFriend(int userId, int friendId)
        {
            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string query = "SELECT 1 FROM friends WHERE user_id = @UserId AND friend_id = @FriendId LIMIT 1";
                    using (var command = new NpgsqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@UserId", userId);
                        command.Parameters.AddWithValue("@FriendId", friendId);

                        var result = await command.ExecuteScalarAsync();
                        return result != null;
                    }
                }
            }
            catch (Exception)
            {
                // This is returned so that this friend won't get inserted again
                // as we don't know if this friend is in the DB or not
                return true;
            }
        }

        public async Task<bool> InsertFriendIntoFriends(int userId, int friendId)
        {
            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string query = "INSERT INTO friends (user_id, friend_id) VALUES (@UserId, @FriendId)";
                    using (var command = new NpgsqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@UserId", userId);
                        command.Parameters.AddWithValue("@FriendId", friendId);

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

    }
}
