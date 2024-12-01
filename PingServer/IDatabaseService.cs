namespace PingServer
{
    public interface IDatabaseService
    {
        Task<bool> InsertUserIntoDatabase(string username, string email, string hashedPassword);
        Task<string?> GetPasswordForUserByUsername(string username);
        Task<string?> GetPasswordForUserByEmail(string email);

        Task<string?> GetUserIdByUsername(string username);
        Task<string?> GetUsernamesByUserId(string userId);
        Task<List<string>> GetFriendsUsernamesListFromUsername(string username);
    }
}
