using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace StokTakip.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSeedData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 5);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
        }
    }
}
