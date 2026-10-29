# Instrucciones de Repositorio para GitHub Copilot

Este repositorio es una API REST construida sobre **.NET 10** (`net10.0`), diseñada bajo **Clean Architecture** y guiada por **Spec-Driven Development (SDD)**.

Antes de proponer, generar o modificar cualquier código, debes consultar y obedecer estrictamente las directrices contenidas en:
- `AGENTS.md` (Constitución del proyecto: gobernanza general, arquitectura, seguridad, manejo de secretos y ciclo SDD).
- `docs/features/<feature>/` (Documentación oficial aprobada de cada funcionalidad).

## Reglas Fundamentales para Copilot

1. **No codificar sin especificación:** No agregues funcionalidades ni modifiques la arquitectura sin una especificación previamente aprobada por el usuario en `docs/features/<feature>/spec.md`.
2. **Inversión de Dependencias (Clean Architecture):**
   - `Domain` no tiene dependencias externas ni de otros proyectos.
   - `Application` solo referencia a `Domain` (casos de uso, interfaces, DTOs).
   - `Infrastructure` implementa adaptadores técnicos (EF Core, SQL Server, JWT, hashing).
   - `WebApi` expone endpoints HTTP delgados y orquesta dependencias.
3. **Manejo Estricto de Secretos:**
   - Prohibido versionar credenciales, passwords, connection strings reales o claves de firma JWT en `appsettings*.json` o código fuente.
   - Utilizar .NET User Secrets en desarrollo local y variables de entorno en producción.
4. **Respuestas HTTP Estándar:**
   - Usar `ProblemDetails` (`Results.ValidationProblem` o `Results.Problem`) para respuestas de error.
   - En login fallido, responder con `401 Unauthorized` genérico e idéntico para evitar enumeración de usuarios.
   - Extraer la identidad en rutas autenticadas exclusivamente desde los claims validados del token.

