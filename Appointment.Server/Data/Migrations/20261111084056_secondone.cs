using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Appointment.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class secondone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Branches",
                columns: new[] { "Id", "Address", "CreatedAt", "Name", "PhoneNumber", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111111"), "123 Main Street", new DateTime(2026, 11, 11, 8, 40, 55, 384, DateTimeKind.Utc).AddTicks(4622), "Downtown Branch", "555-0100", null },
                    { new Guid("11111111-1111-1111-1111-111111111112"), "456 Commerce Blvd", new DateTime(2026, 11, 11, 8, 40, 55, 384, DateTimeKind.Utc).AddTicks(4632), "Suburban Plaza", "555-0200", null },
                    { new Guid("11111111-1111-1111-1111-111111111113"), "789 Park Avenue", new DateTime(2026, 11, 11, 8, 40, 55, 384, DateTimeKind.Utc).AddTicks(4635), "Westside Health Center", "555-0300", null }
                });

            migrationBuilder.InsertData(
                table: "Services",
                columns: new[] { "Id", "CreatedAt", "Description", "DurationMinutes", "Name", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("22222222-2222-2222-2222-222222222221"), new DateTime(2026, 11, 11, 8, 40, 55, 384, DateTimeKind.Utc).AddTicks(4815), "Standard 30-minute appointment slot for routine checkups", 30, "General Consultation", null },
                    { new Guid("22222222-2222-2222-2222-222222222222"), new DateTime(2026, 11, 11, 8, 40, 55, 384, DateTimeKind.Utc).AddTicks(4932), "Comprehensive 60-minute in-depth review", 60, "Specialized Health Assessment", null },
                    { new Guid("22222222-2222-2222-2222-222222222223"), new DateTime(2026, 11, 11, 8, 40, 55, 384, DateTimeKind.Utc).AddTicks(4936), "Quick 15-minute diagnostic drop-off or quick consult", 15, "Express Check-in & Lab", null }
                });

            migrationBuilder.InsertData(
                table: "BranchServices",
                columns: new[] { "BranchId", "ServiceId" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111111"), new Guid("22222222-2222-2222-2222-222222222221") },
                    { new Guid("11111111-1111-1111-1111-111111111111"), new Guid("22222222-2222-2222-2222-222222222222") },
                    { new Guid("11111111-1111-1111-1111-111111111111"), new Guid("22222222-2222-2222-2222-222222222223") },
                    { new Guid("11111111-1111-1111-1111-111111111112"), new Guid("22222222-2222-2222-2222-222222222221") },
                    { new Guid("11111111-1111-1111-1111-111111111112"), new Guid("22222222-2222-2222-2222-222222222223") },
                    { new Guid("11111111-1111-1111-1111-111111111113"), new Guid("22222222-2222-2222-2222-222222222221") },
                    { new Guid("11111111-1111-1111-1111-111111111113"), new Guid("22222222-2222-2222-2222-222222222222") }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "BranchServices",
                keyColumns: new[] { "BranchId", "ServiceId" },
                keyValues: new object[] { new Guid("11111111-1111-1111-1111-111111111111"), new Guid("22222222-2222-2222-2222-222222222221") });

            migrationBuilder.DeleteData(
                table: "BranchServices",
                keyColumns: new[] { "BranchId", "ServiceId" },
                keyValues: new object[] { new Guid("11111111-1111-1111-1111-111111111111"), new Guid("22222222-2222-2222-2222-222222222222") });

            migrationBuilder.DeleteData(
                table: "BranchServices",
                keyColumns: new[] { "BranchId", "ServiceId" },
                keyValues: new object[] { new Guid("11111111-1111-1111-1111-111111111111"), new Guid("22222222-2222-2222-2222-222222222223") });

            migrationBuilder.DeleteData(
                table: "BranchServices",
                keyColumns: new[] { "BranchId", "ServiceId" },
                keyValues: new object[] { new Guid("11111111-1111-1111-1111-111111111112"), new Guid("22222222-2222-2222-2222-222222222221") });

            migrationBuilder.DeleteData(
                table: "BranchServices",
                keyColumns: new[] { "BranchId", "ServiceId" },
                keyValues: new object[] { new Guid("11111111-1111-1111-1111-111111111112"), new Guid("22222222-2222-2222-2222-222222222223") });

            migrationBuilder.DeleteData(
                table: "BranchServices",
                keyColumns: new[] { "BranchId", "ServiceId" },
                keyValues: new object[] { new Guid("11111111-1111-1111-1111-111111111113"), new Guid("22222222-2222-2222-2222-222222222221") });

            migrationBuilder.DeleteData(
                table: "BranchServices",
                keyColumns: new[] { "BranchId", "ServiceId" },
                keyValues: new object[] { new Guid("11111111-1111-1111-1111-111111111113"), new Guid("22222222-2222-2222-2222-222222222222") });

            migrationBuilder.DeleteData(
                table: "Branches",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.DeleteData(
                table: "Branches",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111112"));

            migrationBuilder.DeleteData(
                table: "Branches",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111113"));

            migrationBuilder.DeleteData(
                table: "Services",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222221"));

            migrationBuilder.DeleteData(
                table: "Services",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"));

            migrationBuilder.DeleteData(
                table: "Services",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222223"));
        }
    }
}
