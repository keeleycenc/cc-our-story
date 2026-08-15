using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OurStory.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHeartPointsAndShop : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "heart_point_entries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    ChangeAmount = table.Column<int>(type: "INTEGER", nullable: false),
                    Reason = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceKey = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    IsBackfill = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_heart_point_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_heart_point_entries_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shop_presets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    CoverUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    RedeemMode = table.Column<int>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shop_presets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "shop_items",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    CoverUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Price = table.Column<int>(type: "INTEGER", nullable: false),
                    IsPrivate = table.Column<bool>(type: "INTEGER", nullable: false),
                    RedeemMode = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    SellerId = table.Column<int>(type: "INTEGER", nullable: false),
                    BuyerId = table.Column<int>(type: "INTEGER", nullable: true),
                    PresetId = table.Column<int>(type: "INTEGER", nullable: true),
                    ListingDays = table.Column<int>(type: "INTEGER", nullable: false),
                    ValidDays = table.Column<int>(type: "INTEGER", nullable: false),
                    ListedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ListingExpiresAt = table.Column<long>(type: "INTEGER", nullable: false),
                    PurchasedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    ExpiresAt = table.Column<long>(type: "INTEGER", nullable: true),
                    RedeemRequestedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    UsedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shop_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_shop_items_shop_presets_PresetId",
                        column: x => x.PresetId,
                        principalTable: "shop_presets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_shop_items_users_BuyerId",
                        column: x => x.BuyerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_shop_items_users_SellerId",
                        column: x => x.SellerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_heart_point_entries_CreatedAt",
                table: "heart_point_entries",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_heart_point_entries_UserId_SourceKey",
                table: "heart_point_entries",
                columns: new[] { "UserId", "SourceKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shop_items_BuyerId",
                table: "shop_items",
                column: "BuyerId");

            migrationBuilder.CreateIndex(
                name: "IX_shop_items_PresetId",
                table: "shop_items",
                column: "PresetId");

            migrationBuilder.CreateIndex(
                name: "IX_shop_items_SellerId",
                table: "shop_items",
                column: "SellerId");

            migrationBuilder.CreateIndex(
                name: "IX_shop_items_Status_IsPrivate",
                table: "shop_items",
                columns: new[] { "Status", "IsPrivate" });

            migrationBuilder.CreateIndex(
                name: "IX_shop_presets_IsActive_SortOrder",
                table: "shop_presets",
                columns: new[] { "IsActive", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "heart_point_entries");

            migrationBuilder.DropTable(
                name: "shop_items");

            migrationBuilder.DropTable(
                name: "shop_presets");
        }
    }
}
