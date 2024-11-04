using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Formatter;
using MQTTnet.Protocol;
using System.Text;

namespace PingClient
{
    public class Client
    {
        public const string Host = "localhost";
        public const int Port = 1883;

        private readonly IMqttClient _mqttClient;
        private readonly string _userId;

        // TODO: in the future the id will be from the db
        public Client(string userId)
        {
            _userId = userId;

            var mqttFactory = new MqttFactory();
            _mqttClient = mqttFactory.CreateMqttClient();
        }

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
            var topic = $"recipient/{recipientId}";

            var mqttMessage = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(message)
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
                var payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);
                Console.WriteLine($"\nReceived message from topic '{topic}': {payload}");
                await Task.CompletedTask;
            };

            var topic = $"recipient/{_userId}";
            await _mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(topic).Build());
        }
    }
}
