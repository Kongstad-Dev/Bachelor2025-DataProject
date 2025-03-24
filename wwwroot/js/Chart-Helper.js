window.renderChart = function (chartData) {
    const ctx = document.getElementById(chartData.canvasId).getContext("2d");

    const defaultColors = [
        "#3366CC", "#DC3912", "#FF9900", "#109618", "#990099",
        "#3B3EAC", "#0099C6", "#DD4477", "#66AA00", "#B82E2E",
        "#316395", "#994499", "#22AA99", "#AAAA11", "#6633CC"
    ];

    const colors = chartData.values.map((_, i) => defaultColors[i % defaultColors.length]);

    if (window.myCharts === undefined) window.myCharts = {};
    if (window.myCharts[chartData.canvasId]) {
        window.myCharts[chartData.canvasId].destroy();
    }

    window.myCharts[chartData.canvasId] = new Chart(ctx, {
        type: "bar",
        data: {
            labels: chartData.labels,
            datasets: [{
                label: chartData.title,
                data: chartData.values,
                backgroundColor: colors,
                borderColor: colors,
                borderWidth: 1
            }]
        },
        options: {
            responsive: true,
            scales: {
                y: {
                    beginAtZero: true
                }
            }
        }
    });
};
