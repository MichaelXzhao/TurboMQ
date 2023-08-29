using Microsoft.AspNetCore.SignalR;

public class QueueMonitorHub : Hub
{
    public async Task SendQueueUpdate(string message)
    {
        await Clients.All.SendAsync("ReceiveQueueUpdate", message);
    }
}
