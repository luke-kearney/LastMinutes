using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LastMinutes.Migrations
{
    public partial class AddedSearchExtraDiagForCache : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SimilarityScore_ArtistName",
                table: "LM_Tracks",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SimilarityScore_Title",
                table: "LM_Tracks",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SimilarityScore_ArtistName",
                table: "LM_Tracks");

            migrationBuilder.DropColumn(
                name: "SimilarityScore_Title",
                table: "LM_Tracks");
        }
    }
}
