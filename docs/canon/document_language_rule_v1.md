# ZAVOD v1 - Document Language Rule

## Purpose

`Document Language Rule v1`
- фиксирует правило языка документации проекта
- определяет связь между языком пользователя и языком документации
- отделяет language policy от layer policy

## Rule

- язык документации определяется языком пользователя проекта
- система должна вести текстовые представления проекта на языке пользователя
- агент должен подстраиваться под язык пользователя при работе с документацией проекта

## Scope

Правило распространяется на:

- `ProjectState` text representations
- `Shifts`
- `Snapshots`
- `Decisions`
- `Tasks`
- `Capsule`
- `Direction`
- `Roadmap`

## Layer Separation

- язык документа не определяет его слой
- язык документа не является признаком product truth
- product truth определяется только расположением файла
- Product Canon lives under `docs/canon/*`
- Managed Project Truth lives under `<managed repo>/.zavod/project/*`
- Product Canon and Managed Project Truth must not be treated as the same layer

## Canons

- documentation language follows user language
- system must adapt documentation output to user language
- agent must adapt documentation work to user language
- layer classification must not depend on document language
- product truth status must not be inferred from language

## Exclusions

- automatic translation of existing documents
- forced single-language policy for all projects
- language-based reclassification of documents
