using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OurStory.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAffinity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Affinity",
                table: "notification_settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "affinity_questions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Text = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_affinity_questions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "affinity_daily_questions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Day = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    QuestionId = table.Column<int>(type: "INTEGER", nullable: true),
                    QuestionText = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    OptionsJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_affinity_daily_questions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_affinity_daily_questions_affinity_questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "affinity_questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "affinity_question_options",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    QuestionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_affinity_question_options", x => x.Id);
                    table.ForeignKey(
                        name: "FK_affinity_question_options_affinity_questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "affinity_questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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

            migrationBuilder.CreateIndex(
                name: "IX_affinity_daily_questions_Day",
                table: "affinity_daily_questions",
                column: "Day",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_affinity_daily_questions_QuestionId",
                table: "affinity_daily_questions",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_affinity_question_options_QuestionId_SortOrder",
                table: "affinity_question_options",
                columns: new[] { "QuestionId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_affinity_questions_IsActive",
                table: "affinity_questions",
                column: "IsActive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "affinity_answers");

            migrationBuilder.DropTable(
                name: "affinity_question_options");

            migrationBuilder.DropTable(
                name: "affinity_daily_questions");

            migrationBuilder.DropTable(
                name: "affinity_questions");

            migrationBuilder.DropColumn(
                name: "Affinity",
                table: "notification_settings");
        }
    }
}
