// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OurStory.Data.Migrations;

/// <inheritdoc />
[DbContext(typeof(OurStoryDbContext))]
[Migration("20260902090000_AddCycleNotificationSetting")]
public sealed class AddCycleNotificationSetting : Migration {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder) {
        migrationBuilder.AddColumn<bool>(
            name: "Cycle",
            table: "notification_settings",
            type: "INTEGER",
            nullable: false,

            // 其余几项通知默认都是开的，已有的两行也跟着开，别让升级这一下把人静音了
            defaultValue: true);

        migrationBuilder.AddColumn<string>(
            name: "LastCycleOn",
            table: "notification_settings",
            type: "TEXT",
            maxLength: 10,
            nullable: false,
            defaultValue: "");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) {
        migrationBuilder.DropColumn(name: "LastCycleOn", table: "notification_settings");
        migrationBuilder.DropColumn(name: "Cycle", table: "notification_settings");
    }
}
