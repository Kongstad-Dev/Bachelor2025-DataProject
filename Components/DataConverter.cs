using BlazorTest.Services.Analytics;

namespace BlazorTest.Components;

public static class DataConverter
{
    public static List<ChartDataPoint> ToChartDataPoints(string[] labels, decimal[] values)
    {
        if (labels.Length != values.Length)
            throw new ArgumentException("Labels and values must have the same length.");

        return labels.Zip(values, (label, value) => new ChartDataPoint
        {
            Label = label,
            Value = value
        }).ToList();
    }
}
