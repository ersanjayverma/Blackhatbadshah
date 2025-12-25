namespace shared.Dto;
public class ChatMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Content { get; set; } = "";
    public bool IsUser { get; set; }
}