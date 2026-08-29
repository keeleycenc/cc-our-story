using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OurStory.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCycleTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CoupleRelationshipId",
                table: "users",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "couple_relationships",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_couple_relationships", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "cycle_daily_logs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RelationshipId = table.Column<int>(type: "INTEGER", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Flow = table.Column<int>(type: "INTEGER", nullable: false),
                    Mood = table.Column<int>(type: "INTEGER", nullable: false),
                    Pain = table.Column<int>(type: "INTEGER", nullable: false),
                    Symptoms = table.Column<int>(type: "INTEGER", nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cycle_daily_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cycle_daily_logs_couple_relationships_RelationshipId",
                        column: x => x.RelationshipId,
                        principalTable: "couple_relationships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_cycle_daily_logs_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cycle_daily_logs_users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cycle_records",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RelationshipId = table.Column<int>(type: "INTEGER", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 1200, nullable: false),
                    SummarySource = table.Column<int>(type: "INTEGER", nullable: false),
                    SummaryStamp = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    SummaryUpdatedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    RequestKey = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cycle_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cycle_records_couple_relationships_RelationshipId",
                        column: x => x.RelationshipId,
                        principalTable: "couple_relationships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_cycle_records_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cycle_records_users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_users_CoupleRelationshipId",
                table: "users",
                column: "CoupleRelationshipId");

            migrationBuilder.CreateIndex(
                name: "IX_couple_relationships_IsActive",
                table: "couple_relationships",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_cycle_daily_logs_CreatedByUserId",
                table: "cycle_daily_logs",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_cycle_daily_logs_RelationshipId_Date",
                table: "cycle_daily_logs",
                columns: new[] { "RelationshipId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cycle_daily_logs_UpdatedByUserId",
                table: "cycle_daily_logs",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_cycle_records_CreatedByUserId",
                table: "cycle_records",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_cycle_records_RelationshipId",
                table: "cycle_records",
                column: "RelationshipId",
                unique: true,
                filter: "\"EndDate\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_cycle_records_RelationshipId_RequestKey",
                table: "cycle_records",
                columns: new[] { "RelationshipId", "RequestKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cycle_records_RelationshipId_StartDate",
                table: "cycle_records",
                columns: new[] { "RelationshipId", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_cycle_records_UpdatedByUserId",
                table: "cycle_records",
                column: "UpdatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_users_couple_relationships_CoupleRelationshipId",
                table: "users",
                column: "CoupleRelationshipId",
                principalTable: "couple_relationships",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_users_couple_relationships_CoupleRelationshipId",
                table: "users");

            migrationBuilder.DropTable(
                name: "cycle_daily_logs");

            migrationBuilder.DropTable(
                name: "cycle_records");

            migrationBuilder.DropTable(
                name: "couple_relationships");

            migrationBuilder.DropIndex(
                name: "IX_users_CoupleRelationshipId",
                table: "users");

            migrationBuilder.DropColumn(
                name: "CoupleRelationshipId",
                table: "users");
        }
    }
}
