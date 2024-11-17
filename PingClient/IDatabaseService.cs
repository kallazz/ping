namespace PingClient
{
    public interface IDatabaseService
    {
        Task<bool> InsertUserIntoDatabase(string username, string email, string hashedPassword);
        Task<string?> GetPasswordForUserByUsername(string username);
        Task<string?> GetPasswordForUserByEmail(string email);
    }
}
