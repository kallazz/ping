using System;
using System.Threading.Tasks;

namespace PingClient
{
    public static class ClientManager
    {
        public static async Task TerminalRun()
        {
            var grpcClient = new Client();

            Console.Write("Enter your username: ");
            var username = Console.ReadLine();
            if (string.IsNullOrEmpty(username))
            {
                Console.Write("Id was not provided");
                Environment.Exit(1);
            }

            Console.Write("Enter your password: ");
            var password = Console.ReadLine();
            if (string.IsNullOrEmpty(password))
            {
                Console.Write("Password was not provided");
                Environment.Exit(1);
            }

            bool loginSuccess = await grpcClient.Login(username, password);
            if (!loginSuccess)
            {
                Console.WriteLine("Login failed. Please check your username and password.");
                return;
            }

            Console.WriteLine("Login successful!");

            while (true)
            {
                Console.WriteLine("Choose an option:");
                Console.WriteLine("1. Send a message");
                Console.WriteLine("2. Propose key exchange");
                Console.WriteLine("Type 'exit' to quit");
                var choice = Console.ReadLine() ?? string.Empty;

                if (choice.ToLower() == "exit")
                {
                    break;
                }

                switch (choice)
                {
                    case "1":
                        Console.Write("Enter the message to send: ");
                        var message = Console.ReadLine() ?? string.Empty;

                        Console.Write("Enter the recipient ID: ");
                        var recipientId = Console.ReadLine() ?? string.Empty;

                        if (!grpcClient.KeyExchangeCompleted)
                        {
                            await grpcClient.ProposeKeyExchange(recipientId);
                            await Task.Delay(1000);
                        }

                        await grpcClient.SendMessage(message, recipientId);
                        Console.WriteLine($"Message sent to user '{recipientId}': {message}");
                        break;

                    case "2":
                        Console.Write("Enter the recipient ID: ");
                        recipientId = Console.ReadLine() ?? string.Empty;

                        await grpcClient.ProposeKeyExchange(recipientId);
                        Console.WriteLine($"Key exchange proposed to user '{recipientId}'");
                        break;

                    default:
                        Console.WriteLine("Invalid option. Please choose 1 or 2.");
                        break;
                }
            }
        }
    }
}