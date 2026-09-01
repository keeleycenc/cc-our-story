using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OurStory.Data.Migrations
{
    /// <inheritdoc />
    public partial class AppendCycleDailyLogsAndIntimacy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_cycle_daily_logs_RelationshipId_Date",
                table: "cycle_daily_logs");

            migrationBuilder.AddColumn<int>(
                name: "IntimacyOutcome",
                table: "cycle_daily_logs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "IntimacyProtection",
                table: "cycle_daily_logs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsIntimate",
                table: "cycle_daily_logs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_cycle_daily_logs_RelationshipId_Date",
                table: "cycle_daily_logs",
                columns: new[] { "RelationshipId", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_cycle_daily_logs_RelationshipId_Date",
                table: "cycle_daily_logs");

            migrationBuilder.DropColumn(
                name: "IntimacyOutcome",
                table: "cycle_daily_logs");

            migrationBuilder.DropColumn(
                name: "IntimacyProtection",
                table: "cycle_daily_logs");

            migrationBuilder.DropColumn(
                name: "IsIntimate",
                table: "cycle_daily_logs");

            migrationBuilder.CreateIndex(
                name: "IX_cycle_daily_logs_RelationshipId_Date",
                table: "cycle_daily_logs",
                columns: new[] { "RelationshipId", "Date" },
                unique: true);
        }
    }
}
