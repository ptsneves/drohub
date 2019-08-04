
using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using DroHub.Areas.DHub.Models;

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
        private LogEntry _last_log_entry;
        public NotificationsHubPoller(IServiceProvider services, ILogger<NotificationsHubPoller> logger,
            IHubContext<NotificationsHub> hub) {
            _logger = logger;
            _hub = hub;
            _services = services;
            using (var scope = _services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<DroHubContext>();
                _last_log_entry = context.Logs.OrderByDescending(l => l.Id).First();
            }
        }

        private async Task DoTask() {
            using (var scope = _services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<DroHubContext>();
                var notifications = context.Logs.OrderByDescending(l => l.Id).Where(l => l.Id > _last_log_entry.Id).ToArray();
                if (notifications.Length != 0)
                {
                    _last_log_entry = notifications.Last();
                    await _hub.Clients.All.SendAsync("notification", notifications);
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