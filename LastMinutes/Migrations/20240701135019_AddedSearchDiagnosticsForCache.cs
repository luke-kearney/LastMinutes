using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LastMinutes.Migrations
{
    public partial class AddedSearchDiagnosticsForCache : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AddedByResult_ArtistName",
                table: "LM_Tracks",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AddedByResult_Title",
                table: "LM_Tracks",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AddedByResult_ArtistName",
                table: "LM_Tracks");

            migrationBuilder.DropColumn(
                name: "AddedByResult_Title",
                table: "LM_Tracks");
        }
    }
}
