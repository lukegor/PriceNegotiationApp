using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace PriceNegotiationApp.Modules.Negotiations.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "negotiations");

            migrationBuilder.CreateTable(
                name: "customers",
                schema: "negotiations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    identity_user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "negotiations",
                schema: "negotiations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    base_price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    current_offer = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    proposals_used = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_proposal_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    decided_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_negotiations", x => x.id);
                    table.ForeignKey(
                        name: "fk_negotiations_customers_customer_id",
                        column: x => x.customer_id,
                        principalSchema: "negotiations",
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_customers_identity_user_id",
                schema: "negotiations",
                table: "customers",
                column: "identity_user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_negotiations_customer_id",
                schema: "negotiations",
                table: "negotiations",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_negotiations_product_id_customer_id",
                schema: "negotiations",
                table: "negotiations",
                columns: new[] { "product_id", "customer_id" },
                unique: true,
                filter: "status = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "negotiations",
                schema: "negotiations");

            migrationBuilder.DropTable(
                name: "customers",
                schema: "negotiations");
        }
    }
}
