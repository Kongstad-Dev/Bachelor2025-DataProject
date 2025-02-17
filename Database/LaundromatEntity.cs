using System.ComponentModel.DataAnnotations;

public class Laundromat
{
    [Key]
    public int kId { get; set; }
    public int externalId { get; set; }
    public string? bank { get; set; }
    public string? name { get; set; }
    public string? zip { get; set; }
    public float longitude { get; set; }
    public float latitude { get; set; }
}