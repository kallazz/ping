namespace PingClient
{
    public static class ClientManager
    {
        public static async Task TerminalRun()
        {
            Console.Write("Enter your user ID: ");
            var userId = Console.ReadLine();
            if (string.IsNullOrEmpty(userId))
            {
                Console.Write("Id was not provided");
                System.Environment.Exit(1);
            }

            var mqttClient = new PingClient.Client(userId);
            await mqttClient.Connect();
            await mqttClient.ReceiveMessages();

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

                        if (!mqttClient.KeyExchangeCompleted)
                        {
                            await mqttClient.ProposeKeyExchange(recipientId);

                            await Task.Delay(1000);
                        }

                        await mqttClient.SendMessage(message, recipientId);

                        Console.WriteLine($"Message sent to user '{recipientId}': {message}");
                        break;

                    case "2":
                        Console.Write("Enter the recipient ID: ");
                        recipientId = Console.ReadLine() ?? string.Empty;

                        await mqttClient.ProposeKeyExchange(recipientId);

                        Console.WriteLine($"Key exchange proposed to user '{recipientId}'");
                        break;

                    default:
                        Console.WriteLine("Invalid option. Please choose 1 or 2.");
                        break;
                }
            }

            await mqttClient.Disconnect();
        }
    }
}