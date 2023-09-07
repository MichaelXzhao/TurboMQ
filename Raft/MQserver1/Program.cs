using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public enum NodeState
{
    Follower,
    Candidate,
    Leader
}

public class MessageQueueServer
{
    private readonly ConcurrentQueue<QueuedMessage> _messageQueue = new ConcurrentQueue<QueuedMessage>();
    private readonly ConcurrentDictionary<string, List<SubscriberInfo>> _subscriptions = new ConcurrentDictionary<string, List<SubscriberInfo>>();
    private readonly ConcurrentDictionary<string, UnconfirmedMessage> _unconfirmedMessages = new ConcurrentDictionary<string, UnconfirmedMessage>();
    private readonly TcpListener _listener;
    private readonly DateTime _startTime = DateTime.Now; 
    private int _totalMessagesProcessed = 0; 
    private const string MessageStorePath = "messages.txt";
    public string Id { get; } = Guid.NewGuid().ToString();
    public string Address { get; private set; }
    public NodeState State { get; private set; } = NodeState.Follower;
    private int _votesReceived = 0;
    private List<string> _nodes;
    private CancellationTokenSource _electionTimeoutCts = new CancellationTokenSource();

    public MessageQueueServer(int port, List<string> nodes)
    {
        _listener = new TcpListener(IPAddress.Any, port);
        _nodes = nodes;
        Address = $"localhost:{((IPEndPoint)_listener.LocalEndpoint).Port}";
        LoadMessages();
        StartElectionTimer();
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

    private void StartElectionTimer()
    {
        _electionTimeoutCts = new CancellationTokenSource();

        Task.Run(async () =>
        {
            try
            {
            var timeout = new Random().Next(1500, 3000);
            await Task.Delay(timeout, _electionTimeoutCts.Token);

            if (State == NodeState.Follower)
            {
                StartElection();
            }
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine($"{Address} : Receiving a heartbeat.");
            }
        });
    }

    private async Task<string> SendCommand(string command, string targetAddress)
    {
        try
        {
            var parts = targetAddress.Split(':');
            var host = parts[0];
            var port = int.Parse(parts[1]);

            using (TcpClient client = new TcpClient(host, port))
            using (NetworkStream stream = client.GetStream())
            using (StreamWriter writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true))
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                await writer.WriteLineAsync(command);
                await writer.FlushAsync();
                string response = await reader.ReadLineAsync();

                return response;
            }
        }
        catch (Exception e)
        {
            return $"An error occurred: {e.Message}";
        }
    }

    // 在選舉的部分
    private void StartElection()
    {
        State = NodeState.Candidate;
        _votesReceived = 1; // vote for self
        Console.WriteLine($"{Address} started an election.");

        foreach (var node in _nodes)
        {
            Task.Run(async () =>
            {
                string response = await SendCommand($"VOTE_FOR_ME {Address}", node);
                if (response == "VOTE_GRANTED")
                {
                    Interlocked.Increment(ref _votesReceived);
                }

                if (_votesReceived > (_nodes.Count + 1) / 2 && State == NodeState.Candidate)
                {
                    BecomeLeader();
                }
            });
        }
    }

    public async Task<bool> RequestVote(string requestingNodeAddress)
    {
        if (State == NodeState.Follower)
        {
            Console.WriteLine($"{Address} voted for {requestingNodeAddress}");
            await SendCommand($"VOTED {Address}", requestingNodeAddress);
            return true;
        }
        return false;
    }


    private void BecomeLeader()
    {
        State = NodeState.Leader;
        Console.WriteLine($"{Address} became the leader.");
        // Send heartbeats
        Task.Run(async () => 
        {
            while (State == NodeState.Leader)
            {
                foreach(var node in _nodes)
                {
                    await SendCommand($"HEARTBEAT {Address}", node);
                }
                await Task.Delay(1000);
            }
        });
    }

    public void ReceiveHeartbeat()
    {
        if (State != NodeState.Leader)
        {
            State = NodeState.Follower;
            _electionTimeoutCts.Cancel();
            StartElectionTimer();
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
        Console.WriteLine($"Server started listening on localhost:{((IPEndPoint)_listener.LocalEndpoint).Port}");

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
                    _messageQueue.Enqueue(new QueuedMessage
                    {
                        MessageId = kvp.Value.Id,
                        MessageContent = kvp.Value.Message
                    });
                    kvp.Value.Timestamp = now;
                    Console.WriteLine($"Resending and re-enqueued message with ID: {kvp.Value.Id} , Message:{kvp.Value.Message}");
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
        List<QueuedMessage> dequeuedMessages = new List<QueuedMessage>(); 

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
            while (_messageQueue.TryDequeue(out QueuedMessage queuedMessage))
            {
                messages.AppendLine($"ID: {queuedMessage.MessageId}, Message: {queuedMessage.MessageContent}");
                dequeuedMessages.Add(queuedMessage); 
            }
            
            if (messages.Length > 0)
            {
                var firstMessageId = dequeuedMessages[0].MessageId;  
                File.WriteAllText(MessageStorePath, ""); 
                response = $"{firstMessageId} OK > Messages received from server: {messages}";
                Console.WriteLine($"Messages dequeued: {messages}");
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
        // Handle voting requests
        else if (command.StartsWith("VOTE_FOR_ME "))
        {
            string candidateIp = command.Substring(11).Trim();  
            if (_nodes.Contains(candidateIp))  
            {
                if (await this.RequestVote(candidateIp)) 
                {
                    response = "VOTE_GRANTED";
                }
                else
                {
                    response = "VOTE_NOT_GRANTED";
                }
            }
            else
            {
                response = "CANDIDATE_NOT_FOUND";  
            }
        }
        else if (command.StartsWith("VOTED "))
        {
            string followerIp = command.Substring(6).Trim();
            Console.WriteLine($"{followerIp} voted");
        }
        else if (command.StartsWith("HEARTBEAT"))
        {
            string leaderIp = command.Substring(9).Trim(); 
            if (_nodes.Contains(leaderIp)) 
            {
                this.ReceiveHeartbeat();  
                response = "HEARTBEAT_RECEIVED";
            }
            else
            {
                response = "LEADER_NOT_FOUND"; 
            }
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
            response = $"Performance Metrics: {_subscriptions.Count} subscribers, {_messageQueue.Count} messages in queue";
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
        List<string> nodes = new List<string>
        {
            "localhost:6666",
            "localhost:7777"
        };
        MessageQueueServer server = new MessageQueueServer(8888, nodes);
        
        server.StartAsync().GetAwaiter().GetResult();
        
    }
}
