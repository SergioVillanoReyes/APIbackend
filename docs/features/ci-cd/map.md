# Mapa de Integración Continua y Flujo Asistido por Agentes

Este documento describe la interacción entre el desarrollador, los agentes de desarrollo en el editor y el pipeline automatizado de GitHub Actions.

## Ciclo de vida del cambio (SDD + Agentes + CI)

```text
               FASE 1: PLANIFICACIÓN Y DISEÑO
            +------------------------------------+
            | Usuario solicita nueva función     |
            +-----------------+------------------+
                              |
                              v
            +------------------------------------+
            | Agente Planner (.agent.md)        |
            | - Consulta AGENTS.md y docs/       |
            | - Elabora intent, spec y plan      |
            | - (NO escribe código de producción)|
            +-----------------+------------------+
                              |
                              v
            +------------------------------------+
            | Usuario aprueba la especificación  |
            +-----------------+------------------+
                              |
               FASE 2: IMPLEMENTACIÓN LOCAL
                              v
            +------------------------------------+
            | Desarrollador / Agente implementa  |
            | - Pasos pequeños en código C#      |
            | - Pruebas unitarias e integración  |
            +-----------------+------------------+
                              |
                              v
            +------------------------------------+
            | Agente Reviewer (.agent.md)        |
            | - Audita contra spec.md            |
            | - Valida Clean Architecture        |
            | - Verifica ausencia de secretos    |
            | - Reporta hallazgos con líneas     |
            +-----------------+------------------+
                              |
               FASE 3: INTEGRACIÓN AUTOMATIZADA
                              v
            +------------------------------------+
            | Git Push / Pull Request a 'main'   |
            +-----------------+------------------+
                              |
                              v
            +------------------------------------+
            | GitHub Actions Runner (ubuntu)     |
            | 1. actions/checkout@v4             |
            | 2. actions/setup-dotnet@v4 (.NET10)|
            | 3. dotnet restore                  |
            | 4. dotnet build -c Release         |
            | 5. dotnet test (Unit Tests)        |
            | 6. dotnet test (Testcontainers MsSql)|
            | 7. dotnet list package --vulnerable|
            +-----------------+------------------+
                              |
                    [Éxito / Falla]
                              v
            +------------------------------------+
            | Merge a main protegido             |
            +------------------------------------+
```

## Estructura y flujo del Workflow de CI (`.github/workflows/ci.yml`)

```text
[Evento: push a main / PR a main]
        |
        v
[Job: build-and-test en ubuntu-latest]
  (Permiso: contents: read)
        |
        ├── Paso 1: Checkout del código fuente
        |
        ├── Paso 2: Instalación del SDK .NET 10 (10.0.x)
        |
        ├── Paso 3: dotnet restore APIbackend.slnx
        |
        ├── Paso 4: dotnet build APIbackend.slnx -c Release --no-restore
        |
        ├── Paso 5: dotnet test Application.Tests (Unitarias, en memoria)
        |
        ├── Paso 6: dotnet test WebApi.IntegrationTests
        |           ├── Se conecta al Docker daemon local (/var/run/docker.sock)
        |           ├── Inicia contenedor SQL Server 2022 efímero
        |           ├── Aplica migraciones EF Core
        |           └── Valida endpoints HTTP reales (/register, /login, /me)
        |
        └── Paso 7: Auditoría de vulnerabilidades transitivas
```

## Delimitación de responsabilidades

| Componente | Rol en el flujo | ¿Dónde se ejecuta? |
|---|---|---|
| **`AGENTS.md`** | Constitución de gobernanza y directrices del repositorio. | Raíz del proyecto. |
| **Agente Planner** | Elabora la intención, especificación y plan técnico SDD. | Editor local (Copilot / Asistente). |
| **Agente Reviewer** | Audita el código implementado contra la especificación y seguridad. | Editor local (Copilot / Asistente). |
| **GitHub Actions CI** | Verificación técnica determinista, compilación y pruebas automatizadas. | Nube de GitHub (Runners aislados). |

