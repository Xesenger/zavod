using System;
using System.Linq;

namespace zavod.Retrieval;

public static class ScopedContextBuilder
{
    public static ScopedContext Build(RetrievalResult retrievalResult)
    {
        ArgumentNullException.ThrowIfNull(retrievalResult);

        var references = retrievalResult.Candidates
            .Select(candidate => new ScopedContextReference(
                candidate.SourceArtifactId,
                candidate.Reference,
                candidate.Preview ?? candidate.Reference))
            .ToArray();

        var snippets = retrievalResult.Candidates
            .Where(static candidate => !string.IsNullOrWhiteSpace(candidate.Preview))
            .Select(static candidate => new ScopedContextSnippet(candidate.CandidateId, candidate.Preview!))
            .ToArray();

        return new ScopedContext(
            retrievalResult.Candidates,
            references,
            snippets,
            retrievalResult.Summary);
    }
}
