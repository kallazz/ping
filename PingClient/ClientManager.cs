namespace PingClient
{
    public static class ClientManager
    {
        public static async Task TerminalRun()
        {
            var grpcClient = new Client();

            Console.WriteLine("Choose an option:");
            Console.WriteLine("1. Login");
            Console.WriteLine("2. Register");
            var choice = Console.ReadLine();

            if (choice == "1")
            {
                Console.Write("Enter your username or email: ");
                var username = Console.ReadLine();
                if (string.IsNullOrEmpty(username))
                {
                    Console.Write("Username or email was not provided.");
                    Environment.Exit(1);
                }

                Console.Write("Enter your password: ");
                var password = Console.ReadLine();
                if (string.IsNullOrEmpty(password))
                {
                    Console.Write("Password was not provided.");
                    Environment.Exit(1);
                }

                var isLogged = await grpcClient.Login(username, password);

                if (!isLogged)
                {
                    Console.Write("Login failed.");
                    return;
                }
                else
                {
                    Console.Write("Login successful.");
                }

                var receiveMessagesTask = grpcClient.ReceiveMessages();

                while (true)
                {
                    // list friends
                    var friendList = grpcClient.GetFriendsList();
                    Console.WriteLine("Your friends:");
                    foreach (var friend in friendList)
                    {
                        Console.WriteLine(friend);
                    }
                    Console.WriteLine();
                    Console.WriteLine("Choose an option:");
                    Console.WriteLine("1. Add friend");
                    Console.WriteLine("2. Choose friend");
                    Console.WriteLine("Type 'exit' to quit");
                    choice = Console.ReadLine() ?? string.Empty;

                    if (choice.ToLower() == "exit")
                    {
                        continue;
                    }

                    switch (choice)
                    {
                        case "1":
                            Console.WriteLine("Choose an option:");
                            Console.WriteLine("1. Show friend list");
                            Console.WriteLine("2. Provide username");
                            Console.WriteLine("Type 'exit' to quit");
                            choice = Console.ReadLine() ?? string.Empty;
                            break;
                        case "2":
                            Console.WriteLine("Choose an option:");
                            Console.WriteLine("1. Send a message");
                            Console.WriteLine("2. Propose key exchange");
                            Console.WriteLine("Type 'exit' to quit");
                            choice = Console.ReadLine() ?? string.Empty;

                            if (choice.ToLower() == "exit")
                            {
                                continue;
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

                            break;
                    }
                }
            }
            else if (choice == "2")
            {
                Console.Write("Enter your username: ");
                var username = Console.ReadLine();
                if (string.IsNullOrEmpty(username))
                {
                    Console.Write("Username was not provided.");
                    Environment.Exit(1);
                }

                Console.Write("Enter your email: ");
                var email = Console.ReadLine();
                if (string.IsNullOrEmpty(email))
                {
                    Console.Write("Email was not provided.");
                    Environment.Exit(1);
                }

                Console.Write("Enter your password: ");
                var password1 = Console.ReadLine();
                if (string.IsNullOrEmpty(password1))
                {
                    Console.Write("Password was not provided.");
                    Environment.Exit(1);
                }

                Console.Write("Enter your password again: ");
                var password2 = Console.ReadLine();
                if (string.IsNullOrEmpty(password2))
                {
                    Console.Write("Second password was not provided.");
                    Environment.Exit(1);
                }

                var isRegistered = await grpcClient.Register(username, email, password1, password2);

                if(!isRegistered)
                {
                    Console.Write("Registration failed.");
                    return;
                }
                else
                {
                    Console.Write("Registration successful.");
                    return;
                }
            }
            else
            {
                Console.Write("None of the correct options was chosen.");
                return;
            }
        }
    }
}
