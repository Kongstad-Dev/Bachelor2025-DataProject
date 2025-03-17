using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Bach2025_nortec.Database;

namespace Bach2025_nortec.Database;

public class Laundromat
{
    [Key]
    public string? kId { get; set; }
    public string? externalId { get; set; }

    [JsonPropertyName("bankName")]
    public string? bank { get; set; }
    public int bId { get; set; }
    public string? name { get; set; }
    public string? zip { get; set; }
    public float longitude { get; set; }
    public float latitude { get; set; }
    public DateTime? lastFetchDate { get; set; }

    [ForeignKey("bId")]
    public BankEntity? Bank { get; set; }

    // Navigation property for transactions
    public virtual ICollection<TransactionEntity>? Transactions { get; set; } =
        new List<TransactionEntity>();
}
