using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OurStory.Data.Migrations
{
    /// <inheritdoc />
    public partial class TrackAffinityQuestionCreator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CreatedByUserId",
                table: "affinity_questions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_affinity_questions_CreatedByUserId",
                table: "affinity_questions",
                column: "CreatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_affinity_questions_users_CreatedByUserId",
                table: "affinity_questions",
                column: "CreatedByUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_affinity_questions_users_CreatedByUserId",
                table: "affinity_questions");

            migrationBuilder.DropIndex(
                name: "IX_affinity_questions_CreatedByUserId",
                table: "affinity_questions");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "affinity_questions");
        }
    }
}
