# Guía Operativa: CI/CD y Agentes de Desarrollo

Esta guía documenta el uso del pipeline de integración continua y la operación de los agentes `planner` y `reviewer`.

## 1. Funcionamiento del Pipeline de CI

El archivo `.github/workflows/ci.yml` automatiza la verificación de la solución ante cada `push` o `pull_request` contra la rama `main`.

### Pasos ejecutados en GitHub Actions
1. **Descarga del código:** Usa `actions/checkout@v4`.
2. **Configuración de .NET:** Instala .NET 10 mediante `actions/setup-dotnet@v4`.
3. **Restauración y Compilación:**
   ```bash
   dotnet restore APIbackend.slnx
   dotnet build APIbackend.slnx -c Release --no-restore
   ```
4. **Ejecución de Pruebas Unitarias:**
   ```bash
   dotnet test tests/Application.Tests/Application.Tests.csproj -c Release --no-build
   ```
5. **Ejecución de Pruebas de Integración:**
   ```bash
   dotnet test tests/WebApi.IntegrationTests/WebApi.IntegrationTests.csproj -c Release --no-build
   ```
   *Nota:* El runner utiliza su propio entorno Docker para instanciar SQL Server 2022 con Testcontainers. No se requieren secretos ni servicios externos.
6. **Auditoría de Vulnerabilidades:**
   ```bash
   dotnet list APIbackend.slnx package --vulnerable --include-transitive
   ```

### Cómo reproducir el pipeline localmente
Para asegurarte de que tus cambios pasarán el CI antes de hacer `push`, ejecuta en tu terminal:

```bash
dotnet restore APIbackend.slnx
dotnet build APIbackend.slnx -c Release --no-restore
dotnet test tests/Application.Tests/Application.Tests.csproj -c Release --no-build
# Si tienes Docker activo localmente:
dotnet test tests/WebApi.IntegrationTests/WebApi.IntegrationTests.csproj -c Release --no-build
dotnet list APIbackend.slnx package --vulnerable --include-transitive
```

---

## 2. Operación de los Agentes de Desarrollo

Los agentes definidos en `.github/agents/` asisten al desarrollador en el flujo SDD:

### Agente Planner (`.github/agents/planner.agent.md`)
- **Cuándo usarlo:** Al concebir una nueva funcionalidad o un cambio que modifique la arquitectura o los contratos de la API.
- **Cómo usarlo:**
  En GitHub Copilot Chat o tu asistente de código, invoca al agente indicando la funcionalidad deseada:
  > *"Actúa como el agente planner y analiza la incorporación de la funcionalidad [NombreFuncionalidad] siguiendo SDD."*
- **Qué esperar:** El planner entregará los borradores de `intent.md`, `spec.md` y `plan.md` listos para tu revisión y aprobación, sin tocar código.

### Agente Reviewer (`.github/agents/reviewer.agent.md`)
- **Cuándo usarlo:** Antes de crear un Pull Request o hacer merge a `main`.
- **Cómo usarlo:**
  > *"Actúa como el agente reviewer y audita los cambios actuales frente a la especificación en docs/features/[nombre] y AGENTS.md."*
- **Qué esperar:** Un reporte con lista de hallazgos clasificados por severidad (Bloqueante, Advertencia, Sugerencia) con referencias exactas a archivos y líneas.

---

## 3. Diagnóstico de Errores Comunes en CI

- **Error de SDK .NET 10:** Si `actions/setup-dotnet` no encuentra la versión `10.0.x`, se puede habilitar `dotnet-quality: 'preview'` en el workflow.
- **Fallo en Testcontainers:** Si las pruebas de integración fallan por conectividad a Docker en el runner, comprueba que el paso no redefina sockets o variables incompatibles con el runner `ubuntu-latest`.
- **Vulnerabilidad detectada en paquetes:** Si `dotnet list package --vulnerable` falla, actualiza explícitamente el paquete transitivo reportado en el archivo `.csproj` correspondiente (siguiendo el precedente de `System.Security.Cryptography.Pkcs` en `Infrastructure.csproj`).

