using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hotdesks.Booking.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationVersioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsLastVersion",
                table: "Reservations",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Reservations",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "VersionId",
                table: "Reservations",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "Reservations"
                SET "VersionId" = "Id";
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "VersionId",
                table: "Reservations",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_Reservations_VersionId_LastVersion",
                table: "Reservations",
                column: "VersionId",
                unique: true,
                filter: "\"IsLastVersion\"");

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
                WHERE ("IsActive" AND "IsLastVersion");
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
                )
                WHERE ("IsActive");
                """);

            migrationBuilder.DropIndex(
                name: "UX_Reservations_VersionId_LastVersion",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "IsLastVersion",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "VersionId",
                table: "Reservations");
        }
    }
}
