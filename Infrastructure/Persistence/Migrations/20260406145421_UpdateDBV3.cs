using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDBV3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "product_variant_id",
                table: "components",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_components_product_variant_id",
                table: "components",
                column: "product_variant_id");

            migrationBuilder.AddForeignKey(
                name: "fk_components_product_variants_product_variant_id",
                table: "components",
                column: "product_variant_id",
                principalTable: "product_variants",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_components_product_variants_product_variant_id",
                table: "components");

            migrationBuilder.DropIndex(
                name: "ix_components_product_variant_id",
                table: "components");

            migrationBuilder.DropColumn(
                name: "product_variant_id",
                table: "components");
        }
    }
}
