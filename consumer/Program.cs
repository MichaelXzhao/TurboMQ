using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

public class MessageQueueConsumer
{
    public static void Main(string[] args)
    {
        while (true)
        {
            try
            {
                string response = "";
                using (TcpClient client = new TcpClient("127.0.0.1", 8888))
                using (NetworkStream stream = client.GetStream())
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                using (StreamWriter writer = new StreamWriter(stream, Encoding.UTF8))
                {
                    writer.WriteLine("DEQUEUE");
                    writer.Flush();

                    response = reader.ReadLine();
                } // This client connection is disposed here, closing the connection.

                if (response != "NO MESSAGE")
                {
                    Console.WriteLine($"Received message from server: {response}");
                    
                    string messageId = response.Split(' ')[0];

                    // Using a separate connection for ACK
                    using (TcpClient ackClient = new TcpClient("127.0.0.1", 8888))
                    using (NetworkStream ackStream = ackClient.GetStream())
                    using (StreamWriter ackWriter = new StreamWriter(ackStream, Encoding.UTF8))
                    {
                        // Send an ACK back to the server with the received message ID.
                        ackWriter.WriteLine($"ACK {messageId}");
                        ackWriter.Flush();
                        Console.WriteLine($"Send ACK {messageId} back to the server");
                    }
                }
                else
                {
                    Console.WriteLine("No messages available in the queue.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }

            Thread.Sleep(1000);
        }
    }

}

