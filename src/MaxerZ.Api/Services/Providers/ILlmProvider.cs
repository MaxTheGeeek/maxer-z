using System;
using System.Threading;
using System.Threading.Tasks;
using MaxerZ.Api.Models;

namespace MaxerZ.Api.Services.Providers
{
    public interface ILlmProvider
    {
        /// <summary>
        /// Unique identifier for this provider: "openrouter" | "groq" | "ollama"
        /// </summary>
        string ProviderId { get; }

        /// <summary>
        /// True if this provider has valid credentials and should be included in the chain.
        /// AI: call this before any attempt. Skip provider if false.
        /// </summary>
        bool IsConfigured(AppSettings settings);

        /// <summary>
        /// Attempt to complete the prompt.
        /// Returns (response, modelUsed) on success.
        /// Throws ProviderExhaustedException if token limit hit — triggers fallback.
        /// Throws ProviderUnavailableException for network/auth errors — triggers fallback.
        /// NEVER throws for content/parsing issues — return partial result instead.
        /// </summary>
        Task<(string response, string modelUsed)> CompleteAsync(
            string prompt,
            AppSettings settings,
            CancellationToken ct);
    }

    /// <summary>
    /// Thrown when a provider signals token/rate limit exceeded.
    /// LlmOrchestrator catches this and moves to next provider immediately.
    /// </summary>
    public class ProviderExhaustedException : Exception
    {
        public string Provider { get; }
        public string Model { get; }
        public ProviderExhaustedException(string provider, string model, string reason)
            : base(reason) { Provider = provider; Model = model; }
    }

    /// <summary>
    /// Thrown when a provider is unreachable or returns auth error.
    /// </summary>
    public class ProviderUnavailableException : Exception
    {
        public string Provider { get; }
        public ProviderUnavailableException(string provider, string reason)
            : base(reason) { Provider = provider; }
    }
}
