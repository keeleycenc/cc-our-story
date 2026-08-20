using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OurStory.Data.Migrations
{
    /// <inheritdoc />
    public partial class ImproveAffinityExperience : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSealed",
                table: "affinity_questions",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "RewardPoints",
                table: "affinity_questions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "affinity_questions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RewardPoints",
                table: "affinity_daily_questions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "affinity_daily_questions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsSealed",
                table: "affinity_questions");

            migrationBuilder.DropColumn(
                name: "RewardPoints",
                table: "affinity_questions");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "affinity_questions");

            migrationBuilder.DropColumn(
                name: "RewardPoints",
                table: "affinity_daily_questions");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "affinity_daily_questions");
        }
    }
}
