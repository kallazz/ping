using Grpc.Net.Client;
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
            if (response.Status == "Login successful")
            {
                clientId = response.ClientId;
                Console.WriteLine($"User {username} logged in with User ID: {clientId}");
                return true;
            }
            else
            {
                Console.WriteLine($"Login failed for user {username}");
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
            Console.WriteLine(response.Status);
        }

        public async Task ProposeKeyExchange(string recipientId)
        {
            if (keyExchangeCompleted)
            {
                Console.WriteLine("Key exchange already completed.");
                return;
            }

            var request = new KeyExchangeRequest
            {
                ClientId = clientId,
                RecipientId = recipientId
            };

            var response = await _client.ProposeKeyExchangeAsync(request);
            Console.WriteLine(response.Status);

            if (response.Status == "Key exchange proposed")
            {
                keyExchangeCompleted = true;
            }
        }

        public void GenerateSharedKey(byte[] otherPublicKey)
        {
            encryptor.GenerateSharedKey(otherPublicKey);
        }

        public void GenerateNewKeyPair()
        {
            encryptor.GenerateNewKeyPair();
        }
    }
}