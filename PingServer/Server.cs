using MQTTnet;
using MQTTnet.Server;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Text;

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

            mqttServer.InterceptingPublishAsync += args =>
            {
                var topic = args.ApplicationMessage.Topic;
                var message = Encoding.UTF8.GetString(args.ApplicationMessage.PayloadSegment.ToArray());
                var clientId = args.ClientId;
                Console.WriteLine($"Received message on topic '{topic}': {message}");
                Console.WriteLine($"Message was sent by user with ClientId: {clientId}");
                return Task.CompletedTask;
            };

            await mqttServer.StartAsync();

            Console.WriteLine("Press Enter to exit.");
            Console.ReadLine();

            await mqttServer.StopAsync();
        }
    }
}
