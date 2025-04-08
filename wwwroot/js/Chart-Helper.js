let chartInstances = {};

window.renderChart = function (config) {
    // Validate required parameters
    if (!config.canvasId) {
        console.error("Missing required 'canvasId' parameter");
        return;
    }

    const canvas = document.getElementById(config.canvasId);
    if (!canvas) {
        console.error(`Canvas element with ID '${config.canvasId}' not found`);
        return;
    }

    const ctx = canvas.getContext('2d');

    // Check if the chartInstances object exists
    if (!window.chartInstances) {
        window.chartInstances = {};
    }

    // Destroy the existing chart instance if it exists
    if (window.chartInstances[config.canvasId]) {
        window.chartInstances[config.canvasId].destroy();
        delete window.chartInstances[config.canvasId];
    }

    // Check if config.values is valid
    if (!config.values || !Array.isArray(config.values) || config.values.length === 0) {
        console.error(`Missing or empty 'values' parameter for chart '${config.canvasId}'`);
        return;
    }

    // Check if first array in values is valid
    if (!config.values[0] || !Array.isArray(config.values[0]) || config.values[0].length === 0) {
        console.error(`Invalid data structure in 'values' for chart '${config.canvasId}'`);
        return;
    }

    // Set font sizes (with defaults)
    const fontSize = config.fontSize || 10;
    const titleFontSize = config.titleFontSize || (fontSize + 4);
    const axisLabelSize = config.axisLabelSize || fontSize;

    let datasets = [];

    // Check if this is likely a SingleValues chart (one row in the 2D array)
    const isSingleValueChart = config.values.length === 1 && !config.stacked;

    if (isSingleValueChart) {
        // Handle SingleValues chart (simple dataset without transposition)
        datasets = [{
            label: config.title || 'Dataset',
            data: config.values[0],
            backgroundColor: config.backgroundColors && config.backgroundColors.length > 0
                ? config.backgroundColors[0]
                : generateColors(1, config.type)[0],
            borderColor: config.type === 'line'
                ? (config.backgroundColors && config.backgroundColors.length > 0
                    ? config.backgroundColors[0]
                    : generateBorderColors(1)[0])
                : generateBorderColors(1)[0],
            borderWidth: config.type === 'line' ? 2 : 1,
            fill: config.type === 'line' ? false : true
        }];
    } else {
        // Handle MultiValues chart (stacked datasets with transposition)
        datasets = config.values[0].map((_, colIndex) => ({
            label: config.datasetLabels && config.datasetLabels.length > colIndex
                ? config.datasetLabels[colIndex]
                : `Dataset ${colIndex + 1}`,
            data: config.values.map(row => row[colIndex]), // Extract the column for each dataset
            backgroundColor: (config.backgroundColors && config.backgroundColors.length > colIndex)
                ? config.backgroundColors[colIndex]
                : generateColors(1, config.type)[0],
            borderColor: generateBorderColors(1)[0],
            borderWidth: 1,
            stack: config.stacked ? 'stack1' : undefined // Only use stack if stacked is true
        }));
    }

    // Create a new chart instance and store it in the chartInstances object
    window.chartInstances[config.canvasId] = new Chart(ctx, {
        type: config.type || "bar",
        data: {
            labels: config.labels || [],
            datasets: datasets
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            animation: {
                duration: 0
            },
            plugins: {
                legend: {
                    display: false, // Only show legend for stacked charts
                    position: 'top',
                    labels: {
                        font: {
                            size: fontSize // Set legend font size
                        },
                        padding: 15
                    }
                },
                title: {
                    display: true,
                    text: config.title || "",
                    font: {
                        size: titleFontSize,
                        weight: 'bold'
                    },
                    padding: {
                        top: 10,
                        bottom: 15
                    }
                }
            },
            scales: {
                x: {
                    stacked: config.stacked, // Enable stacking for x-axis
                    ticks: {
                        font: {
                            size: axisLabelSize // Set x-axis label font size
                        }
                    }
                },
                y: {
                    stacked: config.stacked, // Enable stacking for y-axis
                    ticks: {
                        font: {
                            size: axisLabelSize // Set y-axis label font size
                        }
                    }
                }
            }
        }
    });
};

function generateColors(count, type) {
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
        'rgba(255, 105, 180, 1)',  // vibrant pastel pink
        'rgba(255, 160, 122, 1)',  // soft coral
        'rgba(255, 220, 105, 1)',  // warm pastel yellow
        'rgba(144, 238, 144, 1)',  // light lime green
        'rgba(135, 206, 250, 1)',  // sky blue
        'rgba(173, 216, 230, 1)',  // pastel cyan-blue
        'rgba(216, 191, 216, 1)',  // rich lavender
        'rgba(255, 182, 193, 1)',  // bright blush
        'rgba(255, 204, 153, 1)',  // peachy orange
        'rgba(186, 85, 211, 1)'    // punchy orchid
    ];
    return Array.from({ length: count }, (_, i) => baseColors[i % baseColors.length]);
}