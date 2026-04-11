using System;
using zavod.Bootstrap;

namespace zavod.Lead;

public static class ProjectLead
{
    public static LeadResult Evaluate(BootstrapResult state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.IsColdStart)
        {
            return new LeadResult(
                LeadMode.ColdStart,
                "Проект находится в cold start: активная смена отсутствует.");
        }

        if (state.HasActiveShift)
        {
            return new LeadResult(
                LeadMode.ActiveWork,
                "Обнаружена активная смена: можно продолжать работу.");
        }

        return new LeadResult(
            LeadMode.Idle,
            "Проект загружен: активная смена отсутствует.");
    }
}
