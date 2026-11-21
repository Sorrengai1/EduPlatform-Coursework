using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduPlatform.Migrations
{
    /// <inheritdoc />
    public partial class AddLibraryFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Url",
                table: "LibraryItems",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AddColumn<int>(
                name: "AuthorId",
                table: "LibraryItems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "LibraryItems",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "FilePath",
                table: "LibraryItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LibraryItems_AuthorId",
                table: "LibraryItems",
                column: "AuthorId");

            migrationBuilder.AddForeignKey(
                name: "FK_LibraryItems_Students_AuthorId",
                table: "LibraryItems",
                column: "AuthorId",
                principalTable: "Students",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LibraryItems_Students_AuthorId",
                table: "LibraryItems");

            migrationBuilder.DropIndex(
                name: "IX_LibraryItems_AuthorId",
                table: "LibraryItems");

            migrationBuilder.DropColumn(
                name: "AuthorId",
                table: "LibraryItems");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "LibraryItems");

            migrationBuilder.DropColumn(
                name: "FilePath",
                table: "LibraryItems");

            migrationBuilder.AlterColumn<string>(
                name: "Url",
                table: "LibraryItems",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);
        }
    }
}
