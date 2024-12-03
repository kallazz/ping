using Grpc.Net.Client;
using Grpc.Core;
using PingServer;

namespace PingClient
{
    public class Client
    {
        private readonly PingService.PingServiceClient _client;
        private string? clientUsername;
        private Encryptor encryptor;
        private bool keyExchangeCompleted;
        public byte[]? PublicKey => encryptor.PublicKey;

        public Client()
        {
            encryptor = new Encryptor();
            keyExchangeCompleted = false;

            var handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

            DotNetEnv.Env.Load();
            var host = Environment.GetEnvironmentVariable("HOST")
                       ?? throw new InvalidOperationException("HOST is not set in the environment variables.");
            var port = Environment.GetEnvironmentVariable("PORT")
                       ?? throw new InvalidOperationException("PORT is not set in the environment variables.");

            var channel = GrpcChannel.ForAddress($"https://{host}:{port}", new GrpcChannelOptions { HttpHandler = handler });
            _client = new PingService.PingServiceClient(channel);

        }

        public bool KeyExchangeCompleted => keyExchangeCompleted;

        public async Task<bool> Login(string username, string password)
        {
            var request = new LoginRequest
            {
                Username = username,
                Password = password
            };

            var response = await _client.LoginAsync(request);

            if (response.Status == 0)
            {
                clientUsername = username;
                return true;
            }
            return false;
        }

        public async Task<bool> Register(string username, string email, string password1, string password2)
        {
            var request = new RegisterRequest
            {
                Username = username,
                Email = email,
                Password1 = password1,
                Password2 = password2
            };

            var response = await _client.RegisterAsync(request);

            if (response.Status == 0)
            {
                return true;
            }
            return false;
        }

        public async Task SendMessage(string message, string recipientUsername)
        {
            // TODO: handle key exchange and encryption
            // if (!keyExchangeCompleted)
            // {
            //     throw new InvalidOperationException("Shared key has not been established.");
            // }

            // var encryptedMessage = encryptor.Encrypt(message);
            var request = new MessageRequest
            {
                Client = clientUsername,
                Recipient = recipientUsername,
                Message = message
                // Message = Convert.ToBase64String(encryptedMessage)
            };

            var response = await _client.SendMessageAsync(request);
            if (response.Status == 0)
            {
                Console.WriteLine(response.Message);
            }
            else
            {
                Console.WriteLine(response.Message);
                // TODO: Handle message sending failure
            }
        }

        public async Task ProposeKeyExchange(string recipientUsername)
        {
            if (keyExchangeCompleted)
            {
                Console.WriteLine("Key exchange already completed.");
                return;
            }

            encryptor.GenerateNewKeyPair();
            keyExchangeCompleted = false;

            var request = new KeyExchangeRequest
            {
                Client = clientUsername,
                Recipient = recipientUsername,
                PublicKey = Google.Protobuf.ByteString.CopyFrom(encryptor.PublicKey),
                Init = true
            };

            var response = await _client.ProposeKeyExchangeAsync(request);

            if (response.Status == 0)
            {
                Console.WriteLine(response.Message);
                // wait for the recipient to respond
                await Task.Delay(100);


                if (keyExchangeCompleted)
                {
                    Console.WriteLine("Key exchange completed successfully.");
                }
                else
                {
                    Console.WriteLine("Key exchange failed.");
                    // TODO: Handle key exchange failure
                }
            }
            else
            {
                Console.WriteLine(response.Message);
                // TODO: Handle key exchange failure
            }

        }

        public async Task ReceiveMessages()
        {
            Console.WriteLine("Listening for messages...");
            using var call = _client.ReceiveMessages(new Empty { Client = clientUsername });

            try
            {
                while (await call.ResponseStream.MoveNext(default))
                {
                    var response = call.ResponseStream.Current;
                    Console.WriteLine($"Received message from {response.MessageResponse.Sender}: {response.MessageResponse.Content}");
                    if (response.MessageResponse.Type == "KeyExchangeInit")
                    {
                        Console.WriteLine("Key exchange initiated.");
                        encryptor.GenerateNewKeyPair();
                        encryptor.GenerateSharedKey(Convert.FromBase64String(response.MessageResponse.Content));
                        keyExchangeCompleted = true;
                        await _client.ProposeKeyExchangeAsync(new KeyExchangeRequest
                        {
                            Client = clientUsername,
                            Recipient = response.MessageResponse.Sender,
                            PublicKey = Google.Protobuf.ByteString.CopyFrom(encryptor.PublicKey),
                            Init = false
                        });
                    }
                    else if (response.MessageResponse.Type == "KeyExchangeResponse")
                    {
                        Console.WriteLine("Key exchange response received.");
                        encryptor.GenerateSharedKey(Convert.FromBase64String(response.MessageResponse.Content));
                        keyExchangeCompleted = true;
                    }
                    else
                    {
                        var decryptedMessage = encryptor.Decrypt(Convert.FromBase64String(response.MessageResponse.Content));
                        Console.WriteLine($"Decrypted message: {decryptedMessage}");
                    }
                }
            }
            catch (RpcException ex)
            {
                Console.WriteLine($"An error occurred while receiving messages: {ex.Status.Detail}");
            }
        }

        public async Task<List<string>?> GetFriendsList()
        {
            var friendsList = new List<string>();
            var request = new FriendListRequest
            {
                Client = clientUsername
            };

            var response = await _client.GetFriendsAsync(request);

            if (response.ExitCode.Status == 1)
            {
                return null;
            }

            var friends = response.MessageResponse.Content.Split(';');
            friendsList.AddRange(friends);
            return friendsList;
        }

        public async Task<bool> AddFriend(string friendUsername)
        {
            var request = new AddFriendRequest
            {
                Client = clientUsername,
                Friend = friendUsername
            };

            var response = await _client.AddFriendAsync(request);

            return (response.Status == 0) ? true : false;
        }
    }
}
