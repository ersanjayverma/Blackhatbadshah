namespace shared.Dto;
public class LogDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = "";
    public long SizeBytes { get; set; }
    public DateTime CreatedAt { get; set; }
}
