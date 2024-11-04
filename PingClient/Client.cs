using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Formatter;
using MQTTnet.Protocol;
using System.Text;

namespace PingClient
{
    public static class Client
    {
        public static async Task ConnectAndDisconnectClientUsingMQTTv5()
        {
            var mqttFactory = new MqttFactory();

            using (var mqttClient = mqttFactory.CreateMqttClient())
            {
                var mqttClientOptions = new MqttClientOptionsBuilder().WithTcpServer("localhost", 1883).WithProtocolVersion(MqttProtocolVersion.V500).Build();

                await mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None);

                Console.WriteLine("The MQTT client is connected.");

                _ = ReceiveMessagesAsync(mqttClient);

                await SendMessageAsync(mqttClient);

                await mqttClient.DisconnectAsync(new MqttClientDisconnectOptionsBuilder().WithReason(MqttClientDisconnectOptionsReason.NormalDisconnection).Build());
            }
        }

        private static async Task SendMessageAsync(IMqttClient mqttClient)
        {
            while (true)
            {
                Console.Write("Enter the message to send (or type 'exit' to quit): ");
                string message = Console.ReadLine() ?? string.Empty;

                if (message?.ToLower() == "exit")
                {
                    break;
                }

                Console.Write("Enter the recipient ID: ");
                string recipientId = Console.ReadLine() ?? string.Empty;

                string topic = $"recipient/{recipientId}";

                var mqttMessage = new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(message)
                    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce)
                    .WithRetainFlag(false)
                    .Build();

                await mqttClient.PublishAsync(mqttMessage, CancellationToken.None);
                Console.WriteLine($"Message sent to topic '{topic}': {message}");
            }
        }

        private static async Task ReceiveMessagesAsync(IMqttClient mqttClient)
        {
            mqttClient.ApplicationMessageReceivedAsync += async e =>
            {
                var topic = e.ApplicationMessage.Topic;
                var payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                Console.WriteLine($"Received message from topic '{topic}': {payload}");
                await Task.CompletedTask;
            };

            // For now subscribe all topics
            // TODO: Resolve problem with receiving self-send messages
            await mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic("#").Build());
            Console.WriteLine("Subscribed to all topics.");
        }
    }
}
