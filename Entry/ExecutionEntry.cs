using System;
using zavod.Presentation;

namespace zavod.Entry;

public static class ExecutionEntry
{
    public static ExecutionIntent Enter(ScenarioPresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);

        return presentation.PrimaryAction switch
        {
            PrimaryAction.StartBootstrap => ExecutionIntent.StartBootstrapFlow,
            PrimaryAction.StayIdle => ExecutionIntent.StayIdle,
            PrimaryAction.ResumeActiveShift => ExecutionIntent.ResumeActiveShift,
            _ => throw new ArgumentOutOfRangeException(nameof(presentation), presentation.PrimaryAction, "Unsupported primary action.")
        };
    }
}
