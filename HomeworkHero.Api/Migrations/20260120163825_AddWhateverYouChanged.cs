using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeworkHero.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWhateverYouChanged : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Paramiters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Paramiters", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Paramiters_Name",
                table: "Paramiters",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Paramiters");
        }
    }
}
