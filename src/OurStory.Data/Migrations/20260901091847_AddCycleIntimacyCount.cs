using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OurStory.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCycleIntimacyCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IntimacyCount",
                table: "cycle_daily_logs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // 迁移前的亲密记录都代表至少一次互动，补齐默认次数。
            migrationBuilder.Sql(
                "UPDATE cycle_daily_logs SET IntimacyCount = 1 WHERE IsIntimate = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IntimacyCount",
                table: "cycle_daily_logs");
        }
    }
}
