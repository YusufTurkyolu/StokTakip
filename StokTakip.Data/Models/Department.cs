using System.ComponentModel.DataAnnotations;

namespace StokTakip.Data.Models;

public class Department
{
    public int Id { get; set; }

    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
