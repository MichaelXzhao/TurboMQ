using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MessageQueueMonitor.Controllers
{
    [ApiController]
    [Route("MessageQueueMonitor")]
    public class MonitoringController : ControllerBase
    {
        private readonly string _host = "localhost";
        private readonly int _port = 8888;
        private readonly IHubContext<QueueMonitorHub> _hubContext;

        public MonitoringController(IHubContext<QueueMonitorHub> hubContext)
        {
            _hubContext = hubContext;
        }

        [HttpGet("status")]
        public async Task<ActionResult<string>> GetQueueStatus()
        {
            return await SendCommand("VIEW_QUEUE_STATUS");
        }

        [HttpGet("rate")]
        public async Task<ActionResult<string>> GetMessageRate()
        {
            return await SendCommand("VIEW_MESSAGE_RATE");
        }

        [HttpGet("performance")]
        public async Task<ActionResult<string>> GetPerformance()
        {
            return await SendCommand("MONITOR_PERFORMANCE");
        }

        [HttpGet("subscribers")]
        public async Task<ActionResult<string>> GetSubscribers()
        {
            return await SendCommand("VIEW_SUBSCRIBERS");
        }

        private async Task<string> SendCommand(string command)
        {
            try
            {
                using (TcpClient client = new TcpClient(_host, _port))
                using (NetworkStream stream = client.GetStream())
                using (StreamWriter writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true))
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                {
                    writer.WriteLine(command);
                    writer.Flush();
                    string response = reader.ReadLine();

                
                    await _hubContext.Clients.All.SendAsync("ReceiveQueueUpdate", response);

                    return response;
                }
            }
            catch (Exception e)
            {
                return $"An error occurred: {e.Message}";
            }
        }
    }
}
