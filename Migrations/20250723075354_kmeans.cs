using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TodoApi.Migrations
{
    /// <inheritdoc />
    public partial class kmeans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClusterID",
                table: "FileChunks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClusterMethod",
                table: "FileChunks",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClusterID",
                table: "FileChunks");

            migrationBuilder.DropColumn(
                name: "ClusterMethod",
                table: "FileChunks");
        }
    }
}
