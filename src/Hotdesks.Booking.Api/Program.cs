using Hotdesks.Booking.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddDbContext<HotdesksBookingDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("HotdesksBooking")
        ?? throw new InvalidOperationException("Connection string 'HotdesksBooking' was not found.")));

var app = builder.Build();

app.MapControllers();

app.Run();

public partial class Program;
