namespace BlazorTest.Services.Analytics.Util;
using System.Collections.Generic;
using System.Text;
using BlazorTest.Services.Analytics;

public static class CsvExporter
{
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

}
