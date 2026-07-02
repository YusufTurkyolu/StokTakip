using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace StokTakip.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Departments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InventoryItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ItemName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CurrentStock = table.Column<int>(type: "INTEGER", nullable: false),
                    MinimumThreshold = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Transactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    DepartmentId = table.Column<int>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TransactionType = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Transactions_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Transactions_InventoryItems_ItemId",
                        column: x => x.ItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Departments",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "İdari İşler" },
                    { 2, "Çevre Odası" },
                    { 3, "Teknik İşler" },
                    { 4, "Mali Hizmetler" },
                    { 5, "Yazı İşleri" }
                });

            migrationBuilder.InsertData(
                table: "InventoryItems",
                columns: new[] { "Id", "CurrentStock", "ItemName", "MinimumThreshold" },
                values: new object[,]
                {
                    { 1, 50, "A4 Kağıt (Koli)", 10 },
                    { 2, 8, "Tükenmez Kalem", 20 },
                    { 3, 3, "Su Şişesi (Damacana)", 5 },
                    { 4, 25, "Zımba Teli", 10 },
                    { 5, 2, "Toner (Siyah)", 3 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_ItemName",
                table: "InventoryItems",
                column: "ItemName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_DepartmentId",
                table: "Transactions",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_ItemId",
                table: "Transactions",
                column: "ItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Transactions");

            migrationBuilder.DropTable(
                name: "Departments");

            migrationBuilder.DropTable(
                name: "InventoryItems");
        }
    }
}
