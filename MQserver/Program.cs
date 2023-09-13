using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

public class MessageQueueServer
{
    private readonly ConcurrentQueue<QueuedMessage> _messageQueue = new ConcurrentQueue<QueuedMessage>();
    private readonly ConcurrentDictionary<string, List<SubscriberInfo>> _subscriptions = new ConcurrentDictionary<string, List<SubscriberInfo>>();
    private readonly ConcurrentDictionary<string, UnconfirmedMessage> _unconfirmedMessages = new ConcurrentDictionary<string, UnconfirmedMessage>();
    private readonly TcpListener _listener;
    private readonly DateTime _startTime = DateTime.Now; 
    private int _totalMessagesProcessed = 0; 
    private const string MessageStorePath = "messages.txt";

    public MessageQueueServer(int port)
    {
        _listener = new TcpListener(IPAddress.Any, port);
        LoadMessages();
    }

    private void LoadMessages()
    {
        if (File.Exists(MessageStorePath))
        {
            var lines = File.ReadAllLines(MessageStorePath);
            foreach (var line in lines)
            {
                var parts = line.Split(new[] { " : " }, StringSplitOptions.None);
                if (parts.Length == 2) 
                {
                    var messageId = parts[0];
                    var messageContent = parts[1];
                    _messageQueue.Enqueue(new QueuedMessage
                    {
                        MessageId = messageId,
                        MessageContent = messageContent
                    });
                }
            }
        }
    }


    class QueuedMessage
    {
        public string MessageId { get; set; }
        public string MessageContent { get; set; }
    }


    class SubscriberInfo
    {
        public TcpClient Client { get; set; }
        public ConcurrentQueue<string> Messages { get; } = new ConcurrentQueue<string>();
    }

    class UnconfirmedMessage
    {
        public string Id { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }
    }


    public async Task StartAsync()
    {
        _listener.Start();
        Console.WriteLine($"Server started listening on {_listener.LocalEndpoint}");

        var monitorTask = MonitorUnconfirmedMessagesAsync(TimeSpan.FromMinutes(0.1));

        while (true)
        {
            TcpClient client = await _listener.AcceptTcpClientAsync();
            _ = HandleClientAsync(client);
        }
    }

