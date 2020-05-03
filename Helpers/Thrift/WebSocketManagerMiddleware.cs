using System;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using DroHub.Areas.DHub.API;
using DroHub.Areas.DHub.Helpers.ResourceAuthorizationHandlers;
using DroHub.Areas.DHub.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
namespace DroHub.Helpers.Thrift
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class WebSocketManagerMiddleware
    {

        public WebSocketManagerMiddleware(RequestDelegate next) {
            ;
        }

        public async Task Invoke(HttpContext context, ThriftMessageHandler handler, IThriftTasks tasks,
            DeviceAPI device_api) {
            var serial_number = context.User.Claims.
                Single(c => c.Type == DeviceAuthorizationHandler.TELEMETRY_SERIAL_NUMBER_CLAIM).Value;

            var res = await device_api.authorizeDeviceFlightActions(new DeviceAPI.DeviceSerial(serial_number));
            if (!res) {
                context.Response.StatusCode = 403;
                return;
            }

            await handler.runHandler(context, tasks);
        }
    }
    public static class RequestWebSocketMiddlewareExtensions
    {
        public static IEndpointConventionBuilder MapWebSocketManager(this IEndpointRouteBuilder endpoints,
                                                            PathString path) {
            var websocket_options = new WebSocketOptions()
            {
                KeepAliveInterval = TimeSpan.FromSeconds(20),
                ReceiveBufferSize = 1024
            };
            var pipeline = endpoints.CreateApplicationBuilder()

                .UseAuthentication()
                .UseAuthorization()
                .UseWebSockets(websocket_options)
                .UseMiddleware<WebSocketManagerMiddleware>()
                .Build();
            return endpoints.Map(path, pipeline).WithDisplayName("Thrift");
        }

        public static IServiceCollection AddWebSocketManager(this IServiceCollection services)
        {
            services.AddHttpContextAccessor();
            services.AddScoped<IThriftTasks, DroneMicroServiceManager>();
            services.AddSingleton<ConnectionManager>();
            services.AddScoped<ThriftMessageHandler>();
            return services;
        }
    }
}