using Microsoft.AspNetCore.Mvc;
using RssOverEmail.Dto;
using RssOverEmail.Services;

namespace RssOverEmail.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ItemsController(IItemRepository itemRepository) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? feedId,
        [FromQuery] bool? read,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 50;
        if (pageSize > 200) pageSize = 200;

        var result = await itemRepository.GetItemsAsync(feedId, read, page, pageSize, ct);
        return Ok(result);
    }

    [HttpPatch("{id:int}/read")]
    public async Task<IActionResult> MarkRead(
        int id,
        [FromBody] MarkReadRequest request,
        CancellationToken ct = default)
    {
        var item = await itemRepository.GetByIdAsync(id, ct);
        if (item is null)
            return NotFound();

        await itemRepository.MarkReadAsync(id, request.IsRead, ct);
        return NoContent();
    }
}
