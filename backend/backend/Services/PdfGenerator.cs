using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Markdig;

namespace backend.Services
{
    public static class PdfGenerator
    {
        public static byte[] FromMarkdown(string markdown)
        {
            var bodyHtml = Markdown.ToHtml(markdown);

            var html = $@"
<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'>
<style>
    @page {{
        margin: 30mm 20mm;
    }}

    body {{
        font-family: Arial, Helvetica, sans-serif;
        font-size: 16pt;
        line-height: 1.6;
        color: #1f2933;
    }}

    /* ===== FIRST PAGE HEADER ===== */
    .report-header {{
        border-bottom: 3px solid #991b1b;
        padding-bottom: 12px;
        margin-bottom: 24px;
    }}

    .report-title {{
        font-size: 28pt;
        font-weight: bold;
        margin: 0;
    }}

    .report-subtitle {{
        font-size: 14pt;
        color: #4b5563;
        margin-top: 6px;
    }}

    .contact {{
        margin-top: 10px;
        font-size: 12pt;
        color: #374151;
    }}

    /* Force content after header to next page */
    .page-break {{
        page-break-after: always;
    }}

    h1 {{
        font-size: 26pt;
        border-bottom: 2px solid #991b1b;
        padding-bottom: 6px;
    }}

    h2 {{
        font-size: 22pt;
        color: #991b1b;
    }}

    h3 {{
        font-size: 18pt;
    }}

    pre {{
        background: #111827;
        color: #e5e7eb;
        padding: 12px;
        border-radius: 6px;
        font-size: 14pt;
    }}

    code {{
        background: #111827;
        color: #e5e7eb;
        padding: 4px 6px;
        border-radius: 4px;
        font-size: 14pt;
    }}

    table {{
        border-collapse: collapse;
        width: 100%;
        margin-top: 12px;
    }}

    th, td {{
        border: 1px solid #d1d5db;
        padding: 8px;
        font-size: 14pt;
    }}

    th {{
        background: #f3f4f6;
        text-align: left;
    }}
</style>
</head>
<body>

<!-- ===== FIRST PAGE ONLY ===== -->
<div class='report-header'>
    <div class='report-title'>BlackHatBadshah – Diagnostic Report</div>
    <div class='report-subtitle'>Confidential Technical Analysis</div>
    <div class='contact'>
        admin@blackhatbadshah.com · +91 8580 400 366
    </div>
</div>
<!-- ===== REPORT CONTENT STARTS ===== -->
{bodyHtml}

</body>
</html>";

            var tempHtml = Path.GetTempFileName() + ".html";
            var tempPdf  = Path.GetTempFileName() + ".pdf";

            File.WriteAllText(tempHtml, html, Encoding.UTF8);

            var psi = new ProcessStartInfo
            {
                FileName = "/usr/bin/wkhtmltopdf",
                Arguments = $"\"{tempHtml}\" \"{tempPdf}\"",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };

            using var process = Process.Start(psi);
            var error = process!.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
                throw new Exception($"wkhtmltopdf failed: {error}");

            var pdfBytes = File.ReadAllBytes(tempPdf);

            File.Delete(tempHtml);
            File.Delete(tempPdf);

            return pdfBytes;
        }
    }
}
