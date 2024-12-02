namespace PingServer
{
    public interface IDatabaseService
    {
        Task<bool> InsertUserIntoDatabase(string username, string email, string hashedPassword);

        Task<string?> GetPasswordForUserByUsername(string username);
        Task<string?> GetPasswordForUserByEmail(string email);

        Task<string?> GetUserIdByUsername(string username);
        Task<string?> GetUsernameByUserId(int userId);

        Task<List<string>?> GetFriendUsernameListFromUsername(string username);
        Task<bool> IsUsersFriend(int userId, int friendId);
        Task<bool> InsertFriendIntoFriends(int userId, int friendId);
    }
}
