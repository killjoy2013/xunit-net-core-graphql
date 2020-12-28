using Microsoft.EntityFrameworkCore.Migrations;

namespace GraphQL.WebApi.Migrations
{
    public partial class UniqNames : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_country_name",
                schema: "business",
                table: "country",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_city_name",
                schema: "business",
                table: "city",
                column: "name",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_country_name",
                schema: "business",
                table: "country");

            migrationBuilder.DropIndex(
                name: "IX_city_name",
                schema: "business",
                table: "city");
        }
    }
}
