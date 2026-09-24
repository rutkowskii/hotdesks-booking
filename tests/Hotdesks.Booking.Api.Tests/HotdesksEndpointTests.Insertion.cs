using System.Net;
using System.Net.Http.Json;
using Hotdesks.Booking.Api.Contracts;
using Hotdesks.Booking.Api.Data;
using Hotdesks.Booking.Api.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Hotdesks.Booking.Api.Tests;

public sealed partial class HotdesksEndpointTests
{
    [Fact]
    public async Task Get_hotdesks_returns_an_empty_list()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/hotdesks");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var hotdesks = await response.Content.ReadFromJsonAsync<List<Hotdesk>>();

        Assert.NotNull(hotdesks);
        Assert.Empty(hotdesks);
    }

    [Fact]
    public async Task Hotdesks_can_be_created_and_soft_deleted()
    {
        using var client = factory.CreateClient();
        var runIdentifier = Guid.NewGuid().ToString("N");
        var requests = Enumerable.Range(1, 4)
            .Select(index => new CreateHotdeskRequest
            {
                Name = $"Integration desk {runIdentifier}-{index}",
                IsAvailable247 = index % 2 == 0
            })
            .ToList();

        var createdHotdesks = new List<Hotdesk>();
        foreach (var request in requests)
        {
            var response = await client.PostAsJsonAsync("/api/hotdesks", request);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            var hotdesk = await response.Content.ReadFromJsonAsync<Hotdesk>();
            Assert.NotNull(hotdesk);
            createdHotdesks.Add(hotdesk);
        }

        var getAfterCreateResponse = await client.GetAsync("/api/hotdesks");
        Assert.Equal(HttpStatusCode.OK, getAfterCreateResponse.StatusCode);

        var hotdesksAfterCreate = await getAfterCreateResponse.Content.ReadFromJsonAsync<List<Hotdesk>>();
        Assert.NotNull(hotdesksAfterCreate);
        Assert.Equal(
            createdHotdesks.Select(hotdesk => hotdesk.Id).OrderBy(id => id),
            hotdesksAfterCreate.Select(hotdesk => hotdesk.Id).OrderBy(id => id));

        foreach (var hotdesk in createdHotdesks)
        {
            var response = await client.DeleteAsync($"/api/hotdesks/{hotdesk.Id}");

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        var getAfterDeleteResponse = await client.GetAsync("/api/hotdesks");
        Assert.Equal(HttpStatusCode.OK, getAfterDeleteResponse.StatusCode);

        var hotdesksAfterDelete = await getAfterDeleteResponse.Content.ReadFromJsonAsync<List<Hotdesk>>();
        Assert.NotNull(hotdesksAfterDelete);
        Assert.Empty(hotdesksAfterDelete);
    }

    [Fact]
    public async Task Users_can_be_created()
    {
        using var client = factory.CreateClient();
        var userId = Guid.Empty;

        try
        {
            var response = await client.PostAsJsonAsync(
                "/api/users",
                new CreateUserRequest
                {
                    Name = $"Integration user {Guid.NewGuid():N}"
                });

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            var user = await response.Content.ReadFromJsonAsync<User>();
            Assert.NotNull(user);
            Assert.NotEqual(Guid.Empty, user.Id);
            Assert.StartsWith("Integration user ", user.Name);
            userId = user.Id;
        }
        finally
        {
            await RemoveUserAsync(userId);
        }
    }

    [Fact]
    public async Task Adding_a_reservation_for_a_nonexistent_user_fails()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/reservation/add",
            CreateReservationRequest(Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Adding_a_reservation_for_a_nonexistent_hotdesk_fails()
    {
        using var client = factory.CreateClient();
        var userId = await CreateUserAsync();

        try
        {
            var response = await client.PostAsJsonAsync(
                "/reservation/add",
                CreateReservationRequest(userId, Guid.NewGuid()));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        finally
        {
            await RemoveUserAsync(userId);
        }
    }

    [Fact]
    public async Task Adding_a_reservation_with_a_nonpositive_duration_fails()
    {
        using var client = factory.CreateClient();
        var now = DateTimeOffset.UtcNow;

        var response = await client.PostAsJsonAsync(
            "/reservation/add",
            new AddReservationRequest
            {
                UserId = Guid.NewGuid(),
                HotdeskId = Guid.NewGuid(),
                From = now,
                To = now
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_reservation_can_be_added_and_retrieved()
    {
        using var client = factory.CreateClient();
        var userId = await CreateUserAsync();
        var hotdeskId = Guid.Empty;
        var reservationId = Guid.Empty;

        try
        {
            var createHotdeskResponse = await client.PostAsJsonAsync(
                "/api/hotdesks",
                new CreateHotdeskRequest
                {
                    Name = $"Reservation desk {Guid.NewGuid():N}",
                    IsAvailable247 = true
                });
            Assert.Equal(HttpStatusCode.Created, createHotdeskResponse.StatusCode);

            var hotdesk = await createHotdeskResponse.Content.ReadFromJsonAsync<Hotdesk>();
            Assert.NotNull(hotdesk);
            hotdeskId = hotdesk.Id;

            var addReservationResponse = await client.PostAsJsonAsync(
                "/reservation/add",
                CreateReservationRequest(userId, hotdeskId));
            Assert.Equal(HttpStatusCode.Created, addReservationResponse.StatusCode);

            var reservation = await addReservationResponse.Content.ReadFromJsonAsync<ReservationResponse>();
            Assert.NotNull(reservation);
            reservationId = reservation.Id;
            Assert.Equal(userId, reservation.UserId);
            Assert.Equal(hotdeskId, reservation.HotdeskId);

            var getReservationsResponse = await client.GetAsync("/reservation");
            Assert.Equal(HttpStatusCode.OK, getReservationsResponse.StatusCode);

            var reservations = await getReservationsResponse.Content.ReadFromJsonAsync<List<ReservationResponse>>();
            Assert.NotNull(reservations);
            Assert.Contains(reservations, existing => existing.Id == reservationId);
        }
        finally
        {
            await RemoveReservationAsync(reservationId);
            await RemoveHotdeskAsync(hotdeskId);
            await RemoveUserAsync(userId);
        }
    }

}