    private async Task MonitorUnconfirmedMessagesAsync(TimeSpan timeout)
    {
        while (true)
        {
            var now = DateTime.Now;
            foreach (var kvp in _unconfirmedMessages)
            {
                if (now - kvp.Value.Timestamp > timeout)
                {                   
                    kvp.Value.Timestamp = now;
                    Console.WriteLine($"Resending message with ID: {kvp.Value.Id} , Message:{kvp.Value.Message}");
                }
            }
            await Task.Delay(timeout);
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        NetworkStream stream = client.GetStream();
        StreamReader reader = new StreamReader(stream, Encoding.UTF8);
        StreamWriter writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true);

        string command = await reader.ReadLineAsync();
        string response = "";

        StringBuilder messages = new StringBuilder();

        if (command.StartsWith("ENQUEUE"))
        {
            string message = command.Substring(8);
            string messageId = Guid.NewGuid().ToString();
            _messageQueue.Enqueue(new QueuedMessage
            {
                MessageId = messageId,
                MessageContent = message
            });
            File.AppendAllText(MessageStorePath, messageId + " : " + message + Environment.NewLine); 
            _unconfirmedMessages.TryAdd(messageId, new UnconfirmedMessage
            {
                Id = messageId,
                Message = message,
                Timestamp = DateTime.Now
            });
            response = $"OK > Message sent: {message}";
            Console.WriteLine($"Message enqueued: {message}");
        }
        else if (command.StartsWith("DEQUEUE"))
        {
            if (_messageQueue.TryDequeue(out QueuedMessage queuedMessage))
            {
                messages.AppendLine($"ID: {queuedMessage.MessageId}, Message: {queuedMessage.MessageContent}");
                RemoveMessageFromFile(queuedMessage.MessageId);
                response = $"ID: {queuedMessage.MessageId} OK > Message received from server: {queuedMessage.MessageContent}";
                Console.WriteLine($"Message dequeued: {queuedMessage.MessageContent}");
                _totalMessagesProcessed++;
            }
            else
            {
                response = "NO MESSAGE";
                Console.WriteLine("Tried to dequeue messages, but the queue is empty.");
            }
        }
        else if (command.StartsWith("ACK"))
        {
            string messageId = command.Substring(4);
            _unconfirmedMessages.TryRemove(messageId, out _);
            Console.WriteLine($"ACK received for message ID: {messageId}");
        }

        else if (command.StartsWith("SUBSCRIBE"))
        {
            string topic = command.Substring(10);
            var subscriberInfo = new SubscriberInfo { Client = client };
            _subscriptions.GetOrAdd(topic, new List<SubscriberInfo>()).Add(subscriberInfo);
            response = $"OK > Subscribed to {topic}";
            Console.WriteLine($"Client subscription to Topic:{topic} added");
        }
        else if (command.StartsWith("PUBLISH"))
        {
            string topic = command.Split(' ')[1];
            string message = command.Substring(9 + topic.Length);

            if (_subscriptions.TryGetValue(topic, out var subscriberInfos))
            {
                foreach (var subscriberInfo in subscriberInfos)
                {
                    subscriberInfo.Messages.Enqueue($"BROADCAST > Topic: {topic}, Message: {message}");
                    try
                    {
                        if (subscriberInfo.Client.Connected)
                        {
                            using (StreamWriter sw = new StreamWriter(subscriberInfo.Client.GetStream(), Encoding.UTF8, leaveOpen: true))
                            {
                                if (subscriberInfo.Messages.TryDequeue(out var msgToSend))
                                {
                                    await sw.WriteLineAsync(msgToSend);
                                    await sw.FlushAsync();
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Failed to send message to subscriber: {e.Message}");
                    }
                }
            }
            response = $"OK > Published to {topic}";
            Console.WriteLine($"Successfully published to {topic}");
        }
        else if (command.StartsWith("VIEW_QUEUE_STATUS"))
        {
            response = $"Queue status: {_messageQueue.Count} messages";
            Console.WriteLine(response);
        }
        else if (command.StartsWith("VIEW_SUBSCRIBERS"))
        {
            StringBuilder sb = new StringBuilder();
            foreach (var topic in _subscriptions.Keys)
            {
                sb.AppendLine($"Topic: {topic} - Subscribers: {_subscriptions[topic].Count}");
            }
            response = sb.ToString();
            Console.WriteLine($"Subscribers list:\n{response}");
        }
        else if (command.StartsWith("VIEW_MESSAGE_RATE"))
        {
            double elapsedTimeInSeconds = (DateTime.Now - _startTime).TotalSeconds;
            double messageRate = _totalMessagesProcessed / elapsedTimeInSeconds;
            response = $"Message rate: {messageRate} messages per second";
            Console.WriteLine(response);
        }
        else if (command.StartsWith("MONITOR_PERFORMANCE"))
        {
            response = $"Performance Metrics: {_subscriptions.Count} subscriptions, {_messageQueue.Count} messages in queue";
            Console.WriteLine(response);
        }
        else
        {
            response = "UNKNOWN COMMAND";
            Console.WriteLine("Unknown command");
        }

        await writer.WriteLineAsync(response);
        await writer.FlushAsync();

        
        if (!IsSubscriber(client))
        {
            reader.Dispose();
            writer.Dispose();
            client.Close();
            //Console.WriteLine("Client connection closed");
        }
    }

    private void RemoveMessageFromFile(string messageId)
    {
        var lines = File.ReadAllLines(MessageStorePath).ToList();
        lines.RemoveAll(line => line.StartsWith(messageId));
        File.WriteAllLines(MessageStorePath, lines);
    }

    private bool IsSubscriber(TcpClient client)
    {
        foreach (var subscribers in _subscriptions.Values)
        {
            if (subscribers.Any(subscriberInfo => subscriberInfo.Client == client))
                return true;
        }
        return false;
    }


    public static void Main(string[] args)
    {
        MessageQueueServer server = new MessageQueueServer(8888);
        server.StartAsync().GetAwaiter().GetResult();
    }
}
