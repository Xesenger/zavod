using System;
using zavod.Router;

namespace zavod.Presentation;

public static class ScenarioPresenter
{
    public static ScenarioPresentation Present(RouteResult routeResult)
    {
        ArgumentNullException.ThrowIfNull(routeResult);

        return routeResult.Scenario switch
        {
            Scenario.BootstrapScenario => new ScenarioPresentation(
                Scenario.BootstrapScenario,
                "Новый старт проекта",
                "Проект готов к bootstrap-сценарию и ожидает первый рабочий шаг.",
                "Начать",
                PrimaryAction.StartBootstrap),

            Scenario.IdleScenario => new ScenarioPresentation(
                Scenario.IdleScenario,
                "Проект в ожидании",
                "Проект загружен и сейчас находится в спокойном idle-состоянии.",
                "Остаться в ожидании",
                PrimaryAction.StayIdle),

            Scenario.ActiveShiftScenario => new ScenarioPresentation(
                Scenario.ActiveShiftScenario,
                "Продолжение активной смены",
                "Проект имеет активную смену и готов к продолжению работы.",
                "Продолжить",
                PrimaryAction.ResumeActiveShift),

            _ => throw new ArgumentOutOfRangeException(nameof(routeResult), routeResult.Scenario, "Unsupported route scenario.")
        };
    }
}
