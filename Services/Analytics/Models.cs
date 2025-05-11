using System;
using System.Collections.Generic;

namespace BlazorTest.Services.Analytics
{
    public class ChartDataPoint
    {
        public string Label { get; set; }
        public decimal Value { get; set; }
    }

    public class SoapResults
    {
        public decimal soap1 { get; set; }
        public decimal soap2 { get; set; }
        public decimal soap3 { get; set; }
    }

    public class TimeSeriesInfo
    {
        public List<ChartDataPoint> DataPoints { get; set; }
        public string Interval { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    public class MachineDetailRow
    {
        public string MachineName { get; set; } = string.Empty;
        public int Starts { get; set; }
        public decimal Revenue { get; set; }
        public bool IsWasher { get; set; }
        public decimal PricePerStart { get; set; }
    }
}
