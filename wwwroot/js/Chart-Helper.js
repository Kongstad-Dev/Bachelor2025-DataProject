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

    // Set default border width or use provided value
    // For stacked charts, use thin borders by default to reduce overlap issues
    let defaultBorderWidth = config.type === 'line' ? 2 : 1;
    if (config.stacked && config.borderWidth === undefined) {
        defaultBorderWidth = 2;  // Thin borders for stacked charts by default
    }
    const borderWidth = config.borderWidth !== undefined ? config.borderWidth : defaultBorderWidth;

    let datasets = [];

    // Check if this is likely a SingleValues chart (one row in the 2D array)
    const isSingleValueChart = config.values.length === 1 && !config.stacked;

    if (isSingleValueChart) {
        // For SingleValues chart
        let bgColors;

        // Check for custom colors first
        if (config.backgroundColors && config.backgroundColors.length > 0) {
            if (config.type === 'pie' || config.type === 'doughnut') {
                // For pie/doughnut, repeat the colors to match data points
                bgColors = Array.from({ length: config.values[0].length },
                    (_, i) => config.backgroundColors[i % config.backgroundColors.length]);
            } else {
                // For other charts, use the first color
                bgColors = config.backgroundColors[0];
            }
        } else {
            // Use generated colors as fallback
            if (config.type === 'pie' || config.type === 'doughnut') {
                bgColors = generateColors(config.values[0].length, config.type);
            } else {
                bgColors = generateColors(1, config.type)[0];
            }
        }

        datasets = [{
            label: config.title || 'Dataset',
            data: config.values[0],
            backgroundColor: bgColors,
            borderColor: config.type === 'line' ? bgColors : '#000000',
            borderWidth: config.type === 'line' ? borderWidth : borderWidth,
            fill: config.type === 'line' ? false : true
        }];
    } else {
        // For MultiValues chart
        let datasetColors;

        // Use custom colors if provided
        if (config.backgroundColors && config.backgroundColors.length > 0) {
            datasetColors = Array.from({ length: config.values.length },
                (_, i) => config.backgroundColors[i % config.backgroundColors.length]);
        } else if (config.type === 'bar') {
            // Check if we have dataset labels to determine colors based on content
            if (config.datasetLabels && config.datasetLabels.length > 0) {
                // Assign colors based on whether label contains "vaskemaskine"
                datasetColors = config.datasetLabels.map(label =>
                    label.toLowerCase().includes('vaskemaskine') ?
                        '#66dd66' :  // Blue for washing machines
                        '#dd6666'    // Red for others
                );
            } else {
                datasetColors = generateColors(config.values.length, config.type);
            }
        } else {
            datasetColors = generateColors(config.values.length, config.type);
        }

        if (config.type === 'line') {
            datasets = config.values.map((datasetValues, datasetIndex) => ({
                label: config.datasetLabels && config.datasetLabels.length > datasetIndex
                    ? config.datasetLabels[datasetIndex]
                    : `Dataset ${datasetIndex + 1}`,
                data: datasetValues,
                backgroundColor: 'transparent',
                borderColor: datasetColors[datasetIndex],
                borderWidth: borderWidth,
                fill: false
            }));
        } else {
            datasets = config.values[0].map((_, colIndex) => ({
                label: config.datasetLabels && config.datasetLabels.length > colIndex
                    ? config.datasetLabels[colIndex]
                    : `Dataset ${colIndex + 1}`,
                data: config.values.map(row => row[colIndex]),
                backgroundColor: datasetColors[colIndex],
                borderColor: '#000000', // Restore visible borders
                borderWidth: borderWidth,
                stack: config.stacked ? 'stack1' : undefined
            }));
        }
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
                    beginAtZero: true,
                    stacked: config.stacked, // Enable stacking for x-axis
                    ticks: {
                        font: {
                            size: axisLabelSize // Set x-axis label font size
                        }
                    }
                },
                y: {
                    beginAtZero: true,
                    stacked: config.stacked, // Enable stacking for y-axis
                    ticks: {
                        font: {
                            size: axisLabelSize// Set y-axis label font size


                        }
                    }
                }
            }
        }
    });

    // Notify Blazor that the chart has rendered (for loader)
    if (config.dotNetRef) {
        config.dotNetRef.invokeMethodAsync('ChartRendered');
    }
};

function generateColors(count, type) {
    const baseColors = [
        '#66dd66',  // Pastel green
        '#dd6666',  // Pastel red
        '#66aadd',  // Pastel blue
        '#cc66cc',  // Pastel purple
        '#dddd66',  // Pastel yellow
        '#ffaa66',  // Pastel orange
        '#ff99cc',  // Pastel pink
        '#66cccc',  // Pastel teal
        '#aa99dd',  // Pastel lavender
        '#99ddbb',  // Pastel mint
    ];

    return Array.from({ length: count }, (_, i) => baseColors[i % baseColors.length]);
}