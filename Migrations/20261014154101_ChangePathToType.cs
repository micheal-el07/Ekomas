using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ekomas.Migrations
{
    /// <inheritdoc />
    public partial class ChangePathToType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ImageFilePath",
                table: "Products",
                newName: "ImageContentType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ImageContentType",
                table: "Products",
                newName: "ImageFilePath");
        }
    }
}
