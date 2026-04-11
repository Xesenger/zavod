using System;
using System.Collections.Generic;
using zavod.Execution;
using zavod.Tooling;

namespace zavod.Demo;

public sealed record DemoScenarioSeed(
    IReadOnlyList<DemoStepScenario> Steps,
    string CompletionDraft)
{
    public static DemoScenarioSeed CreateV1()
    {
        return new DemoScenarioSeed(
            Steps: new[]
            {
                new DemoStepScenario(
                    ChatDraft: "делаем синюю кнопку в main.qml",
                    ClarifyDraft: "хочу тёмно-синюю",
                    RevisionDraft: "кнопка должна быть больше",
                    BaseAgreementItems: new[]
                    {
                        "сделать синюю кнопку в main.qml",
                        "обновить визуальное состояние",
                        "не трогать другие файлы"
                    },
                    ClarifiedAgreementItems: new[]
                    {
                        "сделать синюю кнопку в main.qml",
                        "сделать кнопку тёмно-синей и обновить визуальное состояние",
                        "не трогать другие файлы"
                    },
                    WorkCycles: new[]
                    {
                        new DemoWorkCycleSeed(
                            new[]
                            {
                                "работаю с main.qml",
                                "обновляю кнопку",
                                "идёт проверка ОТК"
                            },
                            "Сделана синяя кнопка в `main.qml`.\n\n```diff\n- color: \"gray\"\n+ color: \"#1D4ED8\"\n```\n",
                            new[]
                            {
                                new WorkerExecutionModification("main.qml", "edit", "Сделан синий акцент кнопки."),
                                new WorkerExecutionModification("main.qml", "edit", "Обновлено визуальное состояние кнопки.")
                            }),
                        new DemoWorkCycleSeed(
                            new[]
                            {
                                "обновляю размер кнопки",
                                "проверяю итог после доработки",
                                "идёт проверка ОТК"
                            },
                            "Кнопка увеличена после доработки.\n\n```diff\n- width: 120\n+ width: 168\n```\n",
                            new[]
                            {
                                new WorkerExecutionModification("main.qml", "edit", "Увеличен размер кнопки."),
                                new WorkerExecutionModification("main.qml", "edit", "Проверено итоговое визуальное состояние.")
                            })
                    }),
                new DemoStepScenario(
                    ChatDraft: "добавим под кнопкой короткую подпись 'Start session'",
                    ClarifyDraft: "сделать подпись светло-серой и чуть меньше",
                    RevisionDraft: "подпись должна быть ближе к кнопке",
                    BaseAgreementItems: new[]
                    {
                        "добавить текстовую подпись под кнопкой в main.qml",
                        "выдержать текущий стиль экрана",
                        "не менять логику кнопки"
                    },
                    ClarifiedAgreementItems: new[]
                    {
                        "добавить текстовую подпись под кнопкой в main.qml",
                        "сделать подпись светло-серой и чуть меньше, сохранив стиль экрана",
                        "не менять логику кнопки"
                    },
                    WorkCycles: new[]
                    {
                        new DemoWorkCycleSeed(
                            new[]
                            {
                                "обновляю layout в main.qml",
                                "проверяю выравнивание подписи",
                                "ОТК проверяет визуальную иерархию"
                            },
                            "Добавлена подпись `Start session` под кнопкой.\n\n```diff\n+ Text {\n+     text: \"Start session\"\n+ }\n```\n",
                            new[]
                            {
                                new WorkerExecutionModification("main.qml", "edit", "Добавлена подпись под основной кнопкой.")
                            }),
                        new DemoWorkCycleSeed(
                            new[]
                            {
                                "подвигаю подпись ближе к кнопке",
                                "проверяю отступы после доработки",
                                "ОТК повторно проверяет композицию"
                            },
                            "Подпись `Start session` сдвинута ближе к кнопке.\n\n```diff\n- anchors.topMargin: 12\n+ anchors.topMargin: 6\n```\n",
                            new[]
                            {
                                new WorkerExecutionModification("main.qml", "edit", "Скорректирован отступ между кнопкой и подписью.")
                            })
                    })
            },
            CompletionDraft: "demo завершено");
    }
}

public sealed record DemoStepScenario(
    string ChatDraft,
    string ClarifyDraft,
    string RevisionDraft,
    IReadOnlyList<string> BaseAgreementItems,
    IReadOnlyList<string> ClarifiedAgreementItems,
    IReadOnlyList<DemoWorkCycleSeed> WorkCycles)
{
    public IReadOnlyList<string> BuildAgreementItems(string? clarification)
    {
        return string.IsNullOrWhiteSpace(clarification)
            ? BaseAgreementItems
            : ClarifiedAgreementItems;
    }

    public DemoWorkCycleSeed GetCycle(int cycleIndex)
    {
        if (WorkCycles.Count == 0)
        {
            throw new InvalidOperationException("Demo step must define at least one work cycle.");
        }

        var boundedIndex = Math.Clamp(cycleIndex, 0, WorkCycles.Count - 1);
        return WorkCycles[boundedIndex];
    }

    public WorkerExecutionResult BuildResult(string taskId, int cycleIndex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);

        var cycle = GetCycle(cycleIndex);
        return new WorkerExecutionResult(
            $"DEMO-RESULT-{taskId}-{cycleIndex + 1:D2}",
            taskId,
            WorkerExecutionStatus.Success,
            cycle.ResultReport,
            Array.Empty<IntakeArtifact>(),
            cycle.Modifications,
            Array.Empty<ToolWarning>());
    }
}

public sealed record DemoWorkCycleSeed(
    IReadOnlyList<string> WorkLogLines,
    string ResultReport,
    IReadOnlyList<WorkerExecutionModification> Modifications);
