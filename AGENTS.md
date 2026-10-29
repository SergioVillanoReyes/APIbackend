# Instrucciones y Reglas para Agentes de Programación (AGENTS.md)

Este repositorio contiene una API REST construida sobre **.NET 10** (`net10.0`), diseñada con **Clean Architecture** y guiada por **Spec-Driven Development (SDD)**.

Este documento establece las instrucciones operativas, directrices arquitectónicas, protocolos de seguridad y convenciones que todo agente de programación debe acatar al trabajar en el proyecto.

---

## 1. Principios de Spec-Driven Development (SDD)

En este repositorio, el código responde a especificaciones previamente analizadas y aprobadas.

### 1.1 Consulta del contexto existente
Antes de planificar, refactorizar o modificar código, el agente debe inspeccionar la documentación existente de la funcionalidad afectada (ubicada bajo `docs/features/<funcionalidad>/`) y el código fuente relacionado. No se deben asumir requerimientos ni actuar sin comprender las decisiones vigentes.

### 1.2 Flujo para funcionalidades nuevas
Para el desarrollo de una nueva funcionalidad, el agente debe seguir las siguientes etapas secuenciales:
1. **Revisar el contexto existente:** Analizar la arquitectura actual, los contratos y la documentación ya presente.
2. **Elaborar o actualizar la intención (`intent.md`):** Definir el estado previo, objetivo, alcance explícito, lo que queda explícitamente fuera de alcance y principios de seguridad aplicables.
3. **Elaborar o actualizar la especificación técnica (`spec.md`):** Detallar los requisitos funcionales y no funcionales, reglas de validación, contratos HTTP (rutas, entradas, salidas, códigos de respuesta) y escenarios de aceptación.
4. **Preparar el plan de implementación (`plan.md`):** Desglosar las fases técnicas de ejecución, inventario de archivos a crear/modificar, dependencias necesarias y estrategia de pruebas.
5. **Solicitar aprobación explícita:** Presentar la propuesta documental al usuario y esperar su autorización antes de crear o editar código de producción.
6. **Implementar y validar:** Codificar en pasos pequeños y verificables, ejecutar pruebas y documentar el flujo resultante (`map.md`) y la guía operativa (`README.md`).

### 1.3 Excepciones al ciclo documental completo
No se exige la creación ni modificación de los 5 documentos SDD para:
- Responder preguntas, explicaciones técnicas o consultas sobre el repositorio.
- Tareas de análisis, diagnóstico o auditoría de código/dependencias.
- Correcciones pequeñas de bugs puntuales o ajustes menores que no agreguen funcionalidades nuevas ni modifiquen el diseño arquitectónico ni las reglas de negocio aprobadas.

### 1.4 Manejo de contradicciones, ambigüedades o vacíos
Si el agente encuentra contradicciones entre la documentación, el código existente y una solicitud del usuario, o detecta información faltante para tomar una decisión:
- **No debe resolver el conflicto unilateralmente ni inventar decisiones.**
- Debe detenerse, explicar la contradicción o vacío con referencias exactas a archivos y líneas, y solicitar la decisión del usuario.

---

## 2. Documentos SDD Existentes en el Repositorio

La primera funcionalidad implementada en el proyecto es la autenticación JWT. Sus documentos oficiales residen en las siguientes rutas verificadas:

| Ruta del documento | Propósito y función |
|---|---|
| `docs/features/authentication/intent.md` | Justificación, motivación, objetivos, alcance, elementos fuera de alcance y principios de seguridad de la autenticación. |
| `docs/features/authentication/spec.md` | Requisitos funcionales/no funcionales de `POST /api/auth/register`, `POST /api/auth/login` y `GET /api/auth/me`, reglas de validación y escenarios de aceptación. |
| `docs/features/authentication/plan.md` | Plan ejecutado, fases técnicas, archivos implementados, paquetes aprobados y auditoría de vulnerabilidades. |
| `docs/features/authentication/map.md` | Diagrama y recorrido paso a paso de los flujos HTTP a través de las capas de Clean Architecture. |
| `docs/features/authentication/README.md` | Guía para desarrolladores: requisitos de entorno, configuración de User Secrets, ejecución de migraciones EF Core y pruebas locales/Postman. |
| `docs/features/authentication/authentication.postman_collection.json` | Colección exportada de Postman para pruebas de endpoints. |

> **Nota:** Los archivos bajo `.github/` (`.github/copilot-instructions.md`, `.github/instructions/dotnet.instructions.md`, `.github/agents/planner.agent.md` y `.github/agents/reviewer.agent.md`) se encuentran actualmente vacíos como marcadores de posición iniciales y no sustituyen a este documento.

---

## 3. Clean Architecture y Reglas de Dependencias

La solución `APIbackend.slnx` implementa Clean Architecture con inversión de dependencias estricta:

```text
Domain  <----  Application  <----  Infrastructure
  ^                  ^                   ^
  |                  |                   |
  +------------------+                   |
                     |                   |
                  WebApi ----------------+
```

### 3.1 Responsabilidad y límites de cada proyecto

- **`src/Domain` (`Domain.csproj`):**
  - **Dependencias:** Ninguna (cero referencias a proyectos de la solución y cero paquetes NuGet externos).
  - **Contenido:** Entidades y modelos centrales de negocio (ej. `User.cs`).
  - **Restricción:** No debe contener código de infraestructura, persistencia, HTTP ni frameworks externos.
- **`src/Application` (`Application.csproj`):**
  - **Dependencias:** Únicamente `Domain.csproj`. No utiliza paquetes NuGet externos.
  - **Contenido:** Casos de uso (`AuthenticationService.cs`), contratos/puertos de abstracción (`IUserRepository`, `IPasswordHasher`, `IAccessTokenGenerator`), DTOs/records y excepciones de aplicación.
  - **Restricción:** No debe depender de Entity Framework Core, SQL Server, ASP.NET Core ni `Infrastructure`.
- **`src/Infrastructure` (`Infrastructure.csproj`):**
  - **Dependencias:** `Application.csproj` y `Domain.csproj`.
  - **Contenido:** Adaptadores técnicos: persistencia con EF Core (`AuthDbContext`, `UserConfiguration`, `UserRepository`), migraciones, criptografía (`Pbkdf2PasswordHasher`), generación de tokens (`JwtAccessTokenGenerator`) y registro de dependencias (`DependencyInjection.cs`).
- **`src/WebApi` (`WebApi.csproj`):**
  - **Dependencias:** `Application.csproj` e `Infrastructure.csproj`.
  - **Contenido:** Punto de entrada HTTP (Minimal APIs en `AuthEndpoints.cs`), middleware de autenticación/autorización JWT, OpenAPI, configuración de arranque y gestión de opciones.

---

## 4. Convenciones de Código y Diseño

### 4.1 Patrones observados en el código actual
En la implementación actual del proyecto se observan las siguientes convenciones:
- **Plataforma:** C# sobre .NET 10 con `<Nullable>enable</Nullable>` e `<ImplicitUsings>enable</ImplicitUsings>`.
- **Espacios de nombres con ámbito de archivo:** `namespace Application.Authentication;`.
- **Inyección de dependencias:** Uso predominante de *primary constructors* en servicios y repositorios (ej. `public sealed class AuthenticationService(...)`).
- **Inmutabilidad y sellado:** Uso frecuente de modificador `sealed` en clases y `record` para DTOs y respuestas de datos.
- **Asincronía:** Métodos asíncronos que aceptan `CancellationToken` con valor por defecto `default`.
- **Normalización de identificadores:** Conversión consistente de logins/emails mediante `Trim().ToUpperInvariant()`.
- **Respuestas HTTP:** Uso de `Results.ValidationProblem` ante errores de entrada y `Results.Problem` con formato `ProblemDetails` para conflictos o errores de autenticación.

### 4.2 Distinción con recomendaciones futuras
Las características estilísticas mencionadas arriba reflejan cómo está escrito el código actual. No deben tomarse como imposiciones dogmáticas que bloqueen soluciones válidas, a menos que el usuario o una especificación aprobada establezcan una regla obligatoria expresa.

---

## 5. Protección de Datos, Secretos y Seguridad

### 5.1 Reglas para bases de datos y operaciones destructivas
- **No ejecutar operaciones destructivas sin autorización explícita:** Prohibido eliminar bases de datos, truncar tablas de producción/desarrollo persistente, borrar volúmenes Docker o ejecutar comandos irreversibles sin consentimiento previo del usuario.
- **Migraciones sobre bases persistentes:** No ejecutar `dotnet ef database update` sobre instancias de bases de datos persistentes sin confirmación explícita del usuario.
- **La API no aplica migraciones al iniciar:** La aplicación no debe ejecutar `Database.Migrate()` en el arranque de la API; las migraciones son administradas manualmente vía CLI o en contenedores efímeros de pruebas de integración.

### 5.2 Manejo estricto de secretos
- **Prohibido versionar credenciales:** No escribir contraseñas, cadenas de conexión reales ni claves de firma JWT en `appsettings.json`, `appsettings.Development.json`, código fuente o commits.
- **Entorno de desarrollo local:** Utilizar .NET User Secrets mediante el ID configurado en `WebApi.csproj` (`APIbackend-WebApi-Authentication`) o variables de entorno del sistema (`ConnectionStrings__DefaultConnection`, `Jwt__*`).
- **Validación en arranque:** Las opciones críticas de configuración deben validarse al iniciar la aplicación (`ValidateOnStart()`), deteniendo el proceso de forma temprana si faltan valores requeridos.

