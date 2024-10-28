using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Formatter;

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

                var response = await mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None);

                Console.WriteLine("The MQTT client is connected.");

                await mqttClient.DisconnectAsync(new MqttClientDisconnectOptionsBuilder().WithReason(MqttClientDisconnectOptionsReason.NormalDisconnection).Build());
            }
        }
    }
}
