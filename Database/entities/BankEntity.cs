using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using BlazorTest.Database.entities;

namespace BlazorTest.Database.entities;

public class BankEntity
{
    [Key]
    public int bankId { get; set; }
    public string? name { get; set; }

    // Navigation property for laundromats belonging to this bank
    public virtual ICollection<Laundromat> Laundromats { get; set; } = new List<Laundromat>();
}