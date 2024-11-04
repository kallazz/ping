namespace PingClient
{
    public static class ClientManager
    {
        public static async void TerminalRun()
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
                Console.Write("Enter the message to send (or type 'exit' to quit): ");
                var message = Console.ReadLine() ?? string.Empty;

                if (message.ToLower() == "exit")
                {
                    break;
                }

                Console.Write("Enter the recipient ID: ");
                var recipientId = Console.ReadLine() ?? string.Empty;

                await mqttClient.SendMessage(message, recipientId);

                Console.WriteLine($"Message sent to user '{recipientId}': {message}");
            }

            await mqttClient.Disconnect();
        }

    }
}
