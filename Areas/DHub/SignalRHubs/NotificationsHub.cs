
using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading.Tasks;

namespace DroHub.Areas.DHub.SignalRHubs
{
    public class NotificationsHub : Hub
    {
        public Task SendMessage(string user, string message)
        {
            return Clients.All.SendAsync("ReceiveMessage", user, message);
        }
    }
}