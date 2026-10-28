# Autenticación JWT: plan ejecutado

## Estado

Las decisiones propuestas fueron aprobadas y el plan se ejecutó. La solución
usa .NET 10; `Application` referencia `Domain`, `Infrastructure` referencia
`Application` y `Domain`, y `WebApi` referencia Application/Infrastructure.
La implementación conserva el endpoint preexistente `/weatherforecast`.
Los archivos de instrucciones que ya existían estaban vacíos y no se
modificaron.

## Fases completadas

1. **Contratos:** modelo de usuario propio, login mediante username o email,
   límites de entrada acordados y PBKDF2-HMAC-SHA-256 (no cifrado reversible).
2. **Domain/Application:** entidad, contratos, casos de uso de registro/login y
   pruebas unitarias. Application no referencia EF, ASP.NET Core ni
   Infrastructure.
3. **Infrastructure:** DbContext, índices únicos, repositorio, hash con
   600 000 iteraciones/sal aleatoria de 16 bytes, generación JWT y migración.
   Las colisiones de los índices únicos conocidos se traducen a `409`; otros
   errores no se enmascaran como duplicados.
4. **WebApi:** configuración externa validada al arrancar, User Secrets,
   autenticación bearer, autorización y endpoints HTTP delgados.
5. **Verificación:** prueba de integración con SQL Server 2022 en Docker,
   colección Postman y guía local.

## Archivos implementados

| Proyecto/ruta | Cambio |
|---|---|
| `src/Domain/User.cs` | Entidad de usuario con identificador Guid y campos normalizados. |
| `src/Application/Authentication/` | Contratos, modelos y casos de uso de registro/login. |
| `src/Infrastructure/Persistence/` | DbContext, configuración, repositorio, fábrica de diseño y migración inicial. |
| `src/Infrastructure/Authentication/` | Hash PBKDF2-HMAC-SHA-256 y generación de JWT. |
| `src/WebApi/Program.cs`, `src/WebApi/Endpoints/AuthEndpoints.cs` | Configuración y endpoints HTTP. |
| `src/WebApi/WebApi.csproj`, `src/WebApi/appsettings.json` | Dependencias y configuración no sensible. |
| `tests/Application.Tests/` | Pruebas unitarias de los casos de uso. |
| `tests/WebApi.IntegrationTests/` | Prueba del flujo contra SQL Server Testcontainers. |
| `APIbackend.slnx`, `.config/dotnet-tools.json` | Proyecto de integración y herramienta EF local. |
| `docs/features/authentication/README.md` | Configuración, migraciones y ejecución local. |
| `docs/features/authentication/authentication.postman_collection.json` | Colección de Postman. |

Se mantuvieron los placeholders `Class1.cs` sin uso; no fue necesario
modificarlos.

## Dependencias

| Proyecto | Paquete | Versión | Motivo |
|---|---|---:|---|
| Infrastructure | `Microsoft.EntityFrameworkCore.SqlServer` | 10.0.12 | Proveedor EF Core SQL Server. |
| Infrastructure y WebApi | `Microsoft.EntityFrameworkCore.Design` | 10.0.12 | Comandos de migración EF, privado al proyecto. |
| Infrastructure | `System.IdentityModel.Tokens.Jwt` | 8.14.0 | Crear JWT firmado. |
| WebApi | `Microsoft.AspNetCore.Authentication.JwtBearer` | 10.0.12 | Validación de JWT bearer. |
| WebApi.IntegrationTests | `Microsoft.AspNetCore.Mvc.Testing` | 10.0.12 | Pruebas HTTP hospedadas. |
| WebApi.IntegrationTests | `Testcontainers.MsSql` | 4.8.1 | SQL Server de prueba en Docker. |
| Infrastructure | `System.Security.Cryptography.Pkcs` | 9.0.21 | Sobrescribir versión transitiva vulnerable detectada en restore. |
| WebApi.IntegrationTests | `SSH.NET` | 2026.0.0 | Sobrescribir versión transitiva vulnerable de Testcontainers/Docker. |
| Herramienta local | `dotnet-ef` | 10.0.12 | Crear/aplicar migraciones. |

No se agregó paquete de criptografía para PBKDF2: se usan las primitivas estándar
de `System.Security.Cryptography` incluidas en .NET. Las dependencias
transitivas vulnerables detectadas inicialmente se actualizaron; la auditoría
final no reportó paquetes vulnerables.

## Comandos ejecutados y validación

```bash
dotnet restore APIbackend.slnx
dotnet build APIbackend.slnx --no-restore
dotnet tool restore
dotnet test tests/Application.Tests/Application.Tests.csproj
dotnet test tests/WebApi.IntegrationTests/WebApi.IntegrationTests.csproj
dotnet list APIbackend.slnx package --vulnerable --include-transitive
```

Restore, build, restore de herramientas y ambas suites pasaron. La suite
unitaria contiene seis pruebas. La integración aplicó la migración inicial y
probó validación, registro, persistencia del hash, duplicados, login, expiración,
firma, emisor, audiencia y `/me` contra SQL Server 2022.

Para comandos `dotnet ef`, la fábrica de diseño requiere
`ConnectionStrings__DefaultConnection` en el entorno del proceso. La guía
[`README.md`](./README.md) muestra cómo proporcionarla sin versionar secretos.
La API no aplica migraciones automáticamente al arrancar.

## Requisitos del entorno

- .NET SDK 10.
- SQL Server 2022 o compatible. En la inspección inicial, Docker estaba
  disponible y la imagen oficial estaba descargada, pero no había instancia
  activa ni listener en el puerto 1433. `sqlcmd` no estaba instalado; eso no
  demuestra que no exista una instancia instalada.
- La suite de integración usa Testcontainers, requiere Docker activo y
  administra el ciclo de vida de su contenedor SQL de prueba.
- Para desarrollo manual, usar una instancia local o Docker según
  [`README.md`](./README.md); configurar conexión y clave JWT fuera de Git.

## Criterios verificados

- Build y pruebas unitarias e integración completadas con éxito.
- Migración crea tabla de usuarios e índices únicos en SQL Server.
- Login verifica contraseñas persistidas y emite JWT.
- `/me` acepta solo tokens válidos y toma la identidad de claims autenticados.
- No se devuelve ni almacena el password original; respuestas de credenciales
  incorrectas no distinguen usuario inexistente de password incorrecto.
- La auditoría final de dependencias no detecta paquetes vulnerables.
