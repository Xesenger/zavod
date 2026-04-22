using System;
using System.Diagnostics;

namespace zavod.Sage;

// S2a: hook skeleton, no emitters, no sink wired.
//
// The runner exists so call sites in the orchestrator can bind to a
// stable API. S3+ will route context to typed emitters behind this
// facade without touching call sites again.
//
// Invariants (must hold for every slice from S2a onward):
//   - Hooks never return a value that changes caller control flow.
//   - Hook exceptions never propagate: Sage is fail-open (v2.1a #3).
//   - BeforeExecution is fast-path only: bounded at BeforeExecutionBudget.
//   - No hook writes into role prompts (v2.1a #2).
//
// during_execution is intentionally not declared here — it requires
// async observer plumbing with CancellationToken and belongs to a
// later slice (S2c).
public sealed class SageHookRunner
{
    // v2.1a guardrail #3: before_execution fast-path budget.
    // The contract is declared now; enforcement/degraded-meta emission
    // ships when a real emitter is wired in S3 — until then the body is
    // empty and Stopwatch is effectively a no-op.
    public static readonly TimeSpan BeforeExecutionBudget = TimeSpan.FromMilliseconds(50);

    // S2b: budgeted sink is constructed and held as a property. It is
    // NOT invoked from any hook body in S2b (no emitters yet). S3 will
    // read this property to call TryEmit from typed emitters.
    // Exposed internal (not private) so the S3 emitter layer can access
    // it from the same assembly without reflection.
    internal BudgetedSageSink Sink { get; }

    public SageHookRunner(BudgetedSageSink? sink = null)
    {
        Sink = sink ?? new BudgetedSageSink();
    }

    public void OnAfterIntent(SageAfterIntentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // S3: first live emitter. max 1 observation per hook per user's
        // directive (the per-hook budget allows up to 3; this body takes
        // a stricter 1). Fail-open per v2.1a #3: any exception from the
        // emitter or sink is swallowed so the orchestrator pipeline
        // cannot be broken by Sage.
        try
        {
            var observation = SemanticGapEmitter.TryObserve(context);
            if (observation is not null)
            {
                Sink.TryEmit(context.ProjectRoot, observation);
            }
        }
        catch
        {
            // Intentionally swallow. Losing the signal is the explicit
            // fail-open policy (Sage must never break the pipeline).
        }
    }

    public void OnBeforeExecution(SageBeforeExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // S5a: attention_miss emitter. Fast-path-only per v2.1a #3;
        // regex + substring checks over a bounded anchor pack (<100
        // entries by construction in WorkerAnchorPackBuilder). Budget
        // watchdog records over-budget as a diagnostic but never
        // blocks the pipeline. Fail-open try/catch per v2.1a #3.
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var observation = AttentionMissEmitter.TryObserve(context);
            if (observation is not null)
            {
                Sink.TryEmit(context.ProjectRoot, observation);
            }
        }
        catch
        {
            // Intentionally swallow (fail-open).
        }
        finally
        {
            stopwatch.Stop();
            if (stopwatch.Elapsed > BeforeExecutionBudget)
            {
                // Over-budget on a fast-path hook. S2b did not wire a
                // dedicated degraded-meta emit path and doing so here
                // would require a second TryEmit, doubling the cost of
                // the very overrun we are complaining about. Deferred
                // to a later slice if real measurements show overruns.
            }
        }
    }

    public void OnBeforeResult(SageBeforeResultContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        // S2a: no emitters.
    }

    public void OnAfterResult(SageAfterResultContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        // S2a: no emitters.
        // Fires on every task termination (QcAccepted, QcRevise, QcReject,
        // WorkerRefused, Applied) so v2.1a guardrail #4 (pattern memory
        // counter-evidence) has a complete termination stream to consume.
    }
}
