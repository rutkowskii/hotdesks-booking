using Hotdesks.Booking.Api.Contracts;
using Hotdesks.Booking.Api.Data;
using Hotdesks.Booking.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace Hotdesks.Booking.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class UsersController(HotdesksBookingDbContext dbContext) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<User>> Create(
        CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = request.Name
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Created($"/api/users/{user.Id}", user);
    }
}
