using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LastMinutes.Migrations
{
    public partial class ModeImplementation : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FromWhen",
                table: "LM_Results",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "Mode",
                table: "LM_Results",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ToWhen",
                table: "LM_Results",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "Mode",
                table: "LM_Queue",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FromWhen",
                table: "LM_Results");

            migrationBuilder.DropColumn(
                name: "Mode",
                table: "LM_Results");

            migrationBuilder.DropColumn(
                name: "ToWhen",
                table: "LM_Results");

            migrationBuilder.DropColumn(
                name: "Mode",
                table: "LM_Queue");
        }
    }
}
