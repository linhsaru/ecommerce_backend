using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDBV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "components",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    component_type = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    price = table.Column<decimal>(type: "numeric", nullable: false),
                    brand = table.Column<string>(type: "text", nullable: true),
                    socket = table.Column<string>(type: "text", nullable: true),
                    tdp = table.Column<int>(type: "integer", nullable: true),
                    cores = table.Column<int>(type: "integer", nullable: true),
                    boost_clock = table.Column<double>(type: "double precision", nullable: true),
                    vram = table.Column<int>(type: "integer", nullable: true),
                    length = table.Column<int>(type: "integer", nullable: true),
                    power_consumption = table.Column<int>(type: "integer", nullable: true),
                    required_connectors = table.Column<List<string>>(type: "text[]", nullable: true),
                    recommended_psu = table.Column<int>(type: "integer", nullable: true),
                    form_factor = table.Column<string>(type: "text", nullable: true),
                    ram_slots = table.Column<int>(type: "integer", nullable: true),
                    max_ram = table.Column<int>(type: "integer", nullable: true),
                    ram_type = table.Column<string>(type: "text", nullable: true),
                    chipset = table.Column<string>(type: "text", nullable: true),
                    supported_cpu_generations = table.Column<List<string>>(type: "text[]", nullable: true),
                    m2slots = table.Column<int>(type: "integer", nullable: true),
                    sata_ports = table.Column<int>(type: "integer", nullable: true),
                    pcie_version = table.Column<string>(type: "text", nullable: true),
                    capacity = table.Column<int>(type: "integer", nullable: true),
                    speed = table.Column<int>(type: "integer", nullable: true),
                    memory_type = table.Column<string>(type: "text", nullable: true),
                    timings = table.Column<string>(type: "text", nullable: true),
                    voltage = table.Column<string>(type: "text", nullable: true),
                    wattage = table.Column<int>(type: "integer", nullable: true),
                    certification = table.Column<string>(type: "text", nullable: true),
                    pcie_connectors = table.Column<List<string>>(type: "text[]", nullable: true),
                    cpu_connector = table.Column<string>(type: "text", nullable: true),
                    drive_interface = table.Column<string>(type: "text", nullable: true),
                    drive_form_factor = table.Column<string>(type: "text", nullable: true),
                    cooling_type = table.Column<string>(type: "text", nullable: true),
                    tdp_support = table.Column<int>(type: "integer", nullable: true),
                    supported_sockets = table.Column<List<string>>(type: "text[]", nullable: true),
                    height = table.Column<int>(type: "integer", nullable: true),
                    material = table.Column<string>(type: "text", nullable: true),
                    max_gpu_length = table.Column<int>(type: "integer", nullable: true),
                    max_cooler_height = table.Column<int>(type: "integer", nullable: true),
                    supported_form_factors = table.Column<List<string>>(type: "text[]", nullable: true),
                    psu_form_factor = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_components", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "components");
        }
    }
}
