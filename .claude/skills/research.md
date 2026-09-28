---
name: research
description: Research .NET MAUI patterns, APIs, and project architecture using CodeGraph-first exploration
triggers:
  - "MAUI pattern"
  - "platform-specific"
  - "handler"
  - "how does MAUI"
  - "how does this work"
  - "find where"
  - "where is"
  - "how is this"
  - "what calls"
  - "who uses"
---

# Research Skill — CodeGraph-First Code & MAUI Exploration

## Steps

1. **CodeGraph first**: Before any grep or file reading, query `codegraph_explore` with the relevant symbol names, file names, or natural-language question. One call returns verbatim line-numbered source + call paths + blast radius — including dynamic dispatch hops (DI resolution, data bindings, event handlers) that grep can't follow.

2. **If CodeGraph doesn't cover it** (external APIs, MAUI framework questions): Use web search for official Microsoft MAUI documentation. Target .NET 10 / .NET MAUI — avoid Xamarin.Forms or older .NET MAUI patterns.

3. **Check existing project patterns**: Before recommending a solution, verify whether the project already has a similar pattern:
   - Controls: `MoneyRecord/Controls/`
   - Platform handlers: `MoneyRecord/Platforms/{Android,iOS}/Handlers/`
   - Converters: `MoneyRecord/Converters/Converters.cs`
   - Behaviors: `MoneyRecord/Behaviors/`
   - Services: `MoneyRecord/Services/` and `MoneyRecord/Services/Interfaces/`

4. **Recommend solutions** consistent with:
   - .NET 10 target framework
   - CommunityToolkit.Mvvm source generators ([ObservableProperty], [RelayCommand])
   - Existing DI registration pattern in `MauiProgram.cs` (singletons for services, transient for pages/VMs)
   - Shell navigation pattern
   - Localization via `{ext:Localize}` markup extension
   - Material Design Icons font for icon codes

5. **Report findings** with file paths, line numbers, and code snippets. Include blast radius (what depends on the investigated code) when relevant to the question.
