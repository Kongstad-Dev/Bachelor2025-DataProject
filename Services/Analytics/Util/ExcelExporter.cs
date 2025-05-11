using System;
using System.Collections.Generic;
using System.IO;
using OfficeOpenXml;
using BlazorTest.Database;

namespace BlazorTest.Services.Analytics.Util
{
    public static class ExcelExporter
    {
        // Define the same dryer unit types as used in KeyValueAnalysisService for consistency
        private static readonly int[] DryerUnitTypes = new[] { 1, 18, 5, 10, 14, 19, 27, 29, 41 };

        // Static constructor to set the license - this runs once when the class is first used
        static ExcelExporter()
        {
            // Set the license for EPPlus version 8+
            // For non-commercial projects
            OfficeOpenXml.ExcelPackage.License.SetNonCommercialOrganization("My Noncommercial organization");
        }

        public static byte[] ExportMachineDetailsToExcel(Dictionary<string, List<MachineDetailRow>> laundromatMachines)
        {
            if (laundromatMachines == null || laundromatMachines.Count == 0)
                return new byte[0];

            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Data");
                
                // Configure workbook for outlining/grouping
                worksheet.OutLineApplyStyle = true;
                
                // Set headers with Danish characters
                worksheet.Cells[1, 1].Value = "Rækkemærkater";
                worksheet.Cells[1, 2].Value = "VM Starter";
                worksheet.Cells[1, 3].Value = "TT Starter";
                worksheet.Cells[1, 4].Value = "Omsætning";
                worksheet.Cells[1, 5].Value = "VM StartPris";
                worksheet.Cells[1, 6].Value = "TT StartPris";
                
                // Format header row
                using (var range = worksheet.Cells[1, 1, 1, 6])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                }
                
                int row = 2;
                
                // Variables to track grand totals - using precise calculation methods
                int grandTotalWasherStarts = 0;
                int grandTotalDryerStarts = 0;
                decimal grandTotalRevenue = 0;
                decimal grandTotalWasherRevenue = 0;
                decimal grandTotalDryerRevenue = 0;
                
                foreach (var laundromat in laundromatMachines)
                {
                    // Calculate summary data for laundromat using exact same logic as KeyValueAnalysisService
                    var washerStarts = laundromat.Value.Sum(m => m.IsWasher ? m.Starts : 0);
                    var dryerStarts = laundromat.Value.Sum(m => !m.IsWasher ? m.Starts : 0);
                    var totalRevenue = laundromat.Value.Sum(m => m.Revenue);
                    var washerRevenue = laundromat.Value.Where(m => m.IsWasher).Sum(m => m.Revenue);
                    var dryerRevenue = laundromat.Value.Where(m => !m.IsWasher).Sum(m => m.Revenue);
                    
                    // Update grand totals
                    grandTotalWasherStarts += washerStarts;
                    grandTotalDryerStarts += dryerStarts;
                    grandTotalRevenue += totalRevenue;
                    grandTotalWasherRevenue += washerRevenue;
                    grandTotalDryerRevenue += dryerRevenue;
                    
                    // Calculate average prices (avoid divide by zero)
                    decimal washerAvgPrice = washerStarts > 0 
                        ? washerRevenue / washerStarts 
                        : 0;
                    
                    decimal dryerAvgPrice = dryerStarts > 0 
                        ? dryerRevenue / dryerStarts 
                        : 0;
                    
                    // Store the laundromat summary row number
                    int laundromatRow = row;
                    
                    // Laundromat summary row
                    worksheet.Cells[row, 1].Value = $"{laundromat.Key}";
                    worksheet.Cells[row, 2].Value = washerStarts;
                    worksheet.Cells[row, 3].Value = dryerStarts;
                    worksheet.Cells[row, 4].Value = totalRevenue;
                    worksheet.Cells[row, 5].Value = washerAvgPrice;
                    worksheet.Cells[row, 6].Value = dryerAvgPrice;
                    
                    // Format the summary row with color
                    using (var range = worksheet.Cells[row, 1, row, 6])
                    {
                        range.Style.Font.Bold = true;
                        range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                    }
                    
                    // Configure number formats
                    worksheet.Cells[row, 4].Style.Numberformat.Format = "#,##0.00";
                    worksheet.Cells[row, 5].Style.Numberformat.Format = "#,##0.00";
                    worksheet.Cells[row, 6].Style.Numberformat.Format = "#,##0.00";
                    
                    row++;
                    
                    // Store the first detail row
                    int firstDetailRow = row;
                    
                    // Add individual machine rows
                    foreach (var machine in laundromat.Value.OrderBy(m => m.MachineName))
                    {
                        // Set outline level for this row (makes it part of a collapsible group)
                        worksheet.Row(row).OutlineLevel = 1;
                        
                        worksheet.Cells[row, 1].Value = $"  {machine.MachineName}";
                        
                        // For washers: put count in VM column, leave TT empty
                        if (machine.IsWasher)
                        {
                            worksheet.Cells[row, 2].Value = machine.Starts;
                            worksheet.Cells[row, 5].Value = machine.PricePerStart;
                            worksheet.Cells[row, 5].Style.Numberformat.Format = "#,##0.00";
                        }
                        else
                        {
                            // For dryers: put count in TT column, leave VM empty
                            worksheet.Cells[row, 3].Value = machine.Starts;
                            worksheet.Cells[row, 6].Value = machine.PricePerStart;
                            worksheet.Cells[row, 6].Style.Numberformat.Format = "#,##0.00";
                        }
                        
                        // Revenue is always shown
                        worksheet.Cells[row, 4].Value = machine.Revenue;
                        worksheet.Cells[row, 4].Style.Numberformat.Format = "#,##0.00";
                        
                        row++;
                    }
                    
                    // Define the group (from first detail row to the last detail row)
                    int lastDetailRow = row - 1;
                    
                    // If there are detail rows, create a collapsible group
                    if (lastDetailRow >= firstDetailRow)
                    {
                        worksheet.Row(laundromatRow).Collapsed = false;
                        worksheet.Row(lastDetailRow).Collapsed = true;
                    }
                }
                
