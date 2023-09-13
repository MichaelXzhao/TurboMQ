using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Security.Cryptography;
using System.Threading;
using Microsoft.EntityFrameworkCore;

public class MessageQueueConsumer
{
    private const string ShortUrlBase = "http://localhost:5555/s/";
    private static readonly string ConnectionString = "Server=localhost,1433;Database=assignment;User Id=sa;Password=xxxxxxxx;TrustServerCertificate=True";  

    public static void Main(string[] args)
    {
        using (var context = new UrlDbContext())
        {
            context.Database.EnsureCreated();
        }

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
                }

                if (response != "NO MESSAGE")
                {
                    string messageId = response.Split(' ')[1];
                    var longUrl = response.Split(' ')[8];
                    var shortUrl = GenerateShortUrl(longUrl);
                    Console.WriteLine($"Generated short URL: {shortUrl} for {longUrl}");

                    using (var context = new UrlDbContext())
                    {
                        var mapping = new UrlMapping { LongUrl = longUrl, ShortUrl = shortUrl };
                        context.Urls.Add(mapping);
                        context.SaveChanges();
                    }

                    using (TcpClient ackClient = new TcpClient("127.0.0.1", 8888))
                    using (NetworkStream ackStream = ackClient.GetStream())
                    using (StreamWriter ackWriter = new StreamWriter(ackStream, Encoding.UTF8))
                    {
                        ackWriter.WriteLine($"ACK {messageId}");
                        ackWriter.Flush();
                        Console.WriteLine($"Send ACK for {messageId} back to the server");
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

    private static string GenerateShortUrl(string url)
    {
        using (MD5 md5 = MD5.Create())
        {
            byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(url));
            string suffix = Convert.ToBase64String(hash, 0, 6);
            return ShortUrlBase + suffix;
        }
    }

    public class UrlMapping
    {
        public int Id { get; set; }
        public string LongUrl { get; set; }
        public string ShortUrl { get; set; }
    }

    public class UrlDbContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(ConnectionString);
        }

        public DbSet<UrlMapping> Urls { get; set; }
    }
}


