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
}