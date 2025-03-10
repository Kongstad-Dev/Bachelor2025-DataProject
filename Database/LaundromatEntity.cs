using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Bach2025_nortec.Database;

public class Laundromat
{
    [Key]
    public string? kId { get; set; }
    public string? externalId { get; set; }
    public string? bank { get; set; }
    public int bId { get; set; }
    public string? name { get; set; }
    public string? zip { get; set; }
    public float longitude { get; set; }
    public float latitude { get; set; }

    [ForeignKey("bId")]
    public BankEntity? Bank { get; set; }
}