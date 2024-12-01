using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Grpc.Core;
using System.Collections.Concurrent;
using Microsoft.VisualBasic;

namespace PingServer
{
    public static class Server
    {
        public static ConcurrentDictionary<string, IServerStreamWriter<ServerMessage>> clientConnections = new ConcurrentDictionary<string, IServerStreamWriter<ServerMessage>>();
        public static ConcurrentDictionary<string, string> clientIds = new ConcurrentDictionary<string, string>();
        public static ConcurrentDictionary<string, ConcurrentQueue<ServerMessage>> messageQueues = new ConcurrentDictionary<string, ConcurrentQueue<ServerMessage>>();
        private static IDatabaseService _databaseService;

        private static Authentication authentication;

        public static async Task Run()
        {
            _databaseService = new DatabaseService();
            authentication = new Authentication(_databaseService);
            var host = CreateHostBuilder().Build();
            await host.RunAsync();
        }

        public static IHostBuilder CreateHostBuilder()
        {
            DotNetEnv.Env.Load();
            var port = Environment.GetEnvironmentVariable("PORT")
                       ?? throw new InvalidOperationException("PORT is not set in the environment variables.");

            return Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                    webBuilder.UseUrls($"https://*:{port}");
                });
        }

        public static ConcurrentDictionary<string, string> GetClientIds()
        {
            return clientIds;
        }

        public static IDatabaseService getDatabaseService()
        {
            return _databaseService;
        }

        public static Authentication getAuthentication()
        {
            return authentication;
        }
    }

    public class PingServiceImpl : PingService.PingServiceBase
    {
        public override Task<ExitCode> SendMessage(MessageRequest request, ServerCallContext context)
        {
            Console.WriteLine($"Sent message from {request.ClientId} to {request.RecipientId}: {request.Message}");
            SendMessageToRecipient(request.ClientId, request.RecipientId, request.Message);
            return Task.FromResult(new ExitCode { Status = 0, Message = "Message sent" });
        }

        public override Task<ExitCode> ProposeKeyExchange(KeyExchangeRequest request, ServerCallContext context)
        {
            Console.WriteLine($"Key exchange proposed from {request.ClientId} to {request.RecipientId}");
            SendKeyExchangeToRecipient(request.ClientId, request.RecipientId, request.PublicKey.ToByteArray(), request.Init);
            return Task.FromResult(new ExitCode { Status = 0, Message = "Key exchange proposed" });
        }

        public override async Task ReceiveMessages(Empty request, IServerStreamWriter<ServerMessage> responseStream, ServerCallContext context)
        {
            var clientId = request.ClientId;

            Console.WriteLine($"Client {clientId} connected");
            Server.clientConnections[clientId] = responseStream;

            var messageQueue = Server.messageQueues.GetOrAdd(clientId, new ConcurrentQueue<ServerMessage>());

            while (!context.CancellationToken.IsCancellationRequested)
            {
                while (messageQueue.TryDequeue(out var message))
                {
                    await responseStream.WriteAsync(message);
                }

                await Task.Delay(100);
            }

            Server.clientConnections.TryRemove(clientId, out _);
        }

        public override Task<ExitCode> Login(LoginRequest request, ServerCallContext context)
        {
            var clientId = getClientId(request.Username);

            // TODO: if client does not exist, create registration handling
            if (ValidateLoginAsync(request.Username, request.Password).Result)
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

        public override Task<ExitCode> Register(RegisterRequest request, ServerCallContext context)
        {
            var clientId = getClientId(request.Username);

            if (ValidateRegisterAsync(request.Username, request.Email, request.Password1, request.Password2).Result)
            {
                Console.WriteLine($"User {request.Username} registered with User ID: {clientId}");
                return Task.FromResult(new ExitCode { Status = 0, Message = clientId });
            }
            else
            {
                Console.WriteLine($"Registration failed for user {request.Username}");
                return Task.FromResult(new ExitCode { Status = 1, Message = "Registration failed" });
            }
        }

        private async Task<bool> ValidateRegisterAsync(string username, string email, string password1, string password2)
        {
            var authentication = Server.getAuthentication();
            var validationError = await authentication.RegisterUser(username, email, password1, password2);

            if (validationError == ValidationError.None)
            {
                return true;
            }

            return false;
        }

        private async Task<bool> ValidateLoginAsync(string username, string password)
        {
            var authentication = Server.getAuthentication();
            var validationError = await authentication.LoginUser(username, password);

            if (validationError == ValidationError.None)
            {
                return true;
            }

            return false;
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

        private ExitCode SendMessageToRecipient(string clientId, string recipient, string message)
        {
            Console.WriteLine($"Sending message to {recipient} from {clientId}: {message}");
            var recipientId = Server.GetClientIds()[recipient];

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

        private ExitCode SendKeyExchangeToRecipient(string clientId, string recipient, byte[] publicKey, bool init)
        {
            Console.WriteLine($"Sending key exchange to {recipient} from {clientId}");
            var recipientId = Server.GetClientIds()[recipient];

            if (Server.clientConnections.TryGetValue(recipientId, out var connection))
            {
                var message = new ServerMessage
                {
                    MessageResponse = new MessageResponse
                    {
                        Type = init ? "KeyExchangeInit" : "KeyExchangeResponse",
                        Content = Convert.ToBase64String(publicKey),
                        Sender = GetKeyFromValue(Server.GetClientIds(), clientId)
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

        private string GetKeyFromValue(ConcurrentDictionary<string, string> dictionary, string value)
        {
            foreach (var kvp in dictionary)
            {
                if (kvp.Value == value)
                {
                    return kvp.Key;
                }
            }
            throw new KeyNotFoundException("The given value was not present in the dictionary.");
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