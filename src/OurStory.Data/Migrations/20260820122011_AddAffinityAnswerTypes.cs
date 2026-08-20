using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OurStory.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAffinityAnswerTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LoveDay",
                table: "affinity_daily_questions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.DropTable(
                name: "affinity_answers");

            migrationBuilder.CreateTable(
                name: "affinity_answers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DailyQuestionId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    SelectedOptionIndexesJson = table.Column<string>(type: "TEXT", nullable: false),
                    TextAnswer = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    AnsweredAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_affinity_answers", x => x.Id);
                    table.CheckConstraint(
                        "CK_affinity_answers_payload",
                        "json_valid(\"SelectedOptionIndexesJson\") " +
                        "AND json_type(\"SelectedOptionIndexesJson\") = 'array' " +
                        "AND ((json_array_length(\"SelectedOptionIndexesJson\") > 0 AND \"TextAnswer\" IS NULL) " +
                        "OR (json_array_length(\"SelectedOptionIndexesJson\") = 0 " +
                        "AND \"TextAnswer\" IS NOT NULL AND length(trim(\"TextAnswer\")) > 0))");
                    table.ForeignKey(
                        name: "FK_affinity_answers_affinity_daily_questions_DailyQuestionId",
                        column: x => x.DailyQuestionId,
                        principalTable: "affinity_daily_questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_affinity_answers_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_affinity_answers_DailyQuestionId_Role",
                table: "affinity_answers",
                columns: new[] { "DailyQuestionId", "Role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_affinity_answers_DailyQuestionId_UserId",
                table: "affinity_answers",
                columns: new[] { "DailyQuestionId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_affinity_answers_UserId",
                table: "affinity_answers",
                column: "UserId");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "affinity_answers");

            migrationBuilder.CreateTable(
                name: "affinity_answers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DailyQuestionId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    OptionIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    AnsweredAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_affinity_answers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_affinity_answers_affinity_daily_questions_DailyQuestionId",
                        column: x => x.DailyQuestionId,
                        principalTable: "affinity_daily_questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_affinity_answers_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_affinity_answers_DailyQuestionId_Role",
                table: "affinity_answers",
                columns: new[] { "DailyQuestionId", "Role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_affinity_answers_DailyQuestionId_UserId",
                table: "affinity_answers",
                columns: new[] { "DailyQuestionId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_affinity_answers_UserId",
                table: "affinity_answers",
                column: "UserId");

            migrationBuilder.DropColumn(
                name: "LoveDay",
                table: "affinity_daily_questions");
        }
    }
}
