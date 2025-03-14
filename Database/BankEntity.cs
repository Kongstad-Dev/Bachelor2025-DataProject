﻿using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Bach2025_nortec.Database;

public class BankEntity
{
    [Key]
    public int bId { get; set; }
    public string? name { get; set; }

    // Navigation property for laundromats belonging to this bank
    public virtual ICollection<Laundromat> Laundromats { get; set; } = new List<Laundromat>();
}