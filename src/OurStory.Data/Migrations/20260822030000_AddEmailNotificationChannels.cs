// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OurStory.Data.Migrations;

/// <inheritdoc />
[DbContext(typeof(OurStoryDbContext))]
[Migration("20260822030000_AddEmailNotificationChannels")]
public sealed class AddEmailNotificationChannels : Migration {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder) {
        migrationBuilder.AddColumn<bool>(
            name: "EmailEnabled",
            table: "notification_settings",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        // 已有用户继续保持原来的 Web Push 行为，不需要重新开启渠道。
        migrationBuilder.AddColumn<bool>(
            name: "WebPushEnabled",
            table: "notification_settings",
            type: "INTEGER",
            nullable: false,
            defaultValue: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) {
        migrationBuilder.DropColumn(name: "EmailEnabled", table: "notification_settings");
        migrationBuilder.DropColumn(name: "WebPushEnabled", table: "notification_settings");
    }
}
