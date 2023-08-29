using System;
using System.IO;
using System.Net.Sockets;
using System.Text;

public class MessageQueueClient
{
    private readonly string _host;
    private readonly int _port;

    public MessageQueueClient(string host, int port)
    {
        _host = host;
        _port = port;
    }

    public void Start()
    {
        while (true)
        {
            Console.WriteLine("Choose an option:");
            Console.WriteLine("1. View Queue Status");
            Console.WriteLine("2. View Message Rate");
            Console.WriteLine("3. Monitor Performance");
            Console.WriteLine("4. View Subscribers List");
            Console.WriteLine("5. Exit");

            int choice = int.Parse(Console.ReadLine());

            switch (choice)
            {
                case 1:
                    SendCommand("VIEW_QUEUE_STATUS");
                    break;
                case 2:
                    SendCommand("VIEW_MESSAGE_RATE");
                    break;
                case 3:
                    SendCommand("MONITOR_PERFORMANCE");
                    break;
                case 4:
                    SendCommand("VIEW_SUBSCRIBERS");
                    break;
                case 5:
                    return;
                default:
                    Console.WriteLine("Invalid choice");
                    break;
            }
        }
    }

    private void SendCommand(string command)
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
                Console.WriteLine($"Server response: {response}");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"An error occurred: {e.Message}");
        }
    }

    public static void Main(string[] args)
    {
        MessageQueueClient client = new MessageQueueClient("localhost", 8888);
        client.Start();
    }
}

