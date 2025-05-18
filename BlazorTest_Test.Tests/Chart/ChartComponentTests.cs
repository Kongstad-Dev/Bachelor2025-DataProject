using Bunit;
using BlazorTest.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Xunit;

namespace BlazorTest_Test.Tests.Chart
{
    public class ChartComponentRenderTests : TestContext
    {
        [Fact]
        public void ChartComponent_RendersCanvasWithCorrectId()
        {
            // Arrange
            var canvasId = "myChart";
            JSInterop.SetupVoid("renderChart", _ => true); // Mock JS interop call

            // Act
            var cut = RenderComponent<ChartComponent>(parameters => parameters
                .Add(p => p.CanvasId, canvasId)
                .Add(p => p.Labels, new[] { "A", "B" })
                .Add(p => p.SingleValues, new decimal[] { 1.0m, 2.0m })
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
                .Add(p => p.Labels, new[] { "Jan", "Feb" })
                .Add(p => p.SingleValues, new decimal[] { 100, 200 })
                .Add(p => p.Type, "bar")
            );

            // Assert
            JSInterop.VerifyInvoke("renderChart");
        }
    }
}
