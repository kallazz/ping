using System;
using System.Text;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Server;
using Serilog;

namespace Server
{
    class Program
    {
        private static int MessageCounter = 0;

        static async Task Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateLogger();

            var options = new MqttServerOptionsBuilder()
                .WithDefaultEndpoint()
                .WithDefaultEndpointPort(707)
                .WithConnectionValidator(OnNewConnection)
                .WithApplicationMessageInterceptor(OnNewMessage)
                .Build();

            var mqttServer = new MqttFactory().CreateMqttServer();

            try
            {
                await mqttServer.StartAsync(options);
                Log.Logger.Information("MQTT server started on port 707.");
                Console.WriteLine("Press any key to exit.");
                Console.ReadLine();
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Error starting MQTT server");
            }
            finally
            {
                await mqttServer.StopAsync();
                Log.Logger.Information("MQTT server stopped.");
                Log.CloseAndFlush();
            }
        }

        public static void OnNewConnection(MqttConnectionValidatorContext context)
        {
            Log.Logger.Information(
                "New connection: ClientId = {clientId}, Endpoint = {endpoint}",
                context.ClientId,
                context.Endpoint);
        }

        public static void OnNewMessage(MqttApplicationMessageInterceptorContext context)
        {
            var payload = context.ApplicationMessage?.Payload == null 
                ? null 
                : Encoding.UTF8.GetString(context.ApplicationMessage.Payload);

            MessageCounter++;

            Log.Logger.Information(
                "MessageId: {MessageCounter} - TimeStamp: {TimeStamp} -- Message: ClientId = {clientId}, Topic = {topic}, Payload = {payload}, QoS = {qos}, Retain-Flag = {retainFlag}",
                MessageCounter,
                DateTime.Now,
                context.ClientId,
                context.ApplicationMessage?.Topic,
                payload,
                context.ApplicationMessage?.QualityOfServiceLevel,
                context.ApplicationMessage?.Retain);
        }
    }
}

