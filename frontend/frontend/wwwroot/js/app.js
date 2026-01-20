// Theme handling functions
window.getTheme = () => {
    return localStorage.getItem('theme') || 'dark';
};

window.setTheme = (theme) => {
    localStorage.setItem('theme', theme);
    document.body.setAttribute('data-theme', theme);
};

// Initialize theme on page load
(function() {
    const savedTheme = localStorage.getItem('theme') || 'dark';
    document.body.setAttribute('data-theme', savedTheme);
})();

window.scrollChatToBottom = () => {
    const el = document.querySelector('.chat-body');
    if (el) el.scrollTop = el.scrollHeight;
};

window.scrollToBottom = (element) => {
    if (element) {
        element.scrollTop = element.scrollHeight;
    }
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
    console.log('renderChart called with:', chartData);
    
    try {
        if (typeof Chart === 'undefined') {
            console.error('Chart.js not loaded');
            window.__chartRendered = true;
            return;
        }

        const canvas = document.getElementById('reportChart');
        if (!canvas) {
            console.error('Canvas element not found');
            window.__chartRendered = true;
            return;
        }

        if (!chartData || !chartData.series || chartData.series.length === 0) {
            console.error('Invalid chart data');
            window.__chartRendered = true;
            return;
        }

        const typeMap = {
            LineChart: 'line',
            BarChart: 'bar',
            ColumnChart: 'bar',
            PieChart: 'pie',
            StackedColumnChart: 'bar'
        };

        const chartType = typeMap[chartData.chartType] || 'bar';
        console.log('Chart type:', chartType);

        // Vibrant colors for dark background
        const colors = [
            '#ef4444', // red-500
            '#3b82f6', // blue-500
            '#10b981', // emerald-500
            '#8b5cf6', // violet-500
            '#f97316', // orange-500
            '#06b6d4', // cyan-500
            '#a855f7', // purple-500
            '#22c55e'  // green-500
        ];

        const datasets = chartData.series.map((s, i) => {
            const color = colors[i % colors.length];
            return {
                label: s.name,
                data: s.values,
                borderColor: color,
                backgroundColor: chartType === 'pie'
                    ? colors
                    : color + '40', // 25% opacity for dark bg
                borderWidth: 2.5,
                tension: 0.4,
                pointRadius: chartType === 'line' ? 4 : 0,
                pointHoverRadius: chartType === 'line' ? 6 : 0,
                pointBackgroundColor: color,
                pointBorderColor: '#1e293b', // dark border
                pointBorderWidth: 2
            };
        });

        if (currentChart) {
            currentChart.destroy();
        }

        currentChart = new Chart(canvas.getContext('2d'), {
            type: chartType,
            data: {
                labels: chartData.xAxis.labels,
                datasets
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                animation: false,
                plugins: {
                    legend: {
                        display: true,
                        position: 'top',
                        align: 'start',
                        labels: {
                            usePointStyle: true,
                            pointStyle: 'circle',
                            padding: 20,
                            font: {
                                size: 12,
                                weight: '500',
                                family: "'Inter', sans-serif"
                            },
                            color: '#e2e8f0' // Light text for dark bg
                        }
                    },
                    tooltip: {
                        backgroundColor: 'rgba(30, 41, 59, 0.95)', // Dark tooltip
                        titleColor: '#ffffff',
                        bodyColor: '#e2e8f0',
                        borderColor: '#475569',
                        borderWidth: 1,
                        padding: 12,
                        displayColors: true,
                        titleFont: {
                            size: 13,
                            weight: '600'
                        },
                        bodyFont: {
                            size: 12
                        }
                    }
                },
                scales: chartType !== 'pie' ? {
                    x: {
                        grid: {
                            color: '#334155', // Dark grid lines
                            drawBorder: false
                        },
                        ticks: {
                            color: '#94a3b8', // Light gray text
                            font: {
                                size: 11,
                                family: "'Inter', sans-serif"
                            }
                        }
                    },
                    y: {
                        grid: {
                            color: '#334155', // Dark grid lines
                            drawBorder: false
                        },
                        ticks: {
                            color: '#94a3b8', // Light gray text
                            font: {
                                size: 11,
                                family: "'Inter', sans-serif"
                            }
                        }
                    }
                } : {}
            }
        });

        console.log('Chart created successfully');
        
        setTimeout(() => {
            window.__chartRendered = true;
            console.log('Chart render completed');
        }, 500);
    }
    catch (err) {
        console.error('Chart rendering error:', err);
        window.__chartRendered = true;
    }
};

