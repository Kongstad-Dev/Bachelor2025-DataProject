using Bunit;
using Xunit;
using BlazorTest.Components;
using System.Collections.Generic;
using System.Linq;

namespace BlazorTest_Test.Tests.Chart
{
    public class ChartComponentRenderTests : TestContext
    {
        private static readonly string[] LabelsAB = { "A", "B" };
        private static readonly decimal[] SingleValuesAB = { 1.0m, 2.0m };
        private static readonly string[] LabelsJanFeb = { "Jan", "Feb" };
        private static readonly decimal[] SingleValuesJanFeb = { 100, 200 };
        private static readonly string[] DatasetLabels = { "Data1" };
        private static readonly string[] BackgroundColors = { "#fff", "#000" };

        [Fact]
        public void ChartComponent_RendersCanvasWithCorrectId()
        {
            // Arrange
            var canvasId = "myChart";
            JSInterop.SetupVoid("renderChart", _ => true); // Mock JS interop call

            // Act
            var cut = RenderComponent<ChartComponent>(parameters => parameters
                .Add(p => p.CanvasId, canvasId)
                .Add(p => p.Labels, LabelsAB)
                .Add(p => p.SingleValues, SingleValuesAB)
                .Add(p => p.Title, "Test Chart")
            );

            // Assert
            cut.MarkupMatches($"""
                <canvas id="{canvasId}" style="width: 100%;"></canvas>
            """);
        }

        [Fact]
        public void ChartComponent_TriggersJavaScriptInteropOnRender()
        {
            // Arrange
            JSInterop.SetupVoid("renderChart", _ => true);

            // Act
            var cut = RenderComponent<ChartComponent>(parameters => parameters
                .Add(p => p.CanvasId, "chartCanvas")
                .Add(p => p.Labels, LabelsJanFeb)
                .Add(p => p.SingleValues, SingleValuesJanFeb)
                .Add(p => p.Type, "bar")
                .Add(p => p.Title, "Test Chart")
                .Add(p => p.DatasetLabels, DatasetLabels)
                .Add(p => p.BackgroundColors, BackgroundColors));

            // Assert
            cut.WaitForAssertion(() => JSInterop.VerifyInvoke("renderChart"));
        }

        [Fact]
        public void ChartComponent_Updates_WhenDataChanges()
        {
            using var ctx = new TestContext();
            var initialData = new List<ChartDataPoint> { new ChartDataPoint("January", 100) };
            var updatedData = new List<ChartDataPoint> { new ChartDataPoint("February", 150) };

            var component = ctx.RenderComponent<ChartComponent>(parameters =>
                parameters.Add(p => p.Labels, initialData.Select(d => d.Label).ToArray())
                          .Add(p => p.SingleValues, initialData.Select(d => d.Value).ToArray()));

            component.SetParametersAndRender(parameters =>
                parameters.Add(p => p.Labels, updatedData.Select(d => d.Label).ToArray())
                          .Add(p => p.SingleValues, updatedData.Select(d => d.Value).ToArray()));
        }
            
        // Add this class definition if ChartDataPoint does not exist elsewhere
        public class ChartDataPoint
        {
            public string Label { get; set; }
            public decimal Value { get; set; }
    
            public ChartDataPoint(string label, decimal value)
            {
                Label = label;
                Value = value;
            }
        }
    }
}
