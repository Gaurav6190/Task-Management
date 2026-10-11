using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignedToAndSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "Tasks",
                newName: "CreatedDate");

            migrationBuilder.AddColumn<string>(
                name: "AssignedTo",
                table: "Tasks",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedDate",
                table: "Tasks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Tasks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedDate",
                table: "Tasks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_AssignedTo",
                table: "Tasks",
                column: "AssignedTo");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_Priority",
                table: "Tasks",
                column: "Priority");

            // Status values renamed: Open -> ToDo, Completed -> Done (stored as strings, so EF can't detect this).
            migrationBuilder.Sql("UPDATE [Tasks] SET [Status] = N'ToDo' WHERE [Status] = N'Open';");
            migrationBuilder.Sql("UPDATE [Tasks] SET [Status] = N'Done' WHERE [Status] = N'Completed';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE [Tasks] SET [Status] = N'Open' WHERE [Status] = N'ToDo';");
            migrationBuilder.Sql("UPDATE [Tasks] SET [Status] = N'Completed' WHERE [Status] = N'Done';");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_AssignedTo",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_Priority",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "AssignedTo",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "DeletedDate",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "ModifiedDate",
                table: "Tasks");

            migrationBuilder.RenameColumn(
                name: "CreatedDate",
                table: "Tasks",
                newName: "CreatedAtUtc");
        }
    }
}
