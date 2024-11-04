using MQTTnet;
using MQTTnet.Server;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace PingServer
{
    public static class Server
    {
        private static ConcurrentDictionary<string, string> clientIDs = new ConcurrentDictionary<string, string>();

        public static async Task Run()
        {
            var mqttFactory = new MqttFactory();

            var mqttServerOptions = new MqttServerOptionsBuilder()
                .WithDefaultEndpoint()
                .Build();

            var mqttServer = mqttFactory.CreateMqttServer(mqttServerOptions);

            mqttServer.ClientConnectedAsync += async e =>
            {
                var clientId = Guid.NewGuid().ToString();
                clientIDs[e.ClientId] = clientId;
                Console.WriteLine($"Client {e.ClientId} connected with assigned ID: {clientId}");
                await Task.CompletedTask;
            };

            await mqttServer.StartAsync();

            Console.WriteLine("Press Enter to exit.");
            Console.ReadLine();

            await mqttServer.StopAsync();
        }
    }
}
