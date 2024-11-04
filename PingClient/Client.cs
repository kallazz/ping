using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Formatter;
using MQTTnet.Protocol;

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

                Console.WriteLine("The MQTT client is connected.");

                SendMessageAsync(mqttClient);

                await mqttClient.DisconnectAsync(new MqttClientDisconnectOptionsBuilder().WithReason(MqttClientDisconnectOptionsReason.NormalDisconnection).Build());
            }
        }

        private static async Task SendMessageAsync(IMqttClient mqttClient)
        {
            Console.Write("Enter the message to send: ");
            string message = Console.ReadLine();

            Console.Write("Enter the recipient ID: ");
            string recipientId = Console.ReadLine();

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
}
