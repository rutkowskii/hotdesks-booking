using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hotdesks.Booking.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIsActiveToReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Reservations",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.Sql(
                """
                ALTER TABLE "Reservations"
                DROP CONSTRAINT "EX_Reservations_HotdeskId_TimeRange_NoOverlap";

                ALTER TABLE "Reservations"
                ADD CONSTRAINT "EX_Reservations_HotdeskId_TimeRange_NoOverlap"
                EXCLUDE USING gist (
                    "HotdeskId" WITH =,
                    tstzrange("From", "To", '[)') WITH &&
                )
                WHERE ("IsActive");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "Reservations"
                DROP CONSTRAINT "EX_Reservations_HotdeskId_TimeRange_NoOverlap";

                ALTER TABLE "Reservations"
                ADD CONSTRAINT "EX_Reservations_HotdeskId_TimeRange_NoOverlap"
                EXCLUDE USING gist (
                    "HotdeskId" WITH =,
                    tstzrange("From", "To", '[)') WITH &&
                );
                """);

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Reservations");
        }
    }
}
