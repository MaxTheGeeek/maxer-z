using System.Threading;
using System.Threading.Tasks;
using MaxerZ.Api.Models;

namespace MaxerZ.Api.Services
{
    public class AtsService
    {
        private readonly LlmOrchestrator _llm;

        public AtsService(LlmOrchestrator llm)
        {
            _llm = llm;
        }

        public async Task<AtsResult> AnalyzeAsync(AtsRequest req, CancellationToken ct = default)
        {
            return await _llm.ExecuteAtsPipelineAsync(req, ct);
        }
    }
}
