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
        internal static ConcurrentDictionary<string, string> clientIDs = new ConcurrentDictionary<string, string>();

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
    }

    public class PingServiceImpl : PingService.PingServiceBase
    {
        public override Task<MessageResponse> SendMessage(MessageRequest request, ServerCallContext context)
        {
            var clientId = Guid.NewGuid().ToString();
            Server.clientIDs[request.UserId] = clientId;
            Console.WriteLine($"Client connected: {request.UserId}");
            Console.WriteLine($"Received message from {request.UserId} to {request.RecipientId}: {request.Message}");
            return Task.FromResult(new MessageResponse { Status = "Message sent" });
        }
    
        public override Task<KeyExchangeResponse> ProposeKeyExchange(KeyExchangeRequest request, ServerCallContext context)
        {
            var clientId = Guid.NewGuid().ToString();
            Server.clientIDs[request.UserId] = clientId;
            Console.WriteLine($"Client connected: {request.UserId}");
            Console.WriteLine($"Key exchange proposed from {request.UserId} to {request.RecipientId}");
            return Task.FromResult(new KeyExchangeResponse { Status = "Key exchange proposed" });
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
    
            app.Use(async (context, next) =>
            {
                Console.WriteLine($"Client connected: {context.Connection.RemoteIpAddress}");
                await next.Invoke();
            });
    
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGrpcService<PingServiceImpl>();
            });
        }
    }
}