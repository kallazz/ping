using System.Security.Cryptography;
using System.Text;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Formatter;
using MQTTnet.Protocol;

namespace PingClient
{
    public class Client
    {
        public const string Host = "localhost";
        public const int Port = 1883;

        private readonly IMqttClient _mqttClient;
        private readonly string _userId;
        private Encryptor _encryptor;
        private bool _keyExchangeCompleted;
        public byte[]? PublicKey => _encryptor.PublicKey;

        public Client(string userId)
        {
            _userId = userId;
            _encryptor = new Encryptor();
            _keyExchangeCompleted = false;

            var mqttFactory = new MqttFactory();
            _mqttClient = mqttFactory.CreateMqttClient();
        }

        public bool KeyExchangeCompleted => _keyExchangeCompleted;

        public async Task Connect()
        {
            var mqttClientOptions = new MqttClientOptionsBuilder().WithTcpServer(Host, Port).WithProtocolVersion(MqttProtocolVersion.V500).Build();
            await _mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None);
        }

        public async Task Disconnect()
        {
            await _mqttClient.DisconnectAsync(new MqttClientDisconnectOptionsBuilder().WithReason(MqttClientDisconnectOptionsReason.NormalDisconnection).Build());
        }

        public async Task SendMessage(string message, string recipientId)
        {
            if (!_keyExchangeCompleted)
            {
                throw new InvalidOperationException("Shared key has not been established.");
            }

            var topic = $"recipient/{recipientId}";

            var encryptedMessage = _encryptor.Encrypt(message);
            var mqttMessage = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(encryptedMessage)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce)
                .WithRetainFlag(false)
                .Build();

            await _mqttClient.PublishAsync(mqttMessage, CancellationToken.None);
        }

        public async Task ReceiveMessages()
        {
            _mqttClient.ApplicationMessageReceivedAsync += async e =>
            {
                var topic = e.ApplicationMessage.Topic;
                var payload = e.ApplicationMessage.PayloadSegment.ToArray();
                var message = Encoding.UTF8.GetString(payload);

                if (topic.StartsWith("recipient/"))
                {
                    if (message.StartsWith("KeyExchange:"))
                    {
                        var parts = message.Split(':');
                        var senderId = parts[1];
                        var otherPublicKey = Convert.FromBase64String(parts[2]);
                        _encryptor.GenerateSharedKey(otherPublicKey);
                        Console.WriteLine($"Shared key established with user '{senderId}'.");

                        if (!_keyExchangeCompleted)
                        {
                            _keyExchangeCompleted = true;
                            // Respond with own public key
                            await RespondToKeyExchange(senderId);
                        }
                    }
                    else
                    {
                        var decryptedMessage = _encryptor.Decrypt(payload);
                        Console.WriteLine($"\nReceived message from topic '{topic}': {decryptedMessage}");
                    }
                }

                await Task.CompletedTask;
            };

            var topic = $"recipient/{_userId}";
            await _mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(topic).Build());
        }

        public void GenerateSharedKey(byte[] otherPublicKey)
        {
            _encryptor.GenerateSharedKey(otherPublicKey);
        }

        public void GenerateNewKeyPair()
        {
            _encryptor.GenerateNewKeyPair();
        }

        public async Task ProposeKeyExchange(string recipientId)
        {
            if (_keyExchangeCompleted)
            {
                Console.WriteLine("Key exchange already completed.");
                return;
            }

            var topic = $"recipient/{recipientId}";
            var payload = Encoding.UTF8.GetBytes($"KeyExchange:{_userId}:{Convert.ToBase64String(_encryptor.PublicKey)}");
            var mqttMessage = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce)
                .WithRetainFlag(false)
                .Build();

            await _mqttClient.PublishAsync(mqttMessage, CancellationToken.None);
        }

        private async Task RespondToKeyExchange(string recipientId)
        {
            var topic = $"recipient/{recipientId}";
            var payload = Encoding.UTF8.GetBytes($"KeyExchange:{_userId}:{Convert.ToBase64String(_encryptor.PublicKey)}");
            var mqttMessage = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce)
                .WithRetainFlag(false)
                .Build();

            await _mqttClient.PublishAsync(mqttMessage, CancellationToken.None);
        }
    }
}
