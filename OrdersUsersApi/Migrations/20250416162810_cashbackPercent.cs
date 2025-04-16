using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrdersUsersApi.Migrations
{
    /// <inheritdoc />
    public partial class cashbackPercent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CashbackPercent",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CashbackPercent",
                table: "Users");
        }
    }
}
