using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OurStory.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLlmAtmosphereComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LlmAvatarUrl",
                table: "comments",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LlmMemberId",
                table: "comments",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_comments_MomentId_LlmMemberId",
                table: "comments",
                columns: new[] { "MomentId", "LlmMemberId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_comments_MomentId_LlmMemberId",
                table: "comments");

            migrationBuilder.DropColumn(
                name: "LlmAvatarUrl",
                table: "comments");

            migrationBuilder.DropColumn(
                name: "LlmMemberId",
                table: "comments");
        }
    }
}
