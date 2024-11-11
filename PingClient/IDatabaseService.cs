namespace PingClient
{
    public interface IDatabaseService
    {
        Task<bool> InsertUserIntoDatabase(string username, string email, string hashedPassword);
    }
}
