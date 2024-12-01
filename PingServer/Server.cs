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
        private static IDatabaseService _databaseService;
        private static Authentication authentication;
        public static ConcurrentDictionary<string, IServerStreamWriter<ServerMessage>> clientConnections = new ConcurrentDictionary<string, IServerStreamWriter<ServerMessage>>();
        public static ConcurrentDictionary<string, ConcurrentQueue<ServerMessage>> messageQueues = new ConcurrentDictionary<string, ConcurrentQueue<ServerMessage>>();

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
        public override async Task<ExitCode> SendMessage(MessageRequest request, ServerCallContext context)
        {
            var _databaseService = Server.getDatabaseService();
            string recipientID;
            string clientID;
            try
            {
                clientID = await _databaseService.GetUserIdByUsername(request.Client);
                recipientID = await _databaseService.GetUserIdByUsername(request.Recipient);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return new ExitCode { Status = 1, Message = "Error getting userID" };
            }

            Console.WriteLine($"Sent message from {request.Client} to {request.Recipient}: {request.Message}");
            SendMessageToRecipient(clientID, recipientID, request.Message);
            return new ExitCode { Status = 0, Message = "Message sent" };
        }

        public override async Task<ExitCode> ProposeKeyExchange(KeyExchangeRequest request, ServerCallContext context)
        {
            var _databaseService = Server.getDatabaseService();
            string recipientID;
            string clientID;
            try
            {
                clientID = await _databaseService.GetUserIdByUsername(request.Client);
                recipientID = await _databaseService.GetUserIdByUsername(request.Recipient);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return new ExitCode { Status = 1, Message = "Error getting userID" };
            }

            Console.WriteLine($"Key exchange proposed from {request.Client} to {request.Recipient}");
            SendKeyExchangeToRecipient(clientID, recipientID, request.PublicKey.ToByteArray(), request.Init);
            return new ExitCode { Status = 0, Message = "Key exchange proposed" };
        }

        public override async Task<ExitCode> ReceiveMessages(Empty request, IServerStreamWriter<ServerMessage> responseStream, ServerCallContext context)
        {
            if (string.IsNullOrEmpty(request.Client))
            {
                return new ExitCode { Status = 1, Message = "Client ID cannot be null or empty" };
            }

            var _databaseService = Server.getDatabaseService();
            string clientId;
            try
            {
                clientId = await _databaseService.GetUserIdByUsername(request.Client);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return new ExitCode { Status = 1, Message = "Error getting username" };
            }

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
            Server.messageQueues.TryRemove(clientId, out _);

            Console.WriteLine($"Client {clientId} disconnected");
            return new ExitCode { Status = 0, Message = "Client disconnected" };
        }

        public override async Task<ExitCode> Login(LoginRequest request, ServerCallContext context)
        {
            var _databaseService = Server.getDatabaseService();
            string clientId;
            try
            {
                clientId = await _databaseService.GetUsernamesByUserId(request.Username);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return new ExitCode { Status = 1, Message = "Error getting username" };
            }

            // TODO: if client does not exist, create registration handling
            var validationMsg = ValidateLoginAsync(request.Username, request.Password).Result;
            if (validationMsg == ValidationError.None)
            {
                Console.WriteLine($"User logged in");
                return new ExitCode { Status = 0, Message = "Welcome to server" };
            }
            else
            {
                Console.WriteLine($"Login failed for user {request.Username} with error {validationMsg}");
                return new ExitCode { Status = 1, Message = "Login failed" };
            }
        }

        public override Task<ExitCode> Register(RegisterRequest request, ServerCallContext context)
        {

            var validateMsg = ValidateRegisterAsync(request.Username, request.Email, request.Password1, request.Password2).Result;

            if (validateMsg == ValidationError.None)
            {
                return Task.FromResult(new ExitCode { Status = 0, Message = "Welcome to server" });
            }
            else
            {
                Console.WriteLine($"Registration failed for user  with error {validateMsg}");
                return Task.FromResult(new ExitCode { Status = 1, Message = "Registration failed" });
            }
        }

        private async Task<ValidationError> ValidateRegisterAsync(string username, string email, string password1, string password2)
        {
            var authentication = Server.getAuthentication();
            var validationError = await authentication.RegisterUser(username, email, password1, password2);

            return validationError;
        }

        private async Task<ValidationError> ValidateLoginAsync(string username, string password)
        {
            var authentication = Server.getAuthentication();
            var validationError = await authentication.LoginUser(username, password);

            return validationError;
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

        private ExitCode SendKeyExchangeToRecipient(string clientId, string recipientId, byte[] publicKey, bool init)
        {
            Console.WriteLine($"Sending key exchange to {recipientId} from {clientId}");

            if (Server.clientConnections.TryGetValue(recipientId, out var connection))
            {
                var message = new ServerMessage
                {
                    MessageResponse = new MessageResponse
                    {
                        Type = init ? "KeyExchangeInit" : "KeyExchangeResponse",
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

        public override async Task GetFriendsList(FriendListRequest request, IServerStreamWriter<ServerMessage> responseStream, ServerCallContext context)
        {
            var _databaseService = Server.getDatabaseService();
            List<string> friends;
            try
            {
                friends = await _databaseService.GetFriendsUsernamesListFromUsername(request.Client);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                await responseStream.WriteAsync(new ServerMessage { MessageResponse = new MessageResponse { Content = "Error getting friend list" } });
                return;
            }

            string friendsList = string.Join(";", friends);
            await responseStream.WriteAsync(new ServerMessage { MessageResponse = new MessageResponse { Content = friendsList } });
        }

        public override async Task AddFriend(AddFriendRequest request, IServerStreamWriter<ServerMessage> responseStream, ServerCallContext context)
        {
            var _databaseService = Server.getDatabaseService();
            bool success;
            try
            {
                success = await _databaseService.AddFriend(request.Client, request.Friend);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                await responseStream.WriteAsync(new ServerMessage { MessageResponse = new MessageResponse { Content = "Error adding friend" } });
                return;
            }

            if (success)
            {
                await responseStream.WriteAsync(new ServerMessage { MessageResponse = new MessageResponse { Content = "Friend added successfully" } });
            }
            else
            {
                await responseStream.WriteAsync(new ServerMessage { MessageResponse = new MessageResponse { Content = "Failed to add friend" } });
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
