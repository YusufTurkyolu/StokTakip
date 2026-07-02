using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StokTakip.Data.Migrations
{
    /// <inheritdoc />
    public partial class StockInWithoutDepartment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Departments_DepartmentId",
                table: "Transactions");

            migrationBuilder.AlterColumn<int>(
                name: "DepartmentId",
                table: "Transactions",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Departments_DepartmentId",
                table: "Transactions",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Departments_DepartmentId",
                table: "Transactions");

            migrationBuilder.AlterColumn<int>(
                name: "DepartmentId",
                table: "Transactions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Departments_DepartmentId",
                table: "Transactions",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
