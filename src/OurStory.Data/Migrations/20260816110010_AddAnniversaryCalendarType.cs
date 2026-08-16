using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OurStory.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAnniversaryCalendarType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CalendarType",
                table: "anniversaries",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CalendarType",
                table: "anniversaries");
        }
    }
}
