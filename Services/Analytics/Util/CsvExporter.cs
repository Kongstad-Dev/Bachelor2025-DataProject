namespace BlazorTest.Services.Analytics.Util;
using System.Collections.Generic;
using System.Text;
using BlazorTest.Services.Analytics;
using BlazorTest.Database;
using System.Linq;

public static class CsvExporter
{
    // Add a UTF-8 BOM to ensure Danish characters display correctly
    private static readonly byte[] Utf8Bom = new byte[] { 0xEF, 0xBB, 0xBF };
    
    public static string ExportChartDataToCsv(List<ChartDataPoint> data)
    {
        if (data == null || data.Count == 0)
            return string.Empty;

        var csv = new StringBuilder();
        csv.AppendLine("\"Label\",\"Value\"");

        foreach (var point in data)
        {
            csv.AppendLine($"\"{point.Label}\",\"{point.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}\"");
        }

        return csv.ToString();
    }
    
    public static string ExportMachineDetailsPerLaundromatToCsv(
        Dictionary<string, List<MachineDetailRow>> laundromatMachines)
    {
        if (laundromatMachines == null || !laundromatMachines.Any())
            return string.Empty;
            
        var csv = new StringBuilder();
        
        // Header row with correct Danish characters
        csv.AppendLine("\"Rækkemærkater\",\"VM Starter\",\"TT Starter\",\"Omsætning\",\"VM StartPris\",\"TT StartPris\"");
        
        bool isFirst = true;
        
        // For each laundromat
        foreach (var laundromat in laundromatMachines)
        {
            if (!isFirst)
            {
                // Add separator rows between laundromats
                csv.AppendLine(",,,,,");
                csv.AppendLine(",,,,,");
            }
            
            isFirst = false;
            
            // Add laundromat row with total counts
            var washerStarts = laundromat.Value.Sum(m => m.IsWasher ? m.Starts : 0);
            var dryerStarts = laundromat.Value.Sum(m => !m.IsWasher ? m.Starts : 0);
            var totalRevenue = laundromat.Value.Sum(m => m.Revenue);
            
            // Calculate average prices (avoid divide by zero)
            decimal washerAvgPrice = washerStarts > 0 
                ? laundromat.Value.Where(m => m.IsWasher).Sum(m => m.Revenue) / washerStarts 
                : 0;
                
            decimal dryerAvgPrice = dryerStarts > 0 
                ? laundromat.Value.Where(m => !m.IsWasher).Sum(m => m.Revenue) / dryerStarts 
                : 0;
                
            // Laundromat summary row
            csv.AppendLine($"\"► {laundromat.Key}\",\"{washerStarts}\",\"{dryerStarts}\",\"{totalRevenue:0.###}\",\"{washerAvgPrice:0.00}\",\"{dryerAvgPrice:0.00}\"");
            
            // Add individual machine rows
            foreach (var machine in laundromat.Value.OrderBy(m => m.MachineName))
            {
                // For washers: put count in VM column, leave TT empty
                // For dryers: put count in TT column, leave VM empty
                string washerCount = machine.IsWasher ? machine.Starts.ToString() : "";
                string dryerCount = !machine.IsWasher ? machine.Starts.ToString() : "";
                
                // Only show price in correct column based on machine type
                string washerPrice = machine.IsWasher ? machine.PricePerStart.ToString("0.00") : "";
                string dryerPrice = !machine.IsWasher ? machine.PricePerStart.ToString("0.00") : "";
                
                csv.AppendLine($"\"  {machine.MachineName}\",\"{washerCount}\",\"{dryerCount}\",\"{machine.Revenue:0.###}\",\"{washerPrice}\",\"{dryerPrice}\"");
            }
        }
        
        return csv.ToString();
    }

        public static byte[] ExportMachineDetailsPerLaundromatToCsvBytes(
        Dictionary<string, List<MachineDetailRow>> laundromatMachines)
        {
            string csvContent = ExportMachineDetailsPerLaundromatToCsv(laundromatMachines);
            
            // Convert string to UTF-8 bytes
            byte[] contentBytes = Encoding.UTF8.GetBytes(csvContent);
            
            // Combine BOM and content
            byte[] result = new byte[Utf8Bom.Length + contentBytes.Length];
            Utf8Bom.CopyTo(result, 0);
            contentBytes.CopyTo(result, Utf8Bom.Length);
            
            return result;
        }
}