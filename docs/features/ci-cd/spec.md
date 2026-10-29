# Integración Continua y Agentes de Desarrollo: Especificación Técnica

## Estado

Especificación aprobada. Las directrices de gobernanza se fundamentan en [AGENTS.md](../../AGENTS.md) y las decisiones de motivación en [`intent.md`](./intent.md).

## Requisitos funcionales

### Pipeline de Integración Continua (GitHub Actions)

1. **Disparadores (Triggers):**
   - El workflow debe ejecutarse ante cualquier `push` a la rama `main`.
   - El workflow debe ejecutarse ante cualquier `pull_request` con destino a la rama `main`.
2. **Seguridad y Permisos:**
   - Debe declarar explícitamente `permissions: contents: read` en el nivel raíz del workflow o del job.
3. **Entorno de Ejecución:**
   - Debe ejecutarse en el runner oficial `ubuntu-latest`.
   - Debe utilizar `actions/setup-dotnet@v4` para instalar el SDK de .NET 10 (`dotnet-version: '10.0.x'`).
4. **Ciclo de Construcción:**
   - Ejecutar `dotnet restore APIbackend.slnx`.
   - Ejecutar `dotnet build APIbackend.slnx -c Release --no-restore`.
5. **Ejecución de Pruebas:**
   - Ejecutar `dotnet test tests/Application.Tests/Application.Tests.csproj -c Release --no-build --verbosity normal`.
   - Ejecutar `dotnet test tests/WebApi.IntegrationTests/WebApi.IntegrationTests.csproj -c Release --no-build --verbosity normal`, garantizando que el socket Docker del runner `/var/run/docker.sock` esté accesible para Testcontainers.
6. **Auditoría de Seguridad:**
   - Ejecutar `dotnet list APIbackend.slnx package --vulnerable --include-transitive` como paso de control de dependencias.

### Agente Planner (`.github/agents/planner.agent.md`)

1. **Objetivo:** Analizar requerimientos y generar la documentación preliminar de diseño SDD (`intent.md`, `spec.md`, `plan.md`) en `docs/features/<feature>/`.
2. **Entradas:** Solicitud del usuario, archivo `AGENTS.md`, especificaciones existentes y estructura actual de la solución.
3. **Prohibiciones estrictas:** Prohibido generar código de producción en C#, editar archivos en `src/` o `tests/`, o aplicar migraciones a bases de datos.
4. **Salida esperada:** Propuesta documental estructurada y solicitud de aprobación explícita al usuario antes de proceder a la fase de implementación.

### Agente Reviewer (`.github/agents/reviewer.agent.md`)

1. **Objetivo:** Auditar cambios de código propuestos en pull requests o diffs locales contra la especificación técnica aprobada.
2. **Criterios de auditoría:**
   - Cumplimiento estricto de las fronteras de Clean Architecture (Domain puro, Application solo conoce Domain, etc.).
   - Cobertura de pruebas (unitarias para lógica de aplicación, integración con Testcontainers para persistencia y HTTP).
   - Ausencia absoluta de credenciales o secretos en código o configuración versionada.
   - Manejo adecuado de excepciones mediante `ProblemDetails` y tiempo constante en operaciones criptográficas.
3. **Prohibiciones estrictas:** Prohibido modificar código de forma autónoma durante la revisión.
4. **Salida esperada:** Reporte detallado de hallazgos clasificados por severidad (Bloqueante, Advertencia, Sugerencia) con referencias a archivos y números de línea.

## Requisitos no funcionales

- **Determinismo y Repetibilidad:** Las ejecuciones en CI deben ser independientes del estado previo del runner; cada ejecución inicia en un entorno limpio.
- **Aislamiento de Recursos:** Los contenedores de SQL Server iniciados por Testcontainers deben disponerse al finalizar cada prueba mediante `IAsyncLifetime`.
- **Compatibilidad con Editores:** Los archivos de agentes en `.github/` deben adherirse al formato estándar de GitHub Copilot (Markdown con frontmatter YAML).

## Escenarios de aceptación

1. **CA-01 (Activación de Workflow):** Dado un commit enviado a `main` o un PR abierto hacia `main`, el workflow `CI Pipeline` se dispara automáticamente.
2. **CA-02 (Compilación exitosa en Release):** Dado el código fuente actual de la solución, el pipeline restaura dependencias y compila `APIbackend.slnx` en modo `Release` sin errores.
3. **CA-03 (Pruebas unitarias):** La suite `Application.Tests` se ejecuta en el runner y las 6 pruebas pasan satisfactoriamente.
4. **CA-04 (Pruebas de integración con Docker):** La suite `WebApi.IntegrationTests` descarga la imagen oficial de SQL Server 2022 en el runner de GitHub Actions, ejecuta las migraciones efímeras y completa todas las pruebas de extremo a extremo sin intervención manual ni secretos externos.
5. **CA-05 (Auditoría de paquetes):** El comando `dotnet list package --vulnerable --include-transitive` se ejecuta en el pipeline y no detecta paquetes con advertencias de seguridad.
6. **CA-06 (Conformidad del Agente Planner):** El archivo `.github/agents/planner.agent.md` contiene las restricciones operativas de planificación sin edición de código.
7. **CA-07 (Conformidad del Agente Reviewer):** El archivo `.github/agents/reviewer.agent.md` contiene la lista de verificación completa de Clean Architecture, seguridad y formato de reporte de hallazgos.

