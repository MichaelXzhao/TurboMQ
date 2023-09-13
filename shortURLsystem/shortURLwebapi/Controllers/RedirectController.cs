using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using DatabaseNamespace;

[Route("s")]
public class RedirectController : Controller
{
    private readonly UrlDbContext _context;

    public RedirectController(UrlDbContext context)
    {
        _context = context;
    }

    [HttpGet("{suffix}")]
    public async Task<IActionResult> RedirectToOriginal(string suffix)
    {
        // Build the short URL from the provided suffix
        var shortUrl = "http://localhost:5555/s/" + suffix;

        // Look up the long URL from the database
        var mapping = await _context.Urls.FirstOrDefaultAsync(u => u.ShortUrl == shortUrl);
        if (mapping != null)
        {
            return Redirect(mapping.LongUrl);
        }
        else
        {
            return NotFound("Short URL not found.");
        }
    }
}
