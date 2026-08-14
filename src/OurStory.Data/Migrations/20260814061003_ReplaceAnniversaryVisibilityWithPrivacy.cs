using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OurStory.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceAnniversaryVisibilityWithPrivacy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsVisible",
                table: "anniversaries",
                newName: "IsPrivate");

            migrationBuilder.RenameIndex(
                name: "IX_anniversaries_IsVisible_AnniversaryDate",
                table: "anniversaries",
                newName: "IX_anniversaries_IsPrivate_AnniversaryDate");

            // 旧值 true 表示公开展示；新值 true 表示仅情侣可见，迁移时必须反转语义。
            migrationBuilder.Sql("UPDATE anniversaries SET IsPrivate = CASE WHEN IsPrivate = 1 THEN 0 ELSE 1 END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE anniversaries SET IsPrivate = CASE WHEN IsPrivate = 1 THEN 0 ELSE 1 END;");

            migrationBuilder.RenameColumn(
                name: "IsPrivate",
                table: "anniversaries",
                newName: "IsVisible");

            migrationBuilder.RenameIndex(
                name: "IX_anniversaries_IsPrivate_AnniversaryDate",
                table: "anniversaries",
                newName: "IX_anniversaries_IsVisible_AnniversaryDate");
        }
    }
}
