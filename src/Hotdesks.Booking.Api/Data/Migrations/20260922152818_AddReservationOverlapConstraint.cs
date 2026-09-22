using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hotdesks.Booking.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationOverlapConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Reservations_FromBeforeTo",
                table: "Reservations",
                sql: "\"From\" < \"To\"");

            migrationBuilder.Sql(
                """
                ALTER TABLE "Reservations"
                ADD CONSTRAINT "EX_Reservations_HotdeskId_TimeRange_NoOverlap"
                EXCLUDE USING gist (
                    "HotdeskId" WITH =,
                    tstzrange("From", "To", '[)') WITH &&
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "Reservations"
                DROP CONSTRAINT "EX_Reservations_HotdeskId_TimeRange_NoOverlap";
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_Reservations_FromBeforeTo",
                table: "Reservations");
        }
    }
}
