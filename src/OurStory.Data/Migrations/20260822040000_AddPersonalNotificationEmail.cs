// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OurStory.Data.Migrations;

/// <inheritdoc />
[DbContext(typeof(OurStoryDbContext))]
[Migration("20260822040000_AddPersonalNotificationEmail")]
public sealed class AddPersonalNotificationEmail : Migration {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder) {
        migrationBuilder.AddColumn<string>(
            name: "EmailAddress",
            table: "notification_settings",
            type: "TEXT",
            maxLength: 320,
            nullable: false,
            defaultValue: "");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) {
        migrationBuilder.DropColumn(name: "EmailAddress", table: "notification_settings");
    }
}
