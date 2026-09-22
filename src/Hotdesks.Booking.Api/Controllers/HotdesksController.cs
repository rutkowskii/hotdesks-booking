using Hotdesks.Booking.Api.Data;
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
            .OrderBy(hotdesk => hotdesk.Name)
            .ToListAsync(cancellationToken);

        return Ok(hotdesks);
    }
}
