using System.ComponentModel.DataAnnotations;

namespace Bach2025_nortec.Database;

public class BankEntity
{
    [Key]
    public int BId{ get; set; }
    public string? name { get; set; }
}