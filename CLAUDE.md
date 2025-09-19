# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

AI Agent Orchestra is a unified platform for orchestrating multiple AI coding assistants. It's built as a framework-first .NET 9.0 solution with clean architecture principles, using ASP.NET Core for the API and React for the web dashboard.

## Essential Commands

### Build and Run
```bash
# Build entire solution
dotnet build AI-Agent-Orchestra.sln

# Run API (from project root)
cd src/Orchestra.API
dotnet run

# Run API with specific profile
dotnet run --launch-profile http

# Run tests
dotnet test src/Orchestra.Tests/
```

### Development Scripts
```bash
# Start orchestrator service
pwsh scripts/start-orchestrator.ps1

# Test API endpoints
pwsh scripts/test-api.ps1
# or
bash scripts/test-api.sh
```

## Architecture Overview

### Solution Structure
- **Orchestra.API** - ASP.NET Core Web API with SignalR hubs
- **Orchestra.Core** - Domain logic, entities, and shared interfaces
- **Orchestra.Agents** - Agent connector implementations
- **Orchestra.Web** - React dashboard (planned)
- **Orchestra.Tests** - Unit and integration tests
- **Orchestra.CLI** - Command-line interface (future)

### Key Architectural Patterns

**Mediator Pattern**: All business operations go through IGameMediator using Command/Query pattern. This is the central architectural principle.

**Command/Query Separation**:
- Commands: `{Action}{Entity}Command` (e.g., `PurchaseUpgradeCommand`)
- Handlers: `{CommandName}Handler` (e.g., `PurchaseUpgradeCommandHandler`)
- Events: `{Entity}{Action}Event` (e.g., `UpgradePurchasedEvent`)

**Framework-First Approach**: Built as an extensible framework for multiple AI agent types, not just a single application.

### Technology Stack
- **.NET 9.0** with nullable reference types enabled
- **Entity Framework Core** with SQLite (dev) / PostgreSQL (prod)
- **ASP.NET Core** with OpenAPI/Swagger
- **SignalR** for real-time communication
- **MediatR** for CQRS implementation (planned)

## Development Guidelines

### Code Style (Critical)
- **Mandatory braces**: All block statements (if, for, while, etc.) MUST use braces even for single statements
- **C# naming**: PascalCase for public members, camelCase for private fields
- **XML documentation**: All public APIs must have XML comments in Russian
- **No inline statements**: Always use proper block formatting

### Architectural Rules
- **No direct service calls**: Always use Mediator pattern
- **Command-driven**: Every business operation = separate command
- **Event-driven**: Use events for component communication
- **Test-driven**: Create handler tests for every command
- **Code-first**: Avoid Inspector-based Unity patterns

### Database
- Use Entity Framework Core with Code-First migrations
- SQLite for development, PostgreSQL for production
- All dates in UTC timezone
- Repository pattern through Mediator

## Important Files and Directories

### Configuration
- `.cursor/rules/main.mdc` - Core architectural rules and patterns
- `.cursor/rules/csharp-codestyle.mdc` - C# formatting standards
- `AI-Agent-Orchestra.sln` - Main solution file

### Core Components
- `src/Orchestra.Core/` - Domain models and interfaces
- `src/Orchestra.Core/Data/` - Entity Framework context and entities

### Scripts
- `scripts/test-api.ps1` - API testing automation
- `scripts/start-orchestrator.ps1` - Service startup

## Testing Strategy

- **Framework**: xUnit
- **Pattern**: One test class per command handler
- **Naming**: `{TestedMethod}_{TestScenario}()` format
- **Approach**: Black-box testing focusing on results, not internal state
- Run tests with: `dotnet test src/Orchestra.Tests/`

## LLM Integration Notes

This codebase is designed for LLM-friendly development:
- Predictable patterns over code brevity
- Uniform structure across all components
- Clear separation of responsibilities
- Standardized naming conventions
- Mediator pattern for consistent interaction model

When adding new features, always follow the Command + Handler + Event + Tests pattern through the Mediator framework.

## Claude Code Sub-Agents (Армия Ревьюеров)

### Primary Review Agents
- **work-plan-reviewer** - Reviews and validates work plans created by work-plan-architect agent
- **code-style-reviewer** - Reviews code for adherence to project code style rules (.cursor/rules/*.mdc)
- **code-principles-reviewer** - Reviews code for SOLID, DRY, KISS principles adherence

### Supporting Review Infrastructure
- **common-plan-reviewer** (rule system) - Systematic review rules for work plans
- **systematic-review** (rule system) - Systematic review methodology
- **PlanStructureValidator.ps1** - Automated plan structure validation
- **AutomatedReviewSystem.ps1** - Automated review system
- **QuickReviewFix.ps1** - Express automated corrections

### Special Validators (NOT part of армии ревьюеров)
- **pre-completion-validator** - Validates task completion against original requirements

### Planned Agents
- **testing-specialist** (mentioned in architecture, not implemented)
- **code-reviewer** (mentioned in architecture, not implemented)

**Note**: All agents except pre-completion-validator are considered part of "армии ревьюеров" (army of reviewers).