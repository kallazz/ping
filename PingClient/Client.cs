using Grpc.Net.Client;
using Grpc.Core;
using System;
using System.Text;
using System.Threading.Tasks;
using PingServer;

namespace PingClient
{
    public class Client
    {
        private readonly PingService.PingServiceClient _client;
        private string? clientId;
        private Encryptor encryptor;
        private bool keyExchangeCompleted;
        public byte[]? PublicKey => encryptor.PublicKey;

        public Client()
        {
            encryptor = new Encryptor();
            keyExchangeCompleted = false;

            var handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            
            var channel = GrpcChannel.ForAddress("https://localhost:5001", new GrpcChannelOptions { HttpHandler = handler });
            _client = new PingService.PingServiceClient(channel);

        }

        public bool KeyExchangeCompleted => keyExchangeCompleted;

        public async Task<bool> Login(string username, string password)
        {
            try
            {
                var request = new LoginRequest
                {
                    Username = username,
                    Password = password
                };

                var response = await _client.LoginAsync(request);
                if (response.Status == 0)
                {
                    clientId = response.Message;
                    Console.WriteLine($"User {username} logged in with User ID: {clientId}");
                    return true;
                }
                else
                {
                    Console.WriteLine(response.Message);
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred during login: {ex.Message}");
                return false;
            }
        }

        public async Task SendMessage(string message, string recipientId)
        {
            if (!keyExchangeCompleted)
            {
                throw new InvalidOperationException("Shared key has not been established.");
            }
        
            var encryptedMessage = encryptor.Encrypt(message);
            var request = new MessageRequest
            {
                ClientId = clientId,
                RecipientId = recipientId,
                Message = Convert.ToBase64String(encryptedMessage)
            };
        
            var response = await _client.SendMessageAsync(request);
            if(response.Status == 0)
            {
                Console.WriteLine(response.Message);
            }
            else
            {
                Console.WriteLine(response.Message);
                // TODO: Handle message sending failure
            }
        }

        public async Task ProposeKeyExchange(string recipientId)
        {
            if (keyExchangeCompleted)
            {
                Console.WriteLine("Key exchange already completed.");
                return;
            }
        
            if (string.IsNullOrEmpty(clientId))
            {
                throw new InvalidOperationException("User is not logged in.");
            }
        
            encryptor.GenerateNewKeyPair();
            keyExchangeCompleted = false;
        
            var request = new KeyExchangeRequest
            {
                ClientId = clientId,
                RecipientId = recipientId,
                PublicKey = Google.Protobuf.ByteString.CopyFrom(encryptor.PublicKey),
                Init = true
            };
        
            var response = await _client.ProposeKeyExchangeAsync(request);
            
            if(response.Status == 0)
            {
                Console.WriteLine(response.Message);
                // wait for the recipient to respond
                await Task.Delay(100);
                

                if(keyExchangeCompleted)
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
            using var call = _client.ReceiveMessages(new Empty { ClientId = clientId });
            
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
                            ClientId = clientId,
                            RecipientId = response.MessageResponse.Sender,
                            PublicKey = Google.Protobuf.ByteString.CopyFrom(encryptor.PublicKey),
                            Init = false
                        });
                    }
                    else if(response.MessageResponse.Type == "KeyExchangeResponse")
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
    }
}