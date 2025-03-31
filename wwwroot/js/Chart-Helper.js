let chartInstances = {};

window.renderChart = function (config) {
    const ctx = document.getElementById(config.canvasId).getContext('2d');

    if (!window.chartInstances) {
        window.chartInstances = {};
    }

    if (window.chartInstances[config.canvasId]) {
        window.chartInstances[config.canvasId].destroy();
    }

    window.chartInstances[config.canvasId] = new Chart(ctx, {
        type: config.type || "bar",
        data: {
            labels: config.labels,
            datasets: [{
                label: config.title,
                data: config.values,
                fill: config.type === "line" ? { target: 'origin', value: false } : config.type === "radar",


                tension: 0.3,
                backgroundColor: config.type === "line"
                    ? 'rgba(0, 0, 0, 0)' // single transparent color for whole dataset
                    : generateColors(config.values.length, config.type),
                borderColor:  config.type === "line"
                    ? 'rgba(75, 192, 192, 1)'  // or any clear color
                    : generateBorderColors(config.values.length, config.type),
                borderWidth: 1
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: {
                    display: config.type !== "bar" && config.type !== "line",
                    position: 'top'
                },
                title: {
                    display: true,
                    text: config.title
                }
            },
            scales: config.type === "bar" || config.type === "line"
                ? { y: { beginAtZero: true } }
                : {}
        }
    });
};

function generateColors(count, type) {
    if (type === "line") {
        return Array(count).fill('rgba(0, 0, 0, 0)');
    }

    const baseColors = [
        'rgba(255, 105, 180, 0.7)',  // vibrant pastel pink
        'rgba(255, 160, 122, 0.7)',  // soft coral
        'rgba(255, 220, 105, 0.7)',  // warm pastel yellow
        'rgba(144, 238, 144, 0.7)',  // light lime green
        'rgba(135, 206, 250, 0.7)',  // sky blue
        'rgba(173, 216, 230, 0.7)',  // pastel cyan-blue
        'rgba(216, 191, 216, 0.7)',  // rich lavender
        'rgba(255, 182, 193, 0.7)',  // bright blush
        'rgba(255, 204, 153, 0.7)',  // peachy orange
        'rgba(186, 85, 211, 0.7)'    // punchy orchid
    ];

    return Array.from({ length: count }, (_, i) => baseColors[i % baseColors.length]);
}

function generateBorderColors(count) {
    const baseColors = [
        'rgba(255, 105, 180, 0.7)',  // vibrant pastel pink
        'rgba(255, 160, 122, 0.7)',  // soft coral
        'rgba(255, 220, 105, 0.7)',  // warm pastel yellow
        'rgba(144, 238, 144, 0.7)',  // light lime green
        'rgba(135, 206, 250, 0.7)',  // sky blue
        'rgba(173, 216, 230, 0.7)',  // pastel cyan-blue
        'rgba(216, 191, 216, 0.7)',  // rich lavender
        'rgba(255, 182, 193, 0.7)',  // bright blush
        'rgba(255, 204, 153, 0.7)',  // peachy orange
        'rgba(186, 85, 211, 0.7)'    // punchy orchid
    ];
    return Array.from({ length: count }, (_, i) => baseColors[i % baseColors.length]);
}