                // Calculate overall average prices using the same formula as KeyValueAnalysisService
                decimal grandTotalWasherAvgPrice = grandTotalWasherStarts > 0
                    ? grandTotalWasherRevenue / grandTotalWasherStarts
                    : 0;
                    
                decimal grandTotalDryerAvgPrice = grandTotalDryerStarts > 0
                    ? grandTotalDryerRevenue / grandTotalDryerStarts
                    : 0;
                
                // Add the grand total row with values calculated in the same way as KeyValueAnalysisService
                worksheet.Cells[row, 1].Value = "TOTAL";
                worksheet.Cells[row, 2].Value = grandTotalWasherStarts;
                worksheet.Cells[row, 3].Value = grandTotalDryerStarts;
                worksheet.Cells[row, 4].Value = grandTotalRevenue;
                worksheet.Cells[row, 5].Value = grandTotalWasherAvgPrice;
                worksheet.Cells[row, 6].Value = grandTotalDryerAvgPrice;
                
                // Format the grand total row to stand out
                using (var range = worksheet.Cells[row, 1, row, 6])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Font.Size += 1; // Make it slightly larger
                    range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);
                }
                
                worksheet.Cells[row, 4].Style.Numberformat.Format = "#,##0.00";
                worksheet.Cells[row, 5].Style.Numberformat.Format = "#,##0.00";
                worksheet.Cells[row, 6].Style.Numberformat.Format = "#,##0.00";
                
                // Configure the summary column settings (shows the +/- buttons)
                worksheet.OutLineSummaryRight = false;
                worksheet.OutLineSummaryBelow = false;
                
                // Auto-fit columns for better readability
                worksheet.Cells.AutoFitColumns();
                
                return package.GetAsByteArray();
            }
        }
        
        public static byte[] ExportChartDataToExcel(List<ChartDataPoint> data)
        {
            if (data == null || data.Count == 0)
                return new byte[0];
                
            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Chart Data");
                
                // Set headers
                worksheet.Cells[1, 1].Value = "Label";
                worksheet.Cells[1, 2].Value = "Value";
                
                // Format header row
                using (var range = worksheet.Cells[1, 1, 1, 2])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                }
                
                // Add data rows
                for (int i = 0; i < data.Count; i++)
                {
                    worksheet.Cells[i + 2, 1].Value = data[i].Label;
                    worksheet.Cells[i + 2, 2].Value = data[i].Value;
                }
                
                // Configure number format for values
                worksheet.Cells[2, 2, data.Count + 1, 2].Style.Numberformat.Format = "#,##0.00";
                
                // Auto-fit columns
                worksheet.Cells.AutoFitColumns();
                
                return package.GetAsByteArray();
            }
        }
    }
}