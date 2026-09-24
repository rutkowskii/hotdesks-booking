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
    public async Task Editing_a_reservation_creates_the_next_version()
    {
        using var client = factory.CreateClient();
        var userId = await CreateUserAsync();
        var hotdeskId = await CreateHotdeskAsync(client);
        var reservationIds = new List<Guid>();
        var from = DateTimeOffset.UtcNow.AddHours(1);

        try
        {
            var createResponse = await client.PostAsJsonAsync(
                "/reservation/add",
                new AddReservationRequest
                {
                    UserId = userId,
                    HotdeskId = hotdeskId,
                    From = from,
                    To = from.AddHours(1)
                });
            var initialReservation = await createResponse.Content.ReadFromJsonAsync<ReservationResponse>();
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            Assert.NotNull(initialReservation);
            reservationIds.Add(initialReservation.Id);

            var editResponse = await client.PostAsJsonAsync(
                $"/reservation/{initialReservation.Id}/edit",
                new EditReservationRequest
                {
                    HotdeskId = hotdeskId,
                    From = from.AddHours(2),
                    To = from.AddHours(3)
                });
            Assert.Equal(HttpStatusCode.Created, editResponse.StatusCode);

            var editedReservation = await editResponse.Content.ReadFromJsonAsync<ReservationResponse>();
            Assert.NotNull(editedReservation);
            Assert.Equal(initialReservation.VersionId, editedReservation.VersionId);
            Assert.Equal(2, editedReservation.Version);
            Assert.True(editedReservation.IsLastVersion);
            reservationIds.Add(editedReservation.Id);

            var currentResponse = await client.GetAsync("/reservation");
            var currentReservations = await currentResponse.Content.ReadFromJsonAsync<List<ReservationResponse>>();
            Assert.NotNull(currentReservations);
            Assert.Contains(currentReservations, reservation => reservation.Id == editedReservation.Id);
            Assert.DoesNotContain(currentReservations, reservation => reservation.Id == initialReservation.Id);

            var historyResponse = await client.GetAsync($"/reservation/{initialReservation.VersionId}/history");
            var history = await historyResponse.Content.ReadFromJsonAsync<List<ReservationResponse>>();
            Assert.NotNull(history);
            Assert.Equal(2, history.Count);
            Assert.Contains(history, reservation => reservation.Id == initialReservation.Id && !reservation.IsLastVersion);
            Assert.Contains(history, reservation => reservation.Id == editedReservation.Id && reservation.IsLastVersion);
        }
        finally
        {
            foreach (var reservationId in reservationIds)
            {
                await RemoveReservationAsync(reservationId);
            }

            await RemoveHotdeskAsync(hotdeskId);
            await RemoveUserAsync(userId);
        }
    }

    [Fact]
    public async Task Editing_a_noncurrent_reservation_fails()
    {
        using var client = factory.CreateClient();
        var userId = await CreateUserAsync();
        var hotdeskId = await CreateHotdeskAsync(client);
        var reservationIds = new List<Guid>();
        var from = DateTimeOffset.UtcNow.AddHours(1);

        try
        {
            var createResponse = await client.PostAsJsonAsync(
                "/reservation/add",
                new AddReservationRequest
                {
                    UserId = userId,
                    HotdeskId = hotdeskId,
                    From = from,
                    To = from.AddHours(1)
                });
            var initialReservation = await createResponse.Content.ReadFromJsonAsync<ReservationResponse>();
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            Assert.NotNull(initialReservation);
            reservationIds.Add(initialReservation.Id);

            var firstEditResponse = await client.PostAsJsonAsync(
                $"/reservation/{initialReservation.Id}/edit",
                CreateEditRequest(hotdeskId, from));
            var editedReservation = await firstEditResponse.Content.ReadFromJsonAsync<ReservationResponse>();
            Assert.Equal(HttpStatusCode.Created, firstEditResponse.StatusCode);
            Assert.NotNull(editedReservation);
            reservationIds.Add(editedReservation.Id);

            var staleEditResponse = await client.PostAsJsonAsync(
                $"/reservation/{initialReservation.Id}/edit",
                CreateEditRequest(hotdeskId, from));

            Assert.Equal(HttpStatusCode.Conflict, staleEditResponse.StatusCode);
        }
        finally
        {
            foreach (var reservationId in reservationIds)
            {
                await RemoveReservationAsync(reservationId);
            }

            await RemoveHotdeskAsync(hotdeskId);
            await RemoveUserAsync(userId);
        }
    }

    [Fact]
    public async Task Concurrent_edits_allow_only_one_next_version()
    {
        using var client = factory.CreateClient();
        var userId = await CreateUserAsync();
        var hotdeskId = await CreateHotdeskAsync(client);
        var reservationIds = new List<Guid>();
        var from = DateTimeOffset.UtcNow.AddHours(1);

        try
        {
            var createResponse = await client.PostAsJsonAsync(
                "/reservation/add",
                new AddReservationRequest
                {
                    UserId = userId,
                    HotdeskId = hotdeskId,
                    From = from,
                    To = from.AddHours(1)
                });
            var initialReservation = await createResponse.Content.ReadFromJsonAsync<ReservationResponse>();
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            Assert.NotNull(initialReservation);
            reservationIds.Add(initialReservation.Id);

            var firstEditTask = client.PostAsJsonAsync(
                $"/reservation/{initialReservation.Id}/edit",
                CreateEditRequest(hotdeskId, from));
            var secondEditTask = client.PostAsJsonAsync(
                $"/reservation/{initialReservation.Id}/edit",
                CreateEditRequest(hotdeskId, from));

            var editResponses = await Task.WhenAll(firstEditTask, secondEditTask);

            Assert.Single(editResponses, response => response.StatusCode == HttpStatusCode.Created);
            Assert.Single(editResponses, response => response.StatusCode == HttpStatusCode.Conflict);

            var historyResponse = await client.GetAsync($"/reservation/{initialReservation.VersionId}/history");
            var history = await historyResponse.Content.ReadFromJsonAsync<List<ReservationResponse>>();
            Assert.NotNull(history);
            Assert.Equal(2, history.Count);
            var latestReservation = Assert.Single(history, reservation => reservation.IsLastVersion);
            reservationIds.Add(latestReservation.Id);
        }
        finally
        {
            foreach (var reservationId in reservationIds)
            {
                await RemoveReservationAsync(reservationId);
            }

            await RemoveHotdeskAsync(hotdeskId);
            await RemoveUserAsync(userId);
        }
    }

}
