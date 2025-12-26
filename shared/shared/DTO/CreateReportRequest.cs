namespace shared.Dto;
public class CreateReportRequest
{
    public string Title { get; set; } = null!;
    public string Summary { get; set; } = null!;
    public string ReportPath { get; set; } = null!;
}
