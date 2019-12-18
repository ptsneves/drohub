using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
namespace DroHub.Helpers.Thrift
{
    public class WebSocketManagerMiddleware
    {
        private readonly RequestDelegate _next;

        public WebSocketManagerMiddleware(RequestDelegate next)
        {
            _next = next;
        }
        public async Task Invoke(HttpContext context, ThriftMessageHandler handler, IThriftTasks tasks)
        {
            await handler.runHandler(context, tasks);
        }
    }
    public static class RequestWebSocketMiddlewareExtensions
    {
        public static IApplicationBuilder MapWebSocketManager(this IApplicationBuilder app,
                                                            PathString path)
        {
            var serviceScopeFactory = app.ApplicationServices.GetRequiredService<IServiceScopeFactory>();
            var serviceProvider = serviceScopeFactory.CreateScope().ServiceProvider;

            var websocket_options = new WebSocketOptions()
            {
                KeepAliveInterval = TimeSpan.FromSeconds(20),
                ReceiveBufferSize = 1024
            };
            app.UseWebSockets(websocket_options);
            return app.Map(path, (_app) => _app.UseMiddleware<WebSocketManagerMiddleware>());
        }

        public static IServiceCollection AddWebSocketManager(this IServiceCollection services)
        {
            // foreach (var type in Assembly.GetEntryAssembly().ExportedTypes)
            // {
            //     if (type.GetTypeInfo().BaseType == typeof(IThriftTasks))
            //     {
            //         services.AddScoped(type);
            //     }
            // }
            services.AddScoped<IThriftTasks, DroneMicroServiceManager>();
            services.AddSingleton<ConnectionManager>();
            services.AddHttpContextAccessor();
            services.AddScoped<ThriftMessageHandler>();
            return services;
        }
    }
}