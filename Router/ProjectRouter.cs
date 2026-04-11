using System;
using zavod.Planning;

namespace zavod.Router;

public static class ProjectRouter
{
    public static RouteResult Route(PlanningResult planningResult)
    {
        ArgumentNullException.ThrowIfNull(planningResult);

        return planningResult.NextAction switch
        {
            NextAction.EnterBootstrapFlow => new RouteResult(
                Scenario.BootstrapScenario,
                "Активный сценарий: bootstrap сценарий верхнего слоя."),

            NextAction.StayIdle => new RouteResult(
                Scenario.IdleScenario,
                "Активный сценарий: idle сценарий верхнего слоя."),

            NextAction.ResumeActiveShift => new RouteResult(
                Scenario.ActiveShiftScenario,
                "Активный сценарий: сценарий активной смены."),

            _ => throw new ArgumentOutOfRangeException(nameof(planningResult), planningResult.NextAction, "Unsupported planning action.")
        };
    }
}
