namespace shared.Dto;

/// <summary>
/// Centralized model configuration with friendly names mapped to actual API model identifiers.
/// Uses PlanConstants for model tier classification to ensure consistency.
/// </summary>
public static class ModelMapping
{
    /// <summary>
    /// Claude/Anthropic models - maps friendly name to actual API model identifier
    /// </summary>
    public static readonly Dictionary<string, string> ClaudeModels = new()
    {
        { PlanConstants.Models.ClaudeSonnet45, PlanConstants.ModelApiIds.ClaudeSonnet45 },
        { PlanConstants.Models.ClaudeOpus45, PlanConstants.ModelApiIds.ClaudeOpus45 },
        { PlanConstants.Models.ClaudeSonnet35, PlanConstants.ModelApiIds.ClaudeSonnet35 }
    };

    /// <summary>
    /// OpenAI/GPT models - maps friendly name to actual API model identifier
    /// </summary>
    public static readonly Dictionary<string, string> GPTModels = new()
    {
        { PlanConstants.Models.Gpt4o, PlanConstants.ModelApiIds.Gpt4o },
        { PlanConstants.Models.Gpt4Turbo, PlanConstants.ModelApiIds.Gpt4Turbo },
        { PlanConstants.Models.Gpt4TurboPreview, PlanConstants.ModelApiIds.Gpt4TurboPreview },
        { PlanConstants.Models.Gpt35Turbo, PlanConstants.ModelApiIds.Gpt35Turbo }
    };

    /// <summary>
    /// TogetherAI models - maps friendly name to actual API model identifier
    /// </summary>
    public static readonly Dictionary<string, string> TogetherModels = new()
    {
        { PlanConstants.Models.TogetherLlama370b, PlanConstants.ModelApiIds.TogetherLlama370b },
        { PlanConstants.Models.TogetherLlama31405b, PlanConstants.ModelApiIds.TogetherLlama31405b },
        { PlanConstants.Models.TogetherMixtral8x7b, PlanConstants.ModelApiIds.TogetherMixtral8x7b },
        { PlanConstants.Models.TogetherQwen, PlanConstants.ModelApiIds.TogetherQwen }
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
    /// Pro models - require Pro or Enterprise subscription.
    /// Delegates to PlanConstants for single source of truth.
    /// </summary>
    public static IReadOnlyList<string> ProModels => PlanConstants.ModelTiers.ProModels;

    /// <summary>
    /// Open models - available for Free tier users.
    /// Delegates to PlanConstants for single source of truth.
    /// </summary>
    public static IReadOnlyList<string> OpenModels => PlanConstants.ModelTiers.OpenModels;

    /// <summary>
    /// Default model to use when none is specified
    /// </summary>
    public const string DefaultModel = PlanConstants.Models.Default;

    /// <summary>
    /// Checks if a model requires Pro/Enterprise subscription
    /// </summary>
    public static bool IsProModel(string modelName) => PlanConstants.ModelTiers.IsProModel(modelName);

    /// <summary>
    /// Checks if a model is available for Free tier users
    /// </summary>
    public static bool IsOpenModel(string modelName) => PlanConstants.ModelTiers.IsOpenModel(modelName);

    /// <summary>
    /// Checks if a model name is supported
    /// </summary>
    public static bool IsSupported(string modelName) => AllModels.ContainsKey(modelName);

    /// <summary>
    /// Gets the actual API model identifier for a friendly name
    /// </summary>
    public static string GetActualModelName(string friendlyName) =>
        AllModels.TryGetValue(friendlyName, out var actualName) ? actualName : friendlyName;

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
            PlanConstants.Models.ClaudeSonnet45 => "Claude Sonnet 4.5",
            PlanConstants.Models.ClaudeOpus45 => "Claude Opus 4.5",
            PlanConstants.Models.ClaudeSonnet35 => "Claude Sonnet 3.5",

            // OpenAI models
            PlanConstants.Models.Gpt4o => "GPT-4o",
            PlanConstants.Models.Gpt4Turbo => "GPT-4 Turbo",
            PlanConstants.Models.Gpt4TurboPreview => "GPT-4 Turbo Preview",
            PlanConstants.Models.Gpt35Turbo => "GPT-3.5 Turbo",

            // TogetherAI models
            PlanConstants.Models.TogetherLlama370b => "Llama 3 70B (Together)",
            PlanConstants.Models.TogetherLlama31405b => "Llama 3.1 405B (Together)",
            PlanConstants.Models.TogetherMixtral8x7b => "Mixtral 8x7B (Together)",
            PlanConstants.Models.TogetherQwen => "Qwen Coder 480B (Together)",

            _ => modelName
        };
    }
}