// Dashboard charts storage
let dashboardCharts = {};

window.renderDashboardChart = (canvasId, chartData) => {
    console.log('renderDashboardChart called for:', canvasId, chartData);

    try {
        if (typeof Chart === 'undefined') {
            console.error('Chart.js not loaded');
            return;
        }

        const canvas = document.getElementById(canvasId);
        if (!canvas) {
            console.error('Canvas element not found:', canvasId);
            return;
        }

        if (!chartData || !chartData.series || chartData.series.length === 0) {
            console.error('Invalid chart data for', canvasId);
            return;
        }

        const typeMap = {
            LineChart: 'line',
            BarChart: 'bar',
            PieChart: 'pie',
            DoughnutChart: 'doughnut'
        };

        const chartType = typeMap[chartData.chartType] || 'bar';

        // Dashboard-specific colors (orange accent theme)
        const colors = [
            '#d97706', // orange accent
            '#10b981', // emerald
            '#ef4444', // red
            '#3b82f6', // blue
            '#8b5cf6', // violet
            '#06b6d4', // cyan
        ];

        const datasets = chartData.series.map((s, i) => {
            const color = colors[i % colors.length];
            return {
                label: s.name,
                data: s.values,
                borderColor: color,
                backgroundColor: chartType === 'pie' || chartType === 'doughnut'
                    ? colors.slice(0, chartData.xAxis.labels.length)
                    : color + '40',
                borderWidth: 2.5,
                tension: 0.4,
                pointRadius: chartType === 'line' ? 4 : 0,
                pointHoverRadius: chartType === 'line' ? 6 : 0,
                pointBackgroundColor: color,
                pointBorderColor: '#1a1a1a',
                pointBorderWidth: 2,
                fill: chartType === 'line'
            };
        });

        // Destroy existing chart if present
        if (dashboardCharts[canvasId]) {
            dashboardCharts[canvasId].destroy();
        }

        dashboardCharts[canvasId] = new Chart(canvas.getContext('2d'), {
            type: chartType,
            data: {
                labels: chartData.xAxis.labels,
                datasets
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                animation: { duration: 500 },
                plugins: {
                    legend: {
                        display: true,
                        position: chartType === 'pie' ? 'right' : 'top',
                        labels: {
                            usePointStyle: true,
                            padding: 15,
                            font: { size: 11, family: "'Inter', sans-serif" },
                            color: '#e2e8f0'
                        }
                    },
                    tooltip: {
                        backgroundColor: 'rgba(30, 41, 59, 0.95)',
                        titleColor: '#ffffff',
                        bodyColor: '#e2e8f0',
                        borderColor: '#475569',
                        borderWidth: 1,
                        padding: 12
                    }
                },
                scales: chartType !== 'pie' && chartType !== 'doughnut' ? {
                    x: {
                        grid: { color: '#334155', drawBorder: false },
                        ticks: { color: '#94a3b8', font: { size: 11 } }
                    },
                    y: {
                        grid: { color: '#334155', drawBorder: false },
                        ticks: { color: '#94a3b8', font: { size: 11 } },
                        beginAtZero: true
                    }
                } : {}
            }
        });

        console.log('Dashboard chart created:', canvasId);
    }
    catch (err) {
        console.error('Dashboard chart error:', err);
    }
};