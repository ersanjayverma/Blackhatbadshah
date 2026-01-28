namespace backend.Configuration;

public class QdrantConfiguration
{
    public string Url { get; set; } = "https://qdrant.blackhatbadshah.com";
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string CollectionName { get; set; } = "log_analysis";
    public int VectorSize { get; set; } = 1536; // OpenAI embedding size, adjust for your model
    public int SearchLimit { get; set; } = 10;
    public float ScoreThreshold { get; set; } = 0.7f;
}
