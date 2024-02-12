using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LastMinutes.Migrations
{
    public partial class AddingBadScrobbles : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BadScrobbles",
                table: "LM_Results",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BadScrobbles",
                table: "LM_Results");
        }
    }
}
