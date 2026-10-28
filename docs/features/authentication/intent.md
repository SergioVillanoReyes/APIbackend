# Autenticación JWT: intención

## Estado inicial de la solución

La solución `APIbackend.slnx` contiene `Domain`, `Application`, `Infrastructure` y
`WebApi`, todos dirigidos a `net10.0`. `Application` referencia `Domain`,
`Infrastructure` referencia ambos y `WebApi` referencia `Application` e
`Infrastructure`. El código actual de la API es la plantilla de pronóstico del
tiempo; no hay autenticación, persistencia, configuración de base de datos ni
servicios de dominio implementados.

La solución incluye `tests/Application.Tests`, con una prueba vacía. No incluye
un proyecto de pruebas de integración versionado. Los archivos de instrucciones
`AGENTS.md`, `.github/copilot-instructions.md` y
`.github/instructions/dotnet.instructions.md` existen y están vacíos; no se
modifican.

## Objetivo

Agregar una autenticación funcional y persistente para que una persona pueda
registrarse, iniciar sesión con sus credenciales guardadas en SQL Server y
recibir un JWT firmado. La API debe validar ese token en `GET /api/auth/me` y
obtener la identidad exclusivamente de sus claims validados. La estructura
resultante debe permitir que futuras funciones de la API requieran esa misma
identidad autenticada.

La implementación deberá respetar Clean Architecture: las reglas y los casos
de uso no dependerán de ASP.NET Core ni de EF Core; Infrastructure implementará
los adaptadores de datos y servicios técnicos; WebApi configurará HTTP,
autenticación, autorización e inyección de dependencias.

## Alcance de esta funcionalidad

- `POST /api/auth/register`: valida username, email y password, comprueba
  duplicados y persiste un usuario sin almacenar la contraseña original.
- `POST /api/auth/login`: busca el usuario persistido, verifica el hash y emite
  un access token JWT con expiración configurable.
- `GET /api/auth/me`: exige un JWT válido y devuelve el identificador y
  username de los claims validados.
- Restricciones de unicidad en SQL Server, migración inicial y pruebas unitarias
  y de integración.
- Configuración externa para conexión, emisor, audiencia, clave de firma y
  expiración; instrucciones para desarrollo local y pruebas con Postman.

## Fuera de alcance

No se incluyen refresh tokens, cierre de sesión con revocación, recuperación
de contraseña, verificación de email, OAuth, roles/permisos avanzados ni
funcionalidades de negocio ajenas a autenticación. Tampoco se incorporarán
CQRS, MediatR, repositorios genéricos ni una solución de Identity completa sin
una necesidad que la justifique.

## Principios de seguridad

- Nunca guardar, devolver ni registrar contraseñas en texto plano.
- Derivar un hash de contraseña PBKDF2-HMAC-SHA-256 con sal aleatoria; no cifrar
  contraseñas de forma reversible ni usar SHA-256 directo.
- No versionar contraseñas, cadenas de conexión con credenciales ni claves JWT.
- Validar firma, emisor, audiencia y vigencia del JWT.
- No revelar si el usuario existe en el mensaje de error del inicio de sesión.
- No confiar en identificadores enviados por el cliente para `/me`.
- Mantener unicidad en la base de datos además de comprobar duplicados en la
  aplicación.

## Decisiones aprobadas para esta versión

1. **Modelo de usuario:** entidad propia en Domain con `Guid` como identificador,
   username/email originales y normalizados, hash de contraseña y fecha de
   creación UTC. No se usa `IdentityUser` ni se añade el esquema completo de
   ASP.NET Core Identity.
2. **Hash de contraseña:** usar PBKDF2-HMAC-SHA-256, una sal aleatoria única y
   comparación en tiempo constante, encapsulado detrás de un contrato de
   Application e implementado en Infrastructure con las primitivas estándar
   de .NET. “256” identifica SHA-256 como PRF de PBKDF2; no se usa SHA-256
   directo ni cifrado reversible. El formato almacenado incluirá versión,
   iteraciones, sal y hash para permitir evolución futura. La implementación
   fija 600 000 iteraciones y 16 bytes de sal.
3. **Persistencia:** EF Core 10 con `Microsoft.EntityFrameworkCore.SqlServer`;
   DbContext, mapeos e historial de migraciones en Infrastructure. Los campos
   normalizados se comparan invariantemente en mayúsculas y tienen índices
   únicos.
4. **SQL Server:** usar una instalación local existente si se dispone de ella;
   como opción reproducible, usar la imagen oficial SQL Server 2022 en Docker.
   No se presupone que una base de datos o servicio esté ya ejecutándose.
5. **Identificador de inicio de sesión:** el campo `usernameOrEmail` acepta
   username o email.
6. **Validaciones y duración:** username de 3 a 32 caracteres, email válido,
   password de 12 a 128 caracteres y expiración inicial de 15 minutos,
   configurable entre 1 y 1440.
7. **Pruebas de integración:** `WebApi.IntegrationTests` usa SQL Server
   desechable mediante Testcontainers y requiere Docker.
