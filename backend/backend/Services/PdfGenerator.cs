using System.Text;
using System.Text.Json;
using Markdig;
using Microsoft.Playwright;

namespace backend.Services;

public static class PdfGenerator
{
    public static async Task<byte[]> FromMarkdownAsync(
        string markdown,
        string? chartJson
    )
    {
        // --------------------------------------------------
        // 1. Markdown → HTML
        // --------------------------------------------------
        var markdownHtml = Markdown.ToHtml(markdown);

        // --------------------------------------------------
        // 2. Validate chart JSON
        // --------------------------------------------------
        bool hasValidChart = false;

        if (!string.IsNullOrWhiteSpace(chartJson))
        {
            try
            {
                using var _ = JsonDocument.Parse(chartJson);
                hasValidChart = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Invalid chart JSON: {ex.Message}");
                hasValidChart = false;
            }
        }

        // --------------------------------------------------
        // 3. Conditional chart HTML
        // --------------------------------------------------
        var chartHtml = hasValidChart
            ? """
              <div class="chart-section">
                  <div class="section-header">
                      <div class="section-number">01</div>
                      <h2>Visual Analysis</h2>
                  </div>
                  <div class="chart-container">
                      <canvas id="reportChart"></canvas>
                  </div>
              </div>
              """
            : "";

        // --------------------------------------------------
        // 4. Chart.js library + script
        // --------------------------------------------------
        var chartScript = hasValidChart
            ? "<script src=\"https://cdn.jsdelivr.net/npm/chart.js@4.4.0/dist/chart.umd.min.js\"></script>\n" +
              "<script>\n" +
              "    window.addEventListener('DOMContentLoaded', function() {\n" +
              "        console.log('DOM loaded, rendering chart...');\n" +
              "        try {\n" +
              "            renderChart(" + chartJson + ");\n" +
              "        } catch(err) {\n" +
              "            console.error('Chart render error:', err);\n" +
              "            window.__chartRendered = true;\n" +
              "        }\n" +
              "    });\n" +
              "</script>"
            : "";

        // --------------------------------------------------
        // 5. PROFESSIONAL HTML TEMPLATE
        // --------------------------------------------------
        var htmlTemplate = """
<!DOCTYPE html>
<html>
<head>
<meta charset="utf-8">

<style>
@import url('https://fonts.googleapis.com/css2?family=Inter:wght@300;400;500;600;700&family=JetBrains+Mono:wght@400;500&display=swap');

@page { 
    margin: 20mm 15mm;
    @bottom-right {
        content: counter(page) " / " counter(pages);
        font-size: 9pt;
        color: #64748b;
        font-family: 'Inter', sans-serif;
    }
}

* {
    box-sizing: border-box;
}

html, body {
    margin: 0;
    padding: 0;
    background: #ffffff;
    color: #0f172a;
    font-family: 'Inter', -apple-system, BlinkMacSystemFont, sans-serif;
    font-size: 11pt;
    line-height: 1.7;
    word-wrap: break-word;
    overflow-wrap: break-word;
    -webkit-font-smoothing: antialiased;
    -moz-osx-font-smoothing: grayscale;
}

/* ELEGANT WATERMARK */
.watermark {
    position: fixed;
    top: 50%;
    left: 50%;
    transform: translate(-50%, -50%) rotate(-45deg);
    font-size: 72pt;
    font-weight: 800;
    letter-spacing: 0.15em;
    color: rgba(15, 23, 42, 0.02);
    white-space: nowrap;
    pointer-events: none;
    z-index: 0;
    font-family: 'Inter', sans-serif;
    text-transform: uppercase;
}

/* MAIN CONTAINER */
.report {
    max-width: 100%;
    margin: 0 auto;
    background: #ffffff;
    position: relative;
    z-index: 1;
}

/* SOPHISTICATED HEADER */
.report-header {
    margin-bottom: 48px;
    padding-bottom: 32px;
    border-bottom: 1px solid #e2e8f0;
    position: relative;
}

.brand-accent {
    width: 60px;
    height: 4px;
    background: linear-gradient(135deg, #dc2626 0%, #991b1b 100%);
    margin-bottom: 24px;
    border-radius: 2px;
}

.report-title {
    margin: 0 0 12px 0;
    font-size: 32pt;
    font-weight: 700;
    letter-spacing: -0.025em;
    line-height: 1.2;
    color: #0f172a;
}

.report-subtitle {
    margin: 0;
    font-size: 13pt;
    font-weight: 400;
    color: #64748b;
    letter-spacing: 0.01em;
}

.report-meta {
    margin-top: 24px;
    padding-top: 16px;
    border-top: 1px solid #f1f5f9;
    display: flex;
    gap: 32px;
    font-size: 9pt;
    color: #64748b;
}

.meta-item {
    display: flex;
    flex-direction: column;
    gap: 4px;
}

.meta-label {
    font-weight: 600;
    text-transform: uppercase;
    letter-spacing: 0.05em;
    font-size: 8pt;
    color: #94a3b8;
}

.meta-value {
    font-weight: 500;
    color: #475569;
}

/* REPORT BODY */
.report-body {
    padding-top: 8px;
    word-wrap: break-word;
    overflow-wrap: break-word;
}

/* SECTION HEADERS */
.section-header {
    display: flex;
    align-items: center;
    gap: 16px;
    margin-top: 40px;
    margin-bottom: 24px;
}

.section-number {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    width: 40px;
    height: 40px;
    background: linear-gradient(135deg, #dc2626 0%, #991b1b 100%);
    color: white;
    font-weight: 700;
    font-size: 13pt;
    border-radius: 8px;
    flex-shrink: 0;
}

/* MARKDOWN TYPOGRAPHY */
.report-body h1 {
    font-size: 24pt;
    font-weight: 700;
    margin: 48px 0 24px 0;
    color: #0f172a;
    letter-spacing: -0.02em;
    line-height: 1.3;
    border-bottom: 2px solid #e2e8f0;
    padding-bottom: 16px;
}

.report-body h2 {
    font-size: 18pt;
    font-weight: 600;
    margin: 36px 0 16px 0;
    color: #1e293b;
    letter-spacing: -0.015em;
    line-height: 1.4;
}

.report-body h3 {
    font-size: 14pt;
    font-weight: 600;
    margin: 28px 0 12px 0;
    color: #334155;
    letter-spacing: -0.01em;
}

.report-body h4 {
    font-size: 12pt;
    font-weight: 600;
    margin: 24px 0 10px 0;
    color: #475569;
}

.report-body p {
    margin: 0 0 16px 0;
    color: #334155;
    text-align: justify;
    word-wrap: break-word;
    overflow-wrap: break-word;
}

.report-body ul,
.report-body ol {
    margin: 16px 0;
    padding-left: 28px;
    color: #334155;
}

.report-body li {
    margin: 8px 0;
    padding-left: 8px;
    word-wrap: break-word;
    overflow-wrap: break-word;
}

.report-body strong {
    font-weight: 600;
    color: #0f172a;
}

.report-body em {
    font-style: italic;
    color: #475569;
}

.report-body a {
    color: #dc2626;
    text-decoration: none;
    border-bottom: 1px solid transparent;
    transition: border-color 0.2s;
}

.report-body a:hover {
    border-bottom-color: #dc2626;
}

/* CODE BLOCKS */
.report-body pre {
    background: #f8fafc;
    border: 1px solid #e2e8f0;
    border-left: 4px solid #dc2626;
    border-radius: 8px;
    padding: 20px;
    margin: 24px 0;
    page-break-inside: avoid;
    overflow-x: auto;
    white-space: pre-wrap;
    word-wrap: break-word;
    overflow-wrap: break-word;
    font-family: 'JetBrains Mono', 'Courier New', monospace;
    font-size: 9pt;
    line-height: 1.6;
    box-shadow: 0 1px 3px rgba(0, 0, 0, 0.05);
}

.report-body code {
    background: #f1f5f9;
    color: #dc2626;
    padding: 3px 8px;
    border-radius: 4px;
    font-family: 'JetBrains Mono', 'Courier New', monospace;
    font-size: 9.5pt;
    font-weight: 500;
    word-wrap: break-word;
    overflow-wrap: break-word;
}

.report-body pre code {
    background: transparent;
    color: #0f172a;
    padding: 0;
    border-radius: 0;
}

/* BLOCKQUOTES */
.report-body blockquote {
    margin: 24px 0;
    padding: 16px 24px;
    border-left: 4px solid #dc2626;
    background: #fef2f2;
    color: #991b1b;
    font-style: italic;
    page-break-inside: avoid;
}

/* PROFESSIONAL TABLES */
table {
    width: 100%;
    border-collapse: collapse;
    margin: 24px 0;
    table-layout: fixed;
    font-size: 10pt;
    box-shadow: 0 1px 3px rgba(0, 0, 0, 0.05);
    border-radius: 8px;
    overflow: hidden;
}

thead {
    background: linear-gradient(135deg, #991b1b 0%, #7f1d1d 100%);
    color: white;
}

th {
    padding: 14px 16px;
    text-align: left;
    font-weight: 600;
    letter-spacing: 0.025em;
    text-transform: uppercase;
    font-size: 9pt;
    border: none;
    word-wrap: break-word;
    overflow-wrap: break-word;
}

td {
    padding: 12px 16px;
    border-bottom: 1px solid #e2e8f0;
    color: #334155;
    word-wrap: break-word;
    overflow-wrap: break-word;
}

tbody tr:last-child td {
    border-bottom: none;
}

tbody tr:nth-child(even) {
    background: #f8fafc;
}

tbody tr:hover {
    background: #f1f5f9;
}

/* CHART SECTION */
.chart-section {
    margin: 48px 0;
    padding: 32px;
    background: linear-gradient(135deg, #fafafa 0%, #ffffff 100%);
    border: 1px solid #e2e8f0;
    border-radius: 12px;
    page-break-inside: avoid;
    box-shadow: 0 2px 8px rgba(0, 0, 0, 0.04);
}

.chart-section h2 {
    margin: 0 0 24px 0;
    font-size: 18pt;
    font-weight: 600;
    color: #1e293b;
}

.chart-container {
    height: 400px;
    position: relative;
    background: white;
    border-radius: 8px;
    padding: 16px;
}

/* SOPHISTICATED FOOTER */
.report-footer {
    margin-top: 64px;
    padding-top: 24px;
    border-top: 2px solid #e2e8f0;
    display: flex;
    justify-content: space-between;
    align-items: center;
    font-size: 9pt;
    color: #64748b;
}

.footer-left {
    display: flex;
    flex-direction: column;
    gap: 4px;
}

.footer-classification {
    font-weight: 600;
    text-transform: uppercase;
    letter-spacing: 0.1em;
    color: #dc2626;
    font-size: 8pt;
}

.footer-notice {
    font-weight: 400;
    color: #94a3b8;
}

.footer-right {
    text-align: right;
    font-weight: 500;
    color: #64748b;
}

/* UTILITY CLASSES */
.page-break {
    page-break-after: always;
}

hr {
    border: none;
    border-top: 1px solid #e2e8f0;
    margin: 32px 0;
}

/* PRINT OPTIMIZATIONS */
@media print {
    body {
        -webkit-print-color-adjust: exact;
        print-color-adjust: exact;
    }
    
    .chart-section {
        page-break-inside: avoid;
    }
    
    table {
        page-break-inside: avoid;
    }
}
</style>

<script>
/* Chart rendering with professional styling */
window.__chartRendered = false;
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
</script>
</head>

<body>

<div class="watermark">BlackHat Badshah</div>

<div class="report">
    <div class="report-header">
        <div class="brand-accent"></div>
        <h1 class="report-title">BlackHatBadshah</h1>
        <p class="report-subtitle">Advanced Diagnostic & Analysis Report</p>
        
        <div class="report-meta">
            <div class="meta-item">
                <span class="meta-label">Generated</span>
                <span class="meta-value" id="reportDate"></span>
            </div>
            <div class="meta-item">
                <span class="meta-label">Classification</span>
                <span class="meta-value">Confidential</span>
            </div>
            <div class="meta-item">
                <span class="meta-label">Version</span>
                <span class="meta-value">1.0</span>
            </div>
        </div>
    </div>

    <div class="report-body">
        __MARKDOWN_HTML__
        __CHART_HTML__
    </div>

    <div class="report-footer">
        <div class="footer-left">
            <div class="footer-classification">Confidential</div>
            <div class="footer-notice">For internal use only • Not for distribution</div>
        </div>
        <div class="footer-right">
            BlackHatBadshah Analysis Platform
        </div>
    </div>
</div>

<script>
// Set current date
document.getElementById('reportDate').textContent = new Date().toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'long',
    day: 'numeric'
});
</script>

__CHART_SCRIPT__

</body>
</html>
""";

        // --------------------------------------------------
        // 6. Inject content
        // --------------------------------------------------
        var html = htmlTemplate
            .Replace("__MARKDOWN_HTML__", markdownHtml)
            .Replace("__CHART_HTML__", chartHtml)
            .Replace("__CHART_SCRIPT__", chartScript);
            
        Console.WriteLine("Chart JSON:");
        Console.WriteLine(chartJson ?? "null");
        
        // --------------------------------------------------
        // 7. Playwright render with optimizations
        // --------------------------------------------------
        var browser = await PlaywrightHost.GetBrowserAsync();

        await using var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();
        
        // Enable console logging
        page.Console += (_, msg) => Console.WriteLine($"Browser: {msg.Text}");
        page.PageError += (_, error) => Console.WriteLine($"Page Error: {error}");

        var tempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.html");

        try
        {
            await File.WriteAllTextAsync(tempFilePath, html, Encoding.UTF8);

            await page.GotoAsync($"file://{tempFilePath}", new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle
            });

            if (hasValidChart)
            {
                try
                {
                    await page.WaitForFunctionAsync(
                        "() => window.__chartRendered === true",
                        new PageWaitForFunctionOptions { Timeout = 15000 }
                    );
                    Console.WriteLine("Chart rendered successfully");
                    
                    // Extra delay for chart painting
                    await Task.Delay(1000);
                }
                catch (TimeoutException)
                {
                    Console.WriteLine("Chart render timeout - proceeding anyway");
                    await page.ScreenshotAsync(new PageScreenshotOptions 
                    { 
                        Path = Path.Combine(Path.GetTempPath(), "chart-debug.png") 
                    });
                }
            }

            var pdf = await page.PdfAsync(new PagePdfOptions
            {
                Format = "A4",
                PrintBackground = true,
                PreferCSSPageSize = false,
                DisplayHeaderFooter = false
            });

            return pdf;
        }
        finally
        {
            await page.CloseAsync();
            if (File.Exists(tempFilePath))
                File.Delete(tempFilePath);
        }
    }
}