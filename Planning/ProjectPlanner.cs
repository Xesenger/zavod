using System;
using zavod.Lead;

namespace zavod.Planning;

public static class ProjectPlanner
{
    public static PlanningResult Plan(LeadResult leadResult)
    {
        ArgumentNullException.ThrowIfNull(leadResult);

        return leadResult.Mode switch
        {
            LeadMode.ColdStart => new PlanningResult(
                NextAction.EnterBootstrapFlow,
                "Следующий допустимый сценарий: войти в bootstrap flow."),

            LeadMode.Idle => new PlanningResult(
                NextAction.StayIdle,
                "Следующий допустимый сценарий: остаться в idle состоянии."),

            LeadMode.ActiveWork => new PlanningResult(
                NextAction.ResumeActiveShift,
                "Следующий допустимый сценарий: продолжить активную смену."),

            _ => throw new ArgumentOutOfRangeException(nameof(leadResult), leadResult.Mode, "Unsupported lead mode.")
        };
    }
}