### 5.3 Principios de seguridad aplicados en la API
- **Hashing de contraseñas:** PBKDF2-HMAC-SHA-256 con sal aleatoria de 16 bytes por usuario, 600.000 iteraciones y comparación en tiempo constante (`FixedTimeEquals`). Prohibido almacenar contraseñas en plano, usar algoritmos reversibles o hashes directos no salados (como SHA-256 plano o MD5).
- **Respuestas indistinguibles en login:** Si el login falla (usuario no existe o contraseña inválida), responder con un `401 Unauthorized` genérico e idéntico para evitar enumeración de usuarios.
- **Identidad en endpoints protegidos:** En rutas autenticadas (como `/api/auth/me`), extraer la identidad del usuario exclusivamente desde los claims validados del token (`sub`, `preferred_username`). No aceptar ni confiar en IDs enviados por el cliente vía parámetros, query o body.
- **Autoridad de unicidad:** Las restricciones e índices únicos a nivel SQL Server (`IX_Users_NormalizedUsername`, `IX_Users_NormalizedEmail`) son la autoridad final contra condiciones de carrera. Sus colisiones deben traducirse limpiamente a `409 Conflict`.

---

## 6. Comandos de Validación y Ciclo Técnico

Todo cambio en el código debe verificarse ejecutando los comandos respaldados por la solución:

```bash
# Restauración y compilación limpia
dotnet restore APIbackend.slnx
dotnet build APIbackend.slnx --no-restore

# Pruebas unitarias (ejecución rápida, sin dependencias externas)
dotnet test tests/Application.Tests/Application.Tests.csproj

# Pruebas de integración (requieren Docker activo para Testcontainers y SQL Server)
dotnet test tests/WebApi.IntegrationTests/WebApi.IntegrationTests.csproj

# Auditoría de dependencias por paquetes vulnerables
dotnet list APIbackend.slnx package --vulnerable --include-transitive
```

### 6.1 Procedimiento para migraciones con EF Core
La fábrica en tiempo de diseño `AuthDbContextFactory` requiere la cadena de conexión en la variable de entorno `ConnectionStrings__DefaultConnection`:

```bash
# Restaurar herramientas locales
dotnet tool restore

# Aplicar migraciones pendientes (requiere autorización si es base persistente)
export ConnectionStrings__DefaultConnection="<cadena-de-conexion>"
dotnet ef database update \
  --project src/Infrastructure/Infrastructure.csproj \
  --startup-project src/WebApi/WebApi.csproj
unset ConnectionStrings__DefaultConnection

# Generar una nueva migración (tras cambios aprobados en entidades)
export ConnectionStrings__DefaultConnection="<cadena-de-conexion>"
dotnet ef migrations add <NombreMigracion> \
  --project src/Infrastructure/Infrastructure.csproj \
  --startup-project src/WebApi/WebApi.csproj \
  --output-dir Persistence/Migrations
unset ConnectionStrings__DefaultConnection
```

---

## 7. Decisiones Pendientes y Asuntos No Resueltos

Los siguientes elementos representan aspectos detectados en el repositorio cuya resolución definitiva está **pendiente de decisión del usuario**. Los agentes deben respetarlos en su estado actual y no modificarlos por iniciativa propia:

1. **Archivos de plantilla `Class1.cs`:** Existen clases vacías `Class1.cs` en `src/Domain`, `src/Application` y `src/Infrastructure`. Se mantienen sin uso y no deben eliminarse hasta que el usuario lo autorice en una tarea de limpieza.
2. **Endpoint `/weatherforecast`:** La ruta `/weatherforecast` en `src/WebApi/Program.cs` se conserva de la plantilla inicial. No debe eliminarse ni alterarse sin indicación expresa.
3. **Organización física de contratos y modelos en `Application`:** Los contratos actuales de autenticación están consolidados en `AuthenticationContracts.cs`, coexistiendo con subcarpetas vacías (`Contracts/`, `Login/`, `Models/`). No se debe forzar una reestructuración de archivos ni en esta feature ni en futuras features sin previa definición del usuario.
4. **Instrucciones en `.github/`:** Los archivos de Copilot y agentes dentro de `.github/` permanecen vacíos por el momento; no deben modificarse ni poblarse hasta nueva indicación.

---

## 8. Reglas de Interacción para Agentes

1. **Trabajo en pasos pequeños e incrementales:** Evitar cambios masivos o abarcar múltiples capas a la vez sin validar.
2. **Proteger el trabajo preexistente:** No modificar código funcional ni especificaciones aprobadas sin una solicitud explícita.
3. **No adivinar:** Si las instrucciones son ambiguas o falta contexto técnico, preguntar antes de actuar.

