namespace shared.Dto;

/// <summary>
/// Centralized model configuration with friendly names mapped to actual API model identifiers
/// </summary>
public static class ModelMapping
{
    /// <summary>
    /// Claude/Anthropic models - maps friendly name to actual API model identifier
    /// </summary>
    public static readonly Dictionary<string, string> ClaudeModels = new()
    {
        { "claude-sonnet-4-5", "claude-sonnet-4-5-20250929" },
        { "claude-opus-4-5", "claude-opus-4-5-20251101" },
        { "claude-sonnet-3-5", "claude-3-5-sonnet-20241022" }
    };

    /// <summary>
    /// OpenAI/GPT models - maps friendly name to actual API model identifier
    /// </summary>
    public static readonly Dictionary<string, string> GPTModels = new()
    {
        { "gpt-4o", "gpt-4o" },
        { "gpt-4-turbo", "gpt-4-turbo" },
        { "gpt-4-turbo-preview", "gpt-4-turbo-preview" },
        { "gpt-3.5-turbo", "gpt-3.5-turbo" }
    };

    /// <summary>
    /// TogetherAI models - maps friendly name to actual API model identifier
    /// </summary>
    public static readonly Dictionary<string, string> TogetherModels = new()
    {
        { "together-llama-3-70b", "meta-llama/Llama-3-70b-chat-hf" },
        { "together-llama-3.1-405b", "meta-llama/Meta-Llama-3.1-405B-Instruct-Turbo" },
        { "together-mixtral-8x7b", "mistralai/Mixtral-8x7B-Instruct-v0.1" },
        { "together-qwen", "Qwen/Qwen3-Coder-480B-A35B-Instruct-FP8" }
    };

    /// <summary>
    /// All model mappings combined
    /// </summary>
    public static readonly Dictionary<string, string> AllModels = ClaudeModels
        .Concat(GPTModels)
        .Concat(TogetherModels)
        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

    /// <summary>
    /// All supported model friendly names
    /// </summary>
    public static readonly List<string> AllSupportedModels = AllModels.Keys.ToList();

    /// <summary>
    /// Default model to use when none is specified
    /// </summary>
    public const string DefaultModel = "claude-sonnet-4-5";

    /// <summary>
    /// Checks if a model name is supported
    /// </summary>
    public static bool IsSupported(string modelName)
    {
        return AllModels.ContainsKey(modelName);
    }

    /// <summary>
    /// Gets the actual API model identifier for a friendly name
    /// </summary>
    public static string GetActualModelName(string friendlyName)
    {
        return AllModels.TryGetValue(friendlyName, out var actualName) ? actualName : friendlyName;
    }

    /// <summary>
    /// Gets the provider (Anthropic, OpenAI, or TogetherAI) for a given model
    /// </summary>
    public static string GetProvider(string modelName)
    {
        if (ClaudeModels.ContainsKey(modelName))
            return "Anthropic";
        if (GPTModels.ContainsKey(modelName))
            return "OpenAI";
        if (TogetherModels.ContainsKey(modelName))
            return "TogetherAI";
        return "Unknown";
    }

    /// <summary>
    /// Gets friendly display name for a model
    /// </summary>
    public static string GetDisplayName(string modelName)
    {
        return modelName switch
        {
            // Claude models
            "claude-sonnet-4-5" => "Claude Sonnet 4.5",
            "claude-opus-4-5" => "Claude Opus 4.5",
            "claude-sonnet-3-5" => "Claude Sonnet 3.5",

            // OpenAI models
            "gpt-4o" => "GPT-4o",
            "gpt-4-turbo" => "GPT-4 Turbo",
            "gpt-4-turbo-preview" => "GPT-4 Turbo Preview",
            "gpt-3.5-turbo" => "GPT-3.5 Turbo",

            // TogetherAI models
            "together-llama-3-70b" => "Llama 3 70B (Together)",
            "together-llama-3-8b" => "Llama 3 8B (Together)",
            "together-mixtral-8x7b" => "Mixtral 8x7B (Together)",
            "together-qwen-2-72b" => "Qwen 2 72B (Together)",

            _ => modelName
        };
    }
}
