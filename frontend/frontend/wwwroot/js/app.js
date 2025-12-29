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

        // Professional color palette
        const colors = [
            '#dc2626', // red-600
            '#2563eb', // blue-600
            '#059669', // emerald-600
            '#7c3aed', // violet-600
            '#ea580c', // orange-600
            '#0891b2', // cyan-600
            '#9333ea', // purple-600
            '#16a34a'  // green-600
        ];

        const datasets = chartData.series.map((s, i) => {
            const color = colors[i % colors.length];
            return {
                label: s.name,
                data: s.values,
                borderColor: color,
                backgroundColor: chartType === 'pie'
                    ? colors
                    : color + '33', // 20% opacity
                borderWidth: 2.5,
                tension: 0.4,
                pointRadius: chartType === 'line' ? 4 : 0,
                pointHoverRadius: chartType === 'line' ? 6 : 0,
                pointBackgroundColor: color,
                pointBorderColor: '#ffffff',
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
                            color: '#334155'
                        }
                    },
                    tooltip: {
                        backgroundColor: 'rgba(15, 23, 42, 0.95)',
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
                            color: '#f1f5f9',
                            drawBorder: false
                        },
                        ticks: {
                            color: '#64748b',
                            font: {
                                size: 11,
                                family: "'Inter', sans-serif"
                            }
                        }
                    },
                    y: {
                        grid: {
                            color: '#f1f5f9',
                            drawBorder: false
                        },
                        ticks: {
                            color: '#64748b',
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