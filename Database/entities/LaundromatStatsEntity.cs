using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BlazorTest.Database
{
    [Table("laundromat_stats")]
    public class LaundromatStats
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("Laundromat")]
        public string LaundromatId { get; set; }

        [MaxLength(100)]
        public string LaundromatName { get; set; }

        [Required]
        public StatsPeriodType PeriodType { get; set; }

        [Required]
        public string PeriodKey { get; set; } // Format: "2025-04" for month, "2025-Q2" for quarter, etc.

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        [Required]
        public decimal TotalRevenue { get; set; }

        [Required]
        public int TotalTransactions { get; set; }

        [Required]
        public int WashingMachineTransactions { get; set; }

        [Required]
        public int DryerTransactions { get; set; }

        [Required]
        public DateTime CalculatedAt { get; set; }

        // Navigation property
        public virtual Laundromat Laundromat { get; set; }

        // Calculated properties that don't need to be stored
        [NotMapped]
        public decimal WashingMachinePercentage => TotalTransactions > 0 ?
            (decimal)WashingMachineTransactions / TotalTransactions * 100 : 0;

        [NotMapped]
        public decimal DryerPercentage => TotalTransactions > 0 ?
            (decimal)DryerTransactions / TotalTransactions * 100 : 0;
    }

    public enum StatsPeriodType
    {
        Month = 1,      // Rolling 30-day period from current date
        HalfYear = 2,     // Rolling 180-day (6 month) period from current date
        Year = 3,        // Rolling 365-day period from current date
        Quarter = 4,         // Calendar quarter
    }
}