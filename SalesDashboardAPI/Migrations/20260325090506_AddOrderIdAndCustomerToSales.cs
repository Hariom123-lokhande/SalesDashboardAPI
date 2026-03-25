using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesDashboardAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderIdAndCustomerToSales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Customer",
                table: "Sales",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OrderId",
                table: "Sales",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Customer",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "OrderId",
                table: "Sales");
        }
    }
}
