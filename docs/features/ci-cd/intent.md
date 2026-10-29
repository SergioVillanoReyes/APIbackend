# Integración Continua y Agentes de Desarrollo: Intención

## Estado inicial de la solución

La solución `APIbackend.slnx` implementa Clean Architecture sobre .NET 10 (`net10.0`) con una funcionalidad completa de autenticación JWT persistida en SQL Server.

Hasta el momento, la restauración de dependencias, compilación, auditoría de seguridad y ejecución de pruebas (unitarias y de integración) se realiza de forma manual en la estación de trabajo del desarrollador. No existe un pipeline automatizado que valide los cambios al integrarse en la rama `main` ni en solicitudes de extracción (Pull Requests).

Asimismo, en el repositorio existen archivos marcadores de posición vacíos en `.github/` (`copilot-instructions.md`, `dotnet.instructions.md`, `planner.agent.md`, `reviewer.agent.md`), lo que deja el flujo asistido por agentes sin directrices formales estandarizadas en las herramientas de desarrollo.

## Objetivo

1. **Automatizar la Integración Continua (CI):** Implementar un pipeline en GitHub Actions que compile la solución en configuración `Release`, ejecute la suite de pruebas unitarias y la suite de pruebas de integración con SQL Server (mediante Testcontainers y Docker) en un entorno reproducible y aislado, aplicando el principio de mínimo privilegio (`contents: read`).
2. **Estandarizar los Agentes de Desarrollo:** Definir las especificaciones y prompts de sistema para los roles `planner` y `reviewer` dentro de `.github/agents/`, guiando a los asistentes de inteligencia artificial (como GitHub Copilot) a respetar rigurosamente la metodología Spec-Driven Development (SDD), Clean Architecture y las políticas de seguridad de [AGENTS.md](../../AGENTS.md).

## Alcance de esta funcionalidad

- **Workflow de CI (`.github/workflows/ci.yml`):**
  - Disparadores en `push` a la rama `main` y en `pull_request` dirigidos a `main`.
  - Configuración del SDK de .NET 10 (`10.0.x`).
  - Restauración y compilación de la solución `APIbackend.slnx` en modo `Release`.
  - Ejecución de pruebas unitarias en `tests/Application.Tests/`.
  - Ejecución de pruebas de integración en `tests/WebApi.IntegrationTests/` aprovechando el demonio Docker nativo de GitHub Actions para iniciar el contenedor de SQL Server 2022 vía Testcontainers.
  - Auditoría de paquetes vulnerables (`dotnet list package --vulnerable --include-transitive`).
  - Permisos estrictos de solo lectura en el token del workflow (`permissions: contents: read`).
- **Definición de Agentes en `.github/`:**
  - `planner.agent.md`: Agente enfocado en analizar solicitudes, consultar contexto existente y elaborar documentos SDD (`intent.md`, `spec.md`, `plan.md`) sin generar código de producción.
  - `reviewer.agent.md`: Agente enfocado en auditar cambios frente a la especificación aprobada, verificar Clean Architecture, validar cobertura de pruebas y certificar la ausencia de secretos o credenciales.
  - `copilot-instructions.md`: Instrucciones generales del repositorio que vinculan a Copilot con las directrices de `AGENTS.md`.
  - `dotnet.instructions.md`: Convenciones de diseño y código observadas en C# y .NET 10.
- **Documentación SDD completa:**
  - Creación de `intent.md`, `spec.md`, `plan.md`, `map.md` y `README.md` en `docs/features/ci-cd/`.

## Fuera de alcance

- Despliegue continuo (CD) y publicación de artefactos/contenedores en registros públicos o privados.
- Aprovisionamiento de infraestructura de nube o entornos de staging/producción.
- Ejecución autónoma de modelos de lenguaje o agentes de Copilot dentro de los runners de GitHub Actions (los agentes operan en el entorno de desarrollo del programador).
- Modificación de código fuente en `src/` o de las pruebas existentes en `tests/`.

## Principios de seguridad

- **Mínimo privilegio:** El workflow no posee permisos de escritura, administración ni acceso a secretos corporativos. El token por defecto se restringe a lectura del código fuente (`contents: read`).
- **Cero secretos requeridos:** Las pruebas de integración se ejecutan de manera autónoma con Testcontainers generando credenciales y puertos aleatorios y efímeros; el CI no requiere ni almacena cadenas de conexión ni credenciales reales de base de datos.
- **Auditoría continua de dependencias:** Detección temprana en cada PR de paquetes transitivos con vulnerabilidades conocidas reportadas en NuGet.

