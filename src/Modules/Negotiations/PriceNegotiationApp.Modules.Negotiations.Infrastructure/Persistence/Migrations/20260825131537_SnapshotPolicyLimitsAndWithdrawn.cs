using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace PriceNegotiationApp.Modules.Negotiations.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SnapshotPolicyLimitsAndWithdrawn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "ix_negotiations_product_id_customer_id",
                schema: "negotiations",
                table: "negotiations",
                newName: "uq_negotiations_open_product_customer");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_staff_action_at_utc",
                schema: "negotiations",
                table: "negotiations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "max_proposals",
                schema: "negotiations",
                table: "negotiations",
                type: "integer",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<decimal>(
                name: "offer_multiplier_limit",
                schema: "negotiations",
                table: "negotiations",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 2.0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "last_staff_action_at_utc",
                schema: "negotiations",
                table: "negotiations");

            migrationBuilder.DropColumn(
                name: "max_proposals",
                schema: "negotiations",
                table: "negotiations");

            migrationBuilder.DropColumn(
                name: "offer_multiplier_limit",
                schema: "negotiations",
                table: "negotiations");

            migrationBuilder.RenameIndex(
                name: "uq_negotiations_open_product_customer",
                schema: "negotiations",
                table: "negotiations",
                newName: "ix_negotiations_product_id_customer_id");
        }
    }
}
