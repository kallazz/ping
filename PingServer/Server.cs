using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Grpc.Core;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using PingServer;

namespace PingServer
{
    public static class Server
    {
        public static ConcurrentDictionary<string, IServerStreamWriter<ServerMessage>> clientConnections = new ConcurrentDictionary<string, IServerStreamWriter<ServerMessage>>();
        public static ConcurrentDictionary<string, string> clientIds = new ConcurrentDictionary<string, string>();
        public static ConcurrentDictionary<string, ConcurrentQueue<ServerMessage>> messageQueues = new ConcurrentDictionary<string, ConcurrentQueue<ServerMessage>>();
    
        public static async Task Run()
        {
            var host = CreateHostBuilder().Build();
            await host.RunAsync();
        }
    
        public static IHostBuilder CreateHostBuilder() =>
            Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                    webBuilder.UseUrls("http://localhost:5000", "https://localhost:5001");
                });
    
        public static ConcurrentDictionary<string, string> GetClientIds()
        {
            return clientIds;
        }
    }

    public class PingServiceImpl : PingService.PingServiceBase
    {
        public override Task<ExitCode> SendMessage(MessageRequest request, ServerCallContext context)
        {
            Console.WriteLine($"Received message from {request.ClientId} to {request.RecipientId}: {request.Message}");
            SendMessageToRecipient(request.ClientId, request.RecipientId, request.Message);
            return Task.FromResult(new ExitCode { Status = 0, Message = "Message sent" });
        }
    
        public override Task<ExitCode> ProposeKeyExchange(KeyExchangeRequest request, ServerCallContext context)
        {
            Console.WriteLine($"Key exchange proposed from {request.ClientId} to {request.RecipientId}");
            SendKeyExchangeToRecipient(request.ClientId, request.RecipientId, request.PublicKey.ToByteArray());
            return Task.FromResult(new ExitCode { Status = 0, Message = "Key exchange proposed" });
        }

        public override async Task ReceiveMessages(Empty request, IServerStreamWriter<ServerMessage> responseStream, ServerCallContext context)
        {
            var clientId = context.GetHttpContext().Connection.Id;
        
            if (Server.clientConnections.TryGetValue(clientId, out var connection))
            {
                var messageQueue = Server.messageQueues.GetOrAdd(clientId, new ConcurrentQueue<ServerMessage>());
        
                while (!context.CancellationToken.IsCancellationRequested)
                {
                    while (messageQueue.TryDequeue(out var message))
                    {
                        await responseStream.WriteAsync(message);
                    }
        
                    await Task.Delay(100);
                }
            }
            else
            {
                Console.WriteLine($"Client {clientId} not connected.");
            }
        }
    
        public override Task<ExitCode> Login(LoginRequest request, ServerCallContext context)
        {
            var clientId = getClientId(request.Username);
    
            // TODO: if client does not exist, create registration handling
            if (ValidateUser(request.Username, request.Password))
            {
                Console.WriteLine($"User {request.Username} logged in with User ID: {clientId}");
                return Task.FromResult(new ExitCode { Status = 0, Message = clientId });
            }
            else
            {
                Console.WriteLine($"Login failed for user {request.Username}");
                return Task.FromResult(new ExitCode { Status = 1, Message = "Login failed" });
            }
        }
    
        private bool ValidateUser(string username, string password)
        {
            // TODO: Implement user validation
            return true;
        }
    
        private string getClientId(string username)
        {
            var clientIds = Server.GetClientIds();
            if (clientIds.ContainsKey(username))
            {
                Console.WriteLine($"User {username} already exists");
                return clientIds[username];
            }
            else
            {
                var clientId = Guid.NewGuid().ToString();
                clientIds.TryAdd(username, clientId);
                Console.WriteLine($"User {username} created with User ID: {clientId}");
                return clientId;
            }
        }
    
        private ExitCode SendMessageToRecipient(string clientId, string recipientId, string message)
        {
            Console.WriteLine($"Sending message to {recipientId} from {clientId}: {message}");
        
            if (Server.clientConnections.TryGetValue(recipientId, out var connection))
            {
                var response = new ServerMessage
                {
                    MessageResponse = new MessageResponse { Type = "Message", Content = message, Sender = clientId }
                };
        
                var messageQueue = Server.messageQueues.GetOrAdd(recipientId, new ConcurrentQueue<ServerMessage>());
                messageQueue.Enqueue(response);
        
                return new ExitCode { Status = 0, Message = "Message sent" };
            }
            else
            {
                Console.WriteLine($"Recipient {recipientId} not connected.");
                return new ExitCode { Status = 1, Message = "Recipient not connected" };
            }
        }
        
        private ExitCode SendKeyExchangeToRecipient(string clientId, string recipientId, byte[] publicKey)
        {
            Console.WriteLine($"Sending key exchange to {recipientId} from {clientId}");
            if (Server.clientConnections.TryGetValue(recipientId, out var connection))
            {
                var message = new ServerMessage
                {
                    MessageResponse = new MessageResponse
                    {
                        Type = "KeyExchange",
                        Content = Convert.ToBase64String(publicKey),
                        Sender = clientId
                    }
                };
        
                var messageQueue = Server.messageQueues.GetOrAdd(recipientId, new ConcurrentQueue<ServerMessage>());
                messageQueue.Enqueue(message);
        
                return new ExitCode { Status = 0, Message = "Key exchange sent" };
            }
            else
            {
                Console.WriteLine($"Recipient {recipientId} not connected.");
                return new ExitCode { Status = 1, Message = "Recipient not connected" };
            }
        }
    }

    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddGrpc();
        }
    
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
    
            app.UseRouting();
    
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGrpcService<PingServiceImpl>();
            });
        }
    }
}