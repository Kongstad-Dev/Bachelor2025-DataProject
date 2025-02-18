namespace Bach2025_nortecnortec.Database;
using System.ComponentModel.DataAnnotations;

public class DataEntity
{
    [Key]
    public int kId { get; set; }
    public int externalId { get; set; }
    public int transactionsType { get; set; }
    public int unittype { get; set; }
    public string unitName { get; set; }
    public int program { get; set; }
    public int prewash { get; set; }
    public int programtype { get; set; }
    public int temperature { get; set; }
    public int spin { get; set; }
    public int soapBrand  { get; set; }
    public int dirty { get; set; }
    public int rinse { get; set; }
    public int minuts { get; set; }
    public int seconds { get; set; }
    public int amount { get; set; }
    public string currency { get; set; }
    public string user { get; set; }
    public string debug { get; set; }
}