using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaxerZ.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddResumeRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Resumes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    Experience = table.Column<string>(type: "TEXT", nullable: false),
                    Education = table.Column<string>(type: "TEXT", nullable: false),
                    Skills = table.Column<string>(type: "TEXT", nullable: false),
                    Projects = table.Column<string>(type: "TEXT", nullable: false),
                    Language = table.Column<string>(type: "TEXT", nullable: false),
                    SelectedTemplate = table.Column<string>(type: "TEXT", nullable: false),
                    HeaderAddress = table.Column<string>(type: "TEXT", nullable: false),
                    PdfPath = table.Column<string>(type: "TEXT", nullable: false),
                    SyncedToMcp = table.Column<bool>(type: "INTEGER", nullable: false),
                    UsedProvider = table.Column<string>(type: "TEXT", nullable: false),
                    UsedModel = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Resumes", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Resumes");
        }
    }
}
