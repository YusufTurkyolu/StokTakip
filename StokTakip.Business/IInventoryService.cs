using StokTakip.Data.Models;

namespace StokTakip.Business;

public interface IInventoryService
{
    Task<List<InventoryItem>> GetAllItemsAsync();
    Task<List<Department>> GetAllDepartmentsAsync();
    Task<Department> AddDepartmentAsync(string name);
    Task<bool> DeleteDepartmentAsync(int departmentId);
    Task<InventoryItem> AddItemAsync(string itemName, int initialStock, int minThreshold);
    Task<StockOperationResult> StockInAsync(int itemId, int quantity);
    Task<StockOperationResult> StockOutAsync(int itemId, int departmentId, int quantity);
    Task<List<InventoryItem>> GetLowStockItemsAsync();
    Task<List<Transaction>> GetTransactionsByDepartmentAndDateAsync(int departmentId, DateTime startDate, DateTime endDate);
    Task<List<Transaction>> GetTransactionsByItemAndDateAsync(int itemId, DateTime startDate, DateTime endDate);
    Task<bool> UpdateTransactionAsync(int transactionId, int newQuantity);
    Task<bool> DeleteItemAsync(int itemId);
    Task<int> DeleteTransactionsAsync(IEnumerable<int> transactionIds);
}

// Result Pattern: exception fırlatmak yerine kontrollü başarı/hata nesnesi döner.
// UI katmanı try-catch yerine .Success property'e bakarak karar verir.
public record StockOperationResult(bool Success, string? ErrorMessage = null);
