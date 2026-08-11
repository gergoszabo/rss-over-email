using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RssOverEmail.Data;

namespace RssOverEmail.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FeedsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var feeds = await db.Feeds
            .Select(f => new { f.Id, f.Name, f.Url })
            .ToListAsync(ct);

        return Ok(feeds);
    }
}
