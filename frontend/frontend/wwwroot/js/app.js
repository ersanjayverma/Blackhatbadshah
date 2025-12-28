window.scrollChatToBottom = () => {
    const el = document.querySelector('.chat-body');
    if (el) el.scrollTop = el.scrollHeight;
};
window.downloadFile = (fileName, contentType, bytes) => {
    const blob = new Blob([new Uint8Array(bytes)], { type: contentType });
    const url = URL.createObjectURL(blob);

    const a = document.createElement("a");
    a.href = url;
    a.download = fileName;
    a.click();

    URL.revokeObjectURL(url);
};
window.closeMobileNav = () => {
    const overlay = document.querySelector('.mobile-overlay');
    const sidebar = document.getElementById('main-sidebar');

    if (overlay) overlay.classList.remove('active');
    if (sidebar) sidebar.classList.remove('mobile-open');
};

let currentChart = null;

window.renderChart = (chartData) => {
    try {
        console.log('=== Chart Rendering Started ===');
        console.log('Chart data received:', chartData);

        // Check if Chart.js is loaded
        if (typeof Chart === 'undefined') {
            console.error('Chart.js library not loaded');
            return;
        }
        console.log('Chart.js library loaded successfully');

        const canvas = document.getElementById('reportChart');
        if (!canvas) {
            console.error('Canvas element not found - modal may not be rendered yet');
            return;
        }
        console.log('Canvas element found:', canvas);
        console.log('Canvas dimensions:', canvas.offsetWidth, 'x', canvas.offsetHeight);

        // Destroy existing chart if any
        if (currentChart) {
            console.log('Destroying existing chart');
            currentChart.destroy();
            currentChart = null;
        }

        const ctx = canvas.getContext('2d');
        if (!ctx) {
            console.error('Could not get 2D context');
            return;
        }
        console.log('Canvas 2D context obtained');

        // Validate chart data
        if (!chartData || !chartData.series || chartData.series.length === 0) {
            console.error('Invalid or empty chart data');
            return;
        }
        console.log('Chart data validation passed');
        console.log('Chart type:', chartData.chartType);
        console.log('Series count:', chartData.series.length);
        console.log('Labels count:', chartData.xAxis?.labels?.length);

        // Map chart types
        const chartTypeMap = {
            'LineChart': 'line',
            'BarChart': 'bar',
            'ColumnChart': 'bar',
            'PieChart': 'pie',
            'StackedColumnChart': 'bar'
        };

        const chartType = chartTypeMap[chartData.chartType] || 'bar';
        console.log('Mapped chart type:', chartType);

        // Prepare datasets
        const datasets = chartData.series.map((series, index) => {
            const colors = [
                'rgb(54, 162, 235)',
                'rgb(255, 99, 132)',
                'rgb(255, 205, 86)',
                'rgb(75, 192, 192)',
                'rgb(153, 102, 255)',
                'rgb(255, 159, 64)'
            ];
            const color = colors[index % colors.length];

            return {
                label: series.name,
                data: series.values,
                backgroundColor: chartType === 'pie' ? colors : color.replace('rgb', 'rgba').replace(')', ', 0.5)'),
                borderColor: color,
                borderWidth: 2,
                tension: 0.1
            };
        });
        console.log('Datasets prepared:', datasets.length);

        // Chart configuration
        const config = {
            type: chartType,
            data: {
                labels: chartData.xAxis.labels,
                datasets: datasets
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        display: true,
                        position: 'top'
                    },
                    title: {
                        display: !!chartData.title,
                        text: chartData.title,
                        font: {
                            size: 16,
                            weight: 'bold'
                        }
                    }
                },
                scales: chartType !== 'pie' ? {
                    y: {
                        beginAtZero: true,
                        stacked: chartData.chartType === 'StackedColumnChart'
                    },
                    x: {
                        stacked: chartData.chartType === 'StackedColumnChart'
                    }
                } : undefined
            }
        };

        console.log('Creating Chart instance...');
        currentChart = new Chart(ctx, config);
        console.log('✅ Chart rendered successfully!');
        console.log('=== Chart Rendering Complete ===');
    } catch (error) {
        console.error('❌ Error rendering chart:', error);
        console.error('Error stack:', error.stack);
    }
};