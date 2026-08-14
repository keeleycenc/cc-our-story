using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OurStory.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAnniversaryAuthor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AuthorId",
                table: "anniversaries",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_anniversaries_AuthorId",
                table: "anniversaries",
                column: "AuthorId");

            migrationBuilder.AddForeignKey(
                name: "FK_anniversaries_users_AuthorId",
                table: "anniversaries",
                column: "AuthorId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_anniversaries_users_AuthorId",
                table: "anniversaries");

            migrationBuilder.DropIndex(
                name: "IX_anniversaries_AuthorId",
                table: "anniversaries");

            migrationBuilder.DropColumn(
                name: "AuthorId",
                table: "anniversaries");
        }
    }
}
