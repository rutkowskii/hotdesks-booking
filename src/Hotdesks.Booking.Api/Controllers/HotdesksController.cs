using Hotdesks.Booking.Api.Data;
using Hotdesks.Booking.Api.Contracts;
using Hotdesks.Booking.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Hotdesks.Booking.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class HotdesksController(HotdesksBookingDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Hotdesk>>> GetAll(CancellationToken cancellationToken)
    {
        var hotdesks = await dbContext.Hotdesks
            .AsNoTracking()
            .Where(hotdesk => hotdesk.IsEnabled)
            .OrderBy(hotdesk => hotdesk.Name)
            .ToListAsync(cancellationToken);

        return Ok(hotdesks);
    }

    [HttpPost]
    public async Task<ActionResult<Hotdesk>> Create(
        CreateHotdeskRequest request,
        CancellationToken cancellationToken)
    {
        var hotdesk = new Hotdesk
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            IsAvailable247 = request.IsAvailable247
        };

        dbContext.Hotdesks.Add(hotdesk);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Created($"/api/hotdesks/{hotdesk.Id}", hotdesk);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var hotdesk = await dbContext.Hotdesks.FindAsync([id], cancellationToken);
        if (hotdesk is null)
        {
            return NotFound();
        }

        hotdesk.IsEnabled = false;
        await dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
