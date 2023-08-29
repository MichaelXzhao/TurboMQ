using System;
using System.IO;
using System.Net.Sockets;
using System.Text;

public class PublishProducer
{
    public static void Main(string[] args)
    {
        while (true)
        {
            try
            {
                Console.Write("Enter the topic to publish to: ");
                string topic = Console.ReadLine();
                Console.Write($"Enter the message to publish to topic '{topic}' (or 'exit' to quit): ");
                string message = Console.ReadLine();

                if (message.ToLower() == "exit")
                {
                    break;
                }

                
                using (TcpClient client = new TcpClient("127.0.0.1", 8888))
                using (NetworkStream stream = client.GetStream())
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                using (StreamWriter writer = new StreamWriter(stream, Encoding.UTF8))
                {
                    writer.WriteLine($"PUBLISH {topic} {message}");
                    writer.Flush();

                    string response = reader.ReadLine();
                    Console.WriteLine($"Server response: {response}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }
    }
}

