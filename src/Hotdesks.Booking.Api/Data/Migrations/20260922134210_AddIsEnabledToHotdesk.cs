using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hotdesks.Booking.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIsEnabledToHotdesk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsEnabled",
                table: "Hotdesks",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsEnabled",
                table: "Hotdesks");
        }
    }
}
