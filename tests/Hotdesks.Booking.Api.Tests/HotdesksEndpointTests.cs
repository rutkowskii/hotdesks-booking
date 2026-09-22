using System.Net;
using System.Net.Http.Json;
using Hotdesks.Booking.Api.Contracts;
using Hotdesks.Booking.Api.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Hotdesks.Booking.Api.Tests;

public sealed class HotdesksEndpointTests(HotdesksApiFactory factory) : IClassFixture<HotdesksApiFactory>
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
}

public sealed class HotdesksApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            configurationBuilder.AddJsonFile(
                Path.Combine(AppContext.BaseDirectory, "appsettings.Test.json"),
                optional: false,
                reloadOnChange: false));
    }
}
