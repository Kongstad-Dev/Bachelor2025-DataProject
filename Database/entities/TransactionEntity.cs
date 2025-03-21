using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bach2025_nortec.Database;

public class TransactionEntity
{
    [Key]
    public string? kId { get; set; }

    // Date of transaction
    public DateTime date { get; set; }

    public int transactionType { get; set; }

    public int unitType { get; set; }

    public string? unitName { get; set; }

    public int program { get; set; }

    public int prewash { get; set; }

    public int programType { get; set; }

    public int temperature { get; set; }

    public int spin { get; set; }

    public int soap { get; set; }

    public int soapBrand { get; set; }

    public int dirty { get; set; }

    public int rinse { get; set; }

    // Misspelled because of the api
    // Changing it would break the DataUpdateController
    public int minuts { get; set; }

    public int seconds { get; set; }

    public int amount { get; set; }

    public string? currency { get; set; }

    public string? user { get; set; }

    public string? debug { get; set; }

    // Foreign key to Laundromat
    [ForeignKey("Laundromat")]
    public string? LaundromatId { get; set; }

    public virtual Laundromat? Laundromat { get; set; }
}
