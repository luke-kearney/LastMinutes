using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LastMinutes.Migrations
{
    public partial class AddedQueueStatusAndTrackSource : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "LM_Tracks",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "LM_Queue",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Source",
                table: "LM_Tracks");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "LM_Queue");
        }
    }
}
