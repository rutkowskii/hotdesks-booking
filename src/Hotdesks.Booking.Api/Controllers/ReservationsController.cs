using Hotdesks.Booking.Api.Contracts;
using Hotdesks.Booking.Api.Data;
using Hotdesks.Booking.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Hotdesks.Booking.Api.Controllers;

[ApiController]
[Route("reservation")]
public sealed class ReservationsController(HotdesksBookingDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ReservationResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var reservations = await dbContext.Reservations
            .AsNoTracking()
            .OrderBy(reservation => reservation.From)
            .ThenBy(reservation => reservation.Id)
            .Select(reservation => new ReservationResponse(
                reservation.Id,
                reservation.UserId,
                reservation.HotdeskId,
                reservation.From,
                reservation.To))
            .ToListAsync(cancellationToken);

        return Ok(reservations);
    }

    [HttpPost("add")]
    public async Task<ActionResult<ReservationResponse>> Add(
        AddReservationRequest request,
        CancellationToken cancellationToken)
    {
        if (request.From >= request.To)
        {
            ModelState.AddModelError(
                nameof(request.To),
                "The reservation end must be after its start.");
            return ValidationProblem(ModelState);
        }

        var userExists = await dbContext.Users
            .AnyAsync(user => user.Id == request.UserId, cancellationToken);
        if (!userExists)
        {
            return NotFound($"User '{request.UserId}' was not found.");
        }

        var hotdeskIsAvailable = await dbContext.Hotdesks
            .AnyAsync(
                hotdesk => hotdesk.Id == request.HotdeskId && hotdesk.IsEnabled,
                cancellationToken);
        if (!hotdeskIsAvailable)
        {
            return NotFound($"Enabled hotdesk '{request.HotdeskId}' was not found.");
        }

        var reservation = new Reservation
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            HotdeskId = request.HotdeskId,
            From = request.From,
            To = request.To
        };

        dbContext.Reservations.Add(reservation);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Created(
            $"/reservation/{reservation.Id}",
            new ReservationResponse(
                reservation.Id,
                reservation.UserId,
                reservation.HotdeskId,
                reservation.From,
                reservation.To));
    }
}
