using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeworkHero.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentCorrections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StudentCorrections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HomeworkResultId = table.Column<int>(type: "int", nullable: false),
                    TeacherId = table.Column<int>(type: "int", nullable: false),
                    Mark = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentCorrections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentCorrections_HomeworkResults_HomeworkResultId",
                        column: x => x.HomeworkResultId,
                        principalTable: "HomeworkResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentCorrections_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudentCorrections_HomeworkResultId",
                table: "StudentCorrections",
                column: "HomeworkResultId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentCorrections_TeacherId",
                table: "StudentCorrections",
                column: "TeacherId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudentCorrections");
        }
    }
}
