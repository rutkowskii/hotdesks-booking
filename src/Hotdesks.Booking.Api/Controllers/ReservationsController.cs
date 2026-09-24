using Hotdesks.Booking.Api.Contracts;
using Hotdesks.Booking.Api.Data;
using Hotdesks.Booking.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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
            .Where(reservation => reservation.IsLastVersion)
            .OrderBy(reservation => reservation.From)
            .ThenBy(reservation => reservation.Id)
            .Select(reservation => new ReservationResponse(
                reservation.Id,
                reservation.VersionId,
                reservation.Version,
                reservation.IsLastVersion,
                reservation.UserId,
                reservation.HotdeskId,
                reservation.From,
                reservation.To,
                reservation.IsActive))
            .ToListAsync(cancellationToken);

        return Ok(reservations);
    }

    [HttpGet("{versionId:guid}/history")]
    public async Task<ActionResult<IReadOnlyList<ReservationResponse>>> GetHistory(
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var reservations = await dbContext.Reservations
            .AsNoTracking()
            .Where(reservation => reservation.VersionId == versionId)
            .OrderBy(reservation => reservation.Version)
            .Select(reservation => new ReservationResponse(
                reservation.Id,
                reservation.VersionId,
                reservation.Version,
                reservation.IsLastVersion,
                reservation.UserId,
                reservation.HotdeskId,
                reservation.From,
                reservation.To,
                reservation.IsActive))
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
            return InvalidTimeRange();
        }

        var userExists = await dbContext.Users
            .AnyAsync(user => user.Id == request.UserId, cancellationToken);
        if (!userExists)
        {
            return NotFound($"User '{request.UserId}' was not found.");
        }

        if (!await IsEnabledHotdeskAsync(request.HotdeskId, cancellationToken))
        {
            return NotFound($"Enabled hotdesk '{request.HotdeskId}' was not found.");
        }

        if (await HasActiveOverlapAsync(
                request.HotdeskId,
                request.From,
                request.To,
                excludedReservationId: null,
                cancellationToken))
        {
            return OverlapConflict();
        }

        try
        {
            var reservation = new Reservation
            {
                Id = Guid.NewGuid(),
                VersionId = Guid.NewGuid(),
                Version = 1,
                IsLastVersion = true,
                UserId = request.UserId,
                HotdeskId = request.HotdeskId,
                From = request.From,
                To = request.To
            };

            dbContext.Reservations.Add(reservation);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Created($"/reservation/{reservation.Id}", ToResponse(reservation));
        }
        catch (DbUpdateException exception) when (IsOverlapViolation(exception))
        {
            return OverlapConflict();
        }
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var currentReservation = await dbContext.Reservations.FindAsync([id], cancellationToken);
        if (currentReservation is null)
        {
            return NotFound();
        }

        if (!currentReservation.IsLastVersion)
        {
            return CurrentVersionConflict();
        }

        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            currentReservation.IsLastVersion = false;
            await dbContext.SaveChangesAsync(cancellationToken);

            dbContext.Reservations.Add(new Reservation
            {
                Id = Guid.NewGuid(),
                VersionId = currentReservation.VersionId,
                Version = currentReservation.Version + 1,
                IsLastVersion = true,
                UserId = currentReservation.UserId,
                HotdeskId = currentReservation.HotdeskId,
                From = currentReservation.From,
                To = currentReservation.To,
                IsActive = false
            });
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return NoContent();
        }
        catch (DbUpdateConcurrencyException)
        {
            return CurrentVersionConflict();
        }
    }

    [HttpPost("{id:guid}/edit")]
    public async Task<ActionResult<ReservationResponse>> Edit(
        Guid id,
        EditReservationRequest request,
        CancellationToken cancellationToken)
    {
        if (request.From >= request.To)
        {
            return InvalidTimeRange();
        }

        var currentReservation = await dbContext.Reservations.FindAsync([id], cancellationToken);
        if (currentReservation is null)
        {
            return NotFound();
        }

        if (!currentReservation.IsLastVersion)
        {
            return CurrentVersionConflict();
        }

        if (!await IsEnabledHotdeskAsync(request.HotdeskId, cancellationToken))
        {
            return NotFound($"Enabled hotdesk '{request.HotdeskId}' was not found.");
        }

        if (currentReservation.IsActive
            && await HasActiveOverlapAsync(
                request.HotdeskId,
                request.From,
                request.To,
                currentReservation.Id,
                cancellationToken))
        {
            return OverlapConflict();
        }

        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            currentReservation.IsLastVersion = false;
            await dbContext.SaveChangesAsync(cancellationToken);

            var nextReservation = new Reservation
            {
                Id = Guid.NewGuid(),
                VersionId = currentReservation.VersionId,
                Version = currentReservation.Version + 1,
                IsLastVersion = true,
                UserId = currentReservation.UserId,
                HotdeskId = request.HotdeskId,
                From = request.From,
                To = request.To,
                IsActive = currentReservation.IsActive
            };
            dbContext.Reservations.Add(nextReservation);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Created($"/reservation/{nextReservation.Id}", ToResponse(nextReservation));
        }
        catch (DbUpdateConcurrencyException)
        {
            return CurrentVersionConflict();
        }
        catch (DbUpdateException exception) when (IsOverlapViolation(exception))
        {
            return OverlapConflict();
        }
    }

    private async Task<bool> IsEnabledHotdeskAsync(Guid hotdeskId, CancellationToken cancellationToken) =>
        await dbContext.Hotdesks.AnyAsync(
            hotdesk => hotdesk.Id == hotdeskId && hotdesk.IsEnabled,
            cancellationToken);

    private async Task<bool> HasActiveOverlapAsync(
        Guid hotdeskId,
        DateTimeOffset from,
        DateTimeOffset to,
        Guid? excludedReservationId,
        CancellationToken cancellationToken) =>
        await dbContext.Reservations.AnyAsync(
            reservation => reservation.IsActive
                && reservation.IsLastVersion
                && reservation.HotdeskId == hotdeskId
                && (!excludedReservationId.HasValue || reservation.Id != excludedReservationId.Value)
                && reservation.From < to
                && from < reservation.To,
            cancellationToken);

    private ActionResult InvalidTimeRange()
    {
        ModelState.AddModelError("To", "The reservation end must be after its start.");
        return ValidationProblem(ModelState);
    }

    private ActionResult OverlapConflict() =>
        Conflict(new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "The hotdesk is already reserved for the requested time range."
        });

    private ActionResult CurrentVersionConflict() =>
        Conflict(new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "The reservation version is no longer current."
        });

    private static ReservationResponse ToResponse(Reservation reservation) =>
        new(
            reservation.Id,
            reservation.VersionId,
            reservation.Version,
            reservation.IsLastVersion,
            reservation.UserId,
            reservation.HotdeskId,
            reservation.From,
            reservation.To,
            reservation.IsActive);

    private static bool IsOverlapViolation(DbUpdateException exception) =>
        FindPostgresException(exception) is
        {
            SqlState: PostgresErrorCodes.ExclusionViolation,
            ConstraintName: "EX_Reservations_HotdeskId_TimeRange_NoOverlap"
        };

    private static PostgresException? FindPostgresException(Exception exception)
    {
        for (var currentException = exception; currentException is not null; currentException = currentException.InnerException)
        {
            if (currentException is PostgresException postgresException)
            {
                return postgresException;
            }
        }

        return null;
    }
}
