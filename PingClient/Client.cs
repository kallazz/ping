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
        private readonly string _userId;
        private Encryptor encryptor;
        private bool keyExchangeCompleted;
        public byte[]? PublicKey => encryptor.PublicKey;

        public Client(string userId)
        {
            _userId = userId;
            encryptor = new Encryptor();
            keyExchangeCompleted = false;

            var handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            
            var channel = GrpcChannel.ForAddress("https://localhost:5001", new GrpcChannelOptions { HttpHandler = handler });
            _client = new PingService.PingServiceClient(channel);
        }

        public bool KeyExchangeCompleted => keyExchangeCompleted;

        public async Task SendMessage(string message, string recipientId)
        {
            if (!keyExchangeCompleted)
            {
                throw new InvalidOperationException("Shared key has not been established.");
            }

            var encryptedMessage = encryptor.Encrypt(message);
            var request = new MessageRequest
            {
                UserId = _userId,
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
                UserId = _userId,
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