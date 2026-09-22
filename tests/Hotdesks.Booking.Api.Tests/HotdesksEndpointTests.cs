using System.Net;
using System.Net.Http.Json;
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
