namespace Telegram
{
  public class Client
  {
    public async Task Login()
    {
      using var client = new WTelegram.Client();
      var myself = await client.LoginUserIfNeeded();
      Console.WriteLine($"We are logged-in as {myself} (id {myself.id})");
    }
  }
}j