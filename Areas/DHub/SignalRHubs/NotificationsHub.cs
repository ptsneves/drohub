
using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using DroHub.Areas.DHub.Models;
using Newtonsoft.Json;

using DroHub.Data;
namespace DroHub.Areas.DHub.SignalRHubs
{

    public class NotificationsHub : Hub
    {
        private readonly DroHubContext _context;
        private readonly ILogger<NotificationsHub> _logger;
        public NotificationsHub(DroHubContext context, ILogger<NotificationsHub> logger) {
            _context = context;
            _logger = logger;
        }
    }
    public class NotificationsHubPoller : BackgroundService {

        private readonly ILogger<NotificationsHubPoller> _logger;
        private readonly IHubContext<NotificationsHub> _hub;

        private readonly IServiceProvider _services;
        private int _last_log_entry_id;

        public NotificationsHubPoller(IServiceProvider services, ILogger<NotificationsHubPoller> logger,
            IHubContext<NotificationsHub> hub) {
            _logger = logger;
            _hub = hub;
            _services = services;
            using (var scope = _services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<DroHubContext>();
                var result = context.Logs.OrderByDescending(l => l.Id).FirstOrDefault();
                if (result == null)
                    _last_log_entry_id = -1;
                else
                    _last_log_entry_id = result.Id;

            }
        }

        private async Task DoTask() {
            using (var scope = _services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<DroHubContext>();

                var notifications = context.Logs
                    .Where(l => l.Id > _last_log_entry_id)
                    .OrderByDescending(l => l.Id)
                    .ToArray();

                if (notifications.Length != 0)
                {
                    _last_log_entry_id = notifications.First().Id;
                    await _hub.Clients.All.SendAsync("notification", JsonConvert.SerializeObject(notifications) );
                }
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            stoppingToken.Register(() =>
                    _logger.LogInformation($"Background task is stopping."));

            while (!stoppingToken.IsCancellationRequested)
            {

                await DoTask();

                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }

        }
    }
}