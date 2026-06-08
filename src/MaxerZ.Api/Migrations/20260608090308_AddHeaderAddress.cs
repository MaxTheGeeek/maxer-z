using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaxerZ.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddHeaderAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HeaderAddress",
                table: "CoverLetters",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HeaderAddress",
                table: "CoverLetters");
        }
    }
}
