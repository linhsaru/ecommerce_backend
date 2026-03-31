using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDb_V3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.AlterColumn<Guid>(
                name: "variant_id",
                table: "cart_items",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.CreateTable(
                name: "specification_type",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    unit = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_specification_type", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "product_variant_specification",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    specification_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_product_variant_specification", x => x.id);
                    table.ForeignKey(
                        name: "fk_product_variant_specification_product_variants_product_vari",
                        column: x => x.product_variant_id,
                        principalTable: "product_variants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_product_variant_specification_specification_type_specificat",
                        column: x => x.specification_type_id,
                        principalTable: "specification_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cart_items_variant_id",
                table: "cart_items",
                column: "variant_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_variant_specification_product_variant_id",
                table: "product_variant_specification",
                column: "product_variant_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_variant_specification_specification_type_id",
                table: "product_variant_specification",
                column: "specification_type_id");

            migrationBuilder.AddForeignKey(
                name: "fk_cart_items_product_variants_variant_id",
                table: "cart_items",
                column: "variant_id",
                principalTable: "product_variants",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_cart_items_product_variants_variant_id",
                table: "cart_items");

            migrationBuilder.DropTable(
                name: "product_variant_specification");

            migrationBuilder.DropTable(
                name: "specification_type");

            migrationBuilder.DropIndex(
                name: "ix_cart_items_variant_id",
                table: "cart_items");

            migrationBuilder.AlterColumn<long>(
                name: "variant_id",
                table: "cart_items",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "variant_id1",
                table: "cart_items",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "ix_cart_items_variant_id1",
                table: "cart_items",
                column: "variant_id1");

            migrationBuilder.AddForeignKey(
                name: "fk_cart_items_product_variants_variant_id1",
                table: "cart_items",
                column: "variant_id1",
                principalTable: "product_variants",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
