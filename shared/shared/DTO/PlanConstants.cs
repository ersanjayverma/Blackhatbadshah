namespace shared.Dto;

/// <summary>
/// Centralized plan and model tier constants to avoid magic strings across the codebase
/// </summary>
public static class PlanConstants
{
    /// <summary>
    /// Plan name constants
    /// </summary>
    public static class Plans
    {
        public const string Free = "Free";
        public const string Pro = "Pro";
        public const string Enterprise = "Enterprise";

        public static readonly string[] All = { Free, Pro, Enterprise };

        public static bool IsValid(string planName) =>
            planName == Free || planName == Pro || planName == Enterprise;

        public static bool IsPaid(string planName) =>
            planName == Pro || planName == Enterprise;
    }

    /// <summary>
    /// Model tier constants - defines which models belong to Pro vs Open tiers
    /// </summary>
    public static class ModelTiers
    {
        /// <summary>
        /// Pro models - require Pro or Enterprise subscription
        /// </summary>
        public static readonly string[] ProModels =
        {
            Models.ClaudeOpus45,
            Models.ClaudeSonnet45,
            Models.ClaudeSonnet35,
            Models.Gpt4o,
            Models.Gpt4Turbo
        };

        /// <summary>
        /// Open models - available for Free tier users
        /// </summary>
        public static readonly string[] OpenModels =
        {
            Models.GptOss120b,
            Models.TogetherLlama31405b,
            Models.TogetherMixtral8x7b,
            Models.TogetherQwen,
        };

        public static bool IsProModel(string model) => ProModels.Contains(model);
        public static bool IsOpenModel(string model) => OpenModels.Contains(model);
    }

    /// <summary>
    /// Model name constants - friendly names used across the system
    /// </summary>
    public static class Models
    {
        // Claude/Anthropic models
        public const string ClaudeOpus45 = "claude-opus-4-5";
        public const string ClaudeSonnet45 = "claude-sonnet-4-5";
        public const string ClaudeSonnet35 = "claude-sonnet-3-5";

        // OpenAI/GPT models
        public const string Gpt4o = "gpt-4o";
        public const string Gpt4Turbo = "gpt-4-turbo";
        public const string Gpt4TurboPreview = "gpt-4-turbo-preview";
        public const string Gpt35Turbo = "gpt-3.5-turbo";

        // TogetherAI models
        public const string GptOss120b = "gpt-oss-120b";
        public const string TogetherLlama31405b = "together-llama-3.1-405b";
        public const string TogetherMixtral8x7b = "together-mixtral-8x7b";
        public const string TogetherQwen = "together-qwen";

        // Default model
        public const string Default = ClaudeSonnet45;
        public const string ChartModel = TogetherQwen;
    }

    /// <summary>
    /// Model API identifiers - actual API model names
    /// </summary>
    public static class ModelApiIds
    {
        // Claude/Anthropic
        public const string ClaudeOpus45 = "claude-opus-4-5-20251101";
        public const string ClaudeSonnet45 = "claude-sonnet-4-5-20250929";
        public const string ClaudeSonnet35 = "claude-3-5-sonnet-20241022";

        // OpenAI
        public const string Gpt4o = "gpt-4o";
        public const string Gpt4Turbo = "gpt-4-turbo";
        public const string Gpt4TurboPreview = "gpt-4-turbo-preview";
        public const string Gpt35Turbo = "gpt-3.5-turbo";

        // TogetherAI
        public const string GptOss120b = "openai/gpt-oss-120b";
        public const string TogetherLlama31405b = "meta-llama/Meta-Llama-3.1-405B-Instruct-Turbo";
        public const string TogetherMixtral8x7b = "mistralai/Mixtral-8x7B-Instruct-v0.1";
        public const string TogetherQwen = "Qwen/Qwen3-Coder-480B-A35B-Instruct-FP8";
    }
}
