using System;
using System.Threading.Tasks;
using MQTTnet.Samples.Server;

class Program
{
    static async Task Main(string[] args)
    {
        await Server_Simple_Samples.Publish_Message_From_Broker();

        Console.WriteLine("Press Enter to exit.");
        Console.ReadLine();
    }
}