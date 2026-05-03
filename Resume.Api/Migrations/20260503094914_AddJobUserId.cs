using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Resume.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddJobUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Remove orphaned jobs (UserId = '' fails the new FK constraint).
            // All pre-migration jobs lacked an owner and would be invisible to users anyway.
            migrationBuilder.Sql("DELETE FROM \"CandidateScores\";");
            migrationBuilder.Sql("DELETE FROM \"Feedbacks\";");
            migrationBuilder.Sql("DELETE FROM \"Candidates\";");
            migrationBuilder.Sql("DELETE FROM \"Jobs\";");

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "Jobs",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_UserId",
                table: "Jobs",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Jobs_AspNetUsers_UserId",
                table: "Jobs",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Jobs_AspNetUsers_UserId",
                table: "Jobs");

            migrationBuilder.DropIndex(
                name: "IX_Jobs_UserId",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Jobs");
        }
    }
}
