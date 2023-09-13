using Microsoft.EntityFrameworkCore;


namespace DatabaseNamespace
{
    public class UrlDbContext : DbContext
    {
        public UrlDbContext(DbContextOptions<UrlDbContext> options) : base(options)
        {
        }
        public DbSet<UrlMapping> Urls { get; set; }
    }

    public class UrlMapping 
    {
        public int Id { get; set; }
        public string LongUrl { get; set; }
        public string ShortUrl { get; set; }
    }
}