using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public class SubscribeConsumer
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("Enter the topic you want to subscribe to:");
        string topic = Console.ReadLine();

        try
        {
            using (TcpClient client = new TcpClient("127.0.0.1", 8888))
            using (NetworkStream stream = client.GetStream())
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            using (StreamWriter writer = new StreamWriter(stream, Encoding.UTF8))
            {
                writer.WriteLine($"SUBSCRIBE {topic}");
                writer.Flush();

                string response = reader.ReadLine();
                Console.WriteLine(response);

                while (true)
                {
                    string broadcastMessage = await reader.ReadLineAsync();
                    Console.WriteLine(broadcastMessage);

                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }
}
