using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LastMinutes.Migrations
{
    public partial class AddedQueueFailures : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "submitToLeaderboard",
                table: "LM_Queue",
                newName: "SubmitToLeaderboard");

            migrationBuilder.RenameColumn(
                name: "Updated_On",
                table: "LM_Queue",
                newName: "UpdatedOn");

            migrationBuilder.RenameColumn(
                name: "Created_On",
                table: "LM_Queue",
                newName: "CreatedOn");

            migrationBuilder.AlterColumn<string>(
                name: "Username",
                table: "LM_Queue",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "LM_Queue",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<bool>(
                name: "Failed",
                table: "LM_Queue",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Retries",
                table: "LM_Queue",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Failed",
                table: "LM_Queue");

            migrationBuilder.DropColumn(
                name: "Retries",
                table: "LM_Queue");

            migrationBuilder.RenameColumn(
                name: "SubmitToLeaderboard",
                table: "LM_Queue",
                newName: "submitToLeaderboard");

            migrationBuilder.RenameColumn(
                name: "UpdatedOn",
                table: "LM_Queue",
                newName: "Updated_On");

            migrationBuilder.RenameColumn(
                name: "CreatedOn",
                table: "LM_Queue",
                newName: "Created_On");

            migrationBuilder.AlterColumn<string>(
                name: "Username",
                table: "LM_Queue",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(1024)",
                oldMaxLength: 1024);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "LM_Queue",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(512)",
                oldMaxLength: 512);
        }
    }
}
