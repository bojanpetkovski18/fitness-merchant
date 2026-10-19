using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FitnessMerchant.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDemoState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "demo_state",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    last_reset_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_demo_state", x => x.id);
                    table.CheckConstraint("ck_demo_state_singleton", "id = 1");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "demo_state");
        }
    }
}
