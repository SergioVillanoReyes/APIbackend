# Mapa del flujo de autenticación

Este documento describe el flujo implementado en la API: qué recibe el cliente,
qué componentes se ejecutan y cómo se relacionan las capas de Clean
Architecture.

## Vista general de las capas

```text
Cliente HTTP
    |
    v
WebApi  -- HTTP, JSON, autenticación JWT, autorización y códigos HTTP
    |
    v
Application -- casos de uso: validar, registrar, iniciar sesión
    |                         |
    | contratos/puertos       | contratos/puertos
    v                         v
Infrastructure -- EF Core/SQL Server, hash PBKDF2, emisión JWT
    |
    v
SQL Server
```

La dirección de dependencias del código es:

```text
Domain <- Application <- Infrastructure
                          ^
                          |
                       WebApi
```

`Domain` contiene el modelo central y no depende de las otras capas.
`Application` define los contratos que necesita y los casos de uso.
`Infrastructure` implementa esos contratos con tecnología concreta.
`WebApi` recibe solicitudes HTTP, configura dependencias y transforma los
resultados en respuestas HTTP.

## Flujo de login: `POST /api/auth/login`

### JSON de entrada

El cliente envía `usernameOrEmail` (nombre de usuario o email) y `password`:

```http
POST /api/auth/login
Content-Type: application/json
```

```json
{
  "usernameOrEmail": "ana@example.com",
  "password": "un-password-de-prueba"
}
```

La contraseña del ejemplo es solo ilustrativa; el login comprueba el valor
contra el hash persistido del usuario.

### Recorrido paso a paso

1. **El servidor recibe HTTP — WebApi.** ASP.NET Core recibe el `POST` y el
   sistema de Minimal APIs convierte el JSON en `LoginRequest`. La ruta fue
   registrada en `AuthEndpoints.MapAuthEndpoints()`.
2. **El endpoint llama al caso de uso — WebApi → Application.** La inyección de
   dependencias proporciona `AuthenticationService`; el endpoint invoca
   `LoginAsync(request, cancellationToken)`. El endpoint no consulta la base de
   datos ni genera tokens.
3. **Application valida y normaliza — Application.** `AuthenticationService`
   exige identificador y contraseña, limita el identificador a 254 caracteres
   y la contraseña a 128, y normaliza el identificador con
   `Trim().ToUpperInvariant()`.
4. **Application solicita el usuario — Application → Infrastructure.**
   Application llama al contrato `IUserRepository.FindByLoginAsync`. Este
   contrato pertenece a Application; Application no conoce EF Core ni SQL.
5. **Se consulta SQL Server — Infrastructure → SQL Server.**
   `UserRepository` implementa el contrato y ejecuta con EF Core una consulta
   sobre `AuthDbContext.Users`. Busca el valor normalizado en
   `NormalizedUsername` o `NormalizedEmail` y devuelve el usuario, o `null`.
6. **Se verifica la contraseña — Application → Infrastructure.** Si el usuario
   existe, Application llama a `IPasswordHasher.Verify`. La implementación
   `Pbkdf2PasswordHasher` lee el formato versionado del hash y verifica la
   contraseña con PBKDF2-HMAC-SHA-256, la sal guardada y comparación en tiempo
   constante. La contraseña original nunca se compara contra una columna ni se
   guarda en la base de datos. Si el usuario no existe, el servicio realiza un
   cálculo PBKDF2 ficticio antes de responder, para reducir diferencias de
   tiempo evidentes entre usuario inexistente y contraseña errónea.
7. **Se genera el JWT — Application → Infrastructure.** Solo si la verificación
   es correcta, Application llama `IAccessTokenGenerator.Generate`. La
   implementación crea un JWT firmado con HMAC-SHA-256 que contiene el id en
   `sub`, el username en `preferred_username`, emisor, audiencia y fechas de
   emisión/expiración.
8. **Application produce el resultado.** El caso de uso devuelve
   `LoginResponse` con `accessToken`, `tokenType: "Bearer"` y `expiresAtUtc`.
9. **WebApi responde HTTP.** `AuthEndpoints.LoginAsync` convierte el resultado
   válido en `200 OK` con JSON. Si el usuario no existe o la contraseña no es
   correcta, responde `401 Unauthorized` con el mismo título y mensaje público.
   Si el JSON o campos obligatorios son inválidos, responde `400 Bad Request`.

### Respuesta de login exitoso

```http
HTTP/1.1 200 OK
Content-Type: application/json
```

```json
{
  "accessToken": "<JWT firmado>",
  "tokenType": "Bearer",
  "expiresAtUtc": "<fecha-hora UTC>"
}
```

El cliente conserva el token y lo envía en solicitudes posteriores mediante
`Authorization: Bearer <JWT>`. No debe guardarlo en logs ni compartirlo.

## Flujo de registro: `POST /api/auth/register`

1. **El cliente** envía `username`, `email` y `password` como JSON.
2. **WebApi** convierte el JSON en `RegisterUserRequest` e invoca
   `AuthenticationService.RegisterAsync`.
3. **Application** valida el username (3–32 caracteres permitidos), el email
   (formato válido, máximo 254) y la contraseña (12–128). Normaliza username y
   email a mayúsculas invariantes.
4. **Application** usa `IUserRepository` para consultar si el username/email
   normalizados ya existen. Si alguno existe, el caso de uso devuelve el
   conflicto correspondiente.
5. **Application → Infrastructure:** `IPasswordHasher.Hash` crea un hash
   PBKDF2-HMAC-SHA-256 versionado, con sal aleatoria distinta por contraseña y
   600 000 iteraciones.
6. **Application** construye la entidad `User` con el hash (no conserva una
   contraseña en la entidad) y la entrega al contrato `IUserRepository`.
   **Infrastructure** agrega la entidad con EF Core y la persiste en la tabla
   `Users` de SQL Server.
7. SQL Server aplica además índices únicos para `NormalizedUsername` y
   `NormalizedEmail`. Si dos solicitudes compiten y el valor duplicado pasa la
   consulta previa, el índice evita guardar el duplicado; Infrastructure
   convierte esa colisión conocida en `UserAlreadyExistsException`.
8. **Application** devuelve `UserResponse` con id, username y email públicos.
   **WebApi** responde `201 Created`. Ni el password ni el hash forman parte de
   la respuesta.

Los errores de formato se responden con `400 Bad Request`; los duplicados con
`409 Conflict`.

## Flujo de `/me`: `GET /api/auth/me`

1. El cliente envía `GET /api/auth/me` con
   `Authorization: Bearer <JWT>`.
2. Antes de ejecutar el endpoint, el middleware de autenticación JWT, configurado
   en `Program.cs`, comprueba firma, emisor, audiencia y vigencia. El skew de
   reloj configurado es cero.
3. Si no hay JWT válido, la autorización rechaza la solicitud con
   `401 Unauthorized`; el endpoint no se ejecuta.
4. Con un JWT válido, `AuthEndpoints.GetCurrentUser` obtiene `sub` y
   `preferred_username` del principal autenticado, valida el formato del id y
   responde `200 OK` con `id` y `username`.

`/me` no consulta la base de datos en cada solicitud y no acepta un id enviado
por el cliente: utiliza exclusivamente los claims del JWT que ya validó el
middleware.

## Uso de cada capa

### WebApi

- Punto de entrada HTTP y configuración de la aplicación.
- Registra `POST /api/auth/register`, `POST /api/auth/login` y
  `GET /api/auth/me`.
- Configura la conexión y las opciones JWT a partir de configuración externa,
  User Secrets o variables de entorno.
- Configura autenticación bearer y autorización; convierte casos de uso y
  errores de validación/conflicto a códigos HTTP.
- No contiene la consulta SQL, la regla de hashing ni el caso de uso de login.

Archivos principales: `src/WebApi/Program.cs` y
`src/WebApi/Endpoints/AuthEndpoints.cs`.

### Application

- Coordina el flujo de registro y login.
- Valida campos y normaliza username/email.
- Decide cuándo buscar usuario, comprobar existencia, verificar el hash y
  solicitar la emisión del token.
- Define abstracciones como `IUserRepository`, `IPasswordHasher` e
  `IAccessTokenGenerator`, además de los requests/responses.
- No depende de EF Core, SQL Server, el middleware JWT ni de WebApi.

Archivos principales:
`src/Application/Authentication/AuthenticationService.cs` y
`src/Application/Authentication/AuthenticationContracts.cs`.

### Domain

- Define el modelo de negocio `User`: identificador, nombre y email originales,
  valores normalizados, hash de contraseña y fecha de creación UTC.
- No contiene detalles HTTP, SQL Server, EF Core ni emisión de tokens.
- No decide cómo se persiste ni cómo se calcula criptográficamente el hash.

Archivo principal: `src/Domain/User.cs`.

### Infrastructure

- Implementa el contrato `IUserRepository` mediante `UserRepository`,
  `AuthDbContext` y Entity Framework Core.
- Mapea la entidad a la tabla `Users` e impone índices únicos en SQL Server.
- Implementa `IPasswordHasher` con `Pbkdf2PasswordHasher`.
- Implementa `IAccessTokenGenerator` con `JwtAccessTokenGenerator`.
- Registra esas implementaciones en inyección de dependencias.
- Contiene la configuración EF y las migraciones de base de datos.

Archivos principales: `src/Infrastructure/Persistence/`,
`src/Infrastructure/Authentication/` y
`src/Infrastructure/DependencyInjection.cs`.

## Resumen de las fronteras entre capas

| Pregunta | Responsable |
|---|---|
| ¿Qué ruta HTTP recibe el JSON y qué código HTTP devuelve? | WebApi |
| ¿Qué campos se validan y qué pasos forman el login/registro? | Application |
| ¿Qué datos representa un usuario? | Domain |
| ¿Cómo se consulta/guarda el usuario en SQL Server? | Infrastructure |
| ¿Cómo se deriva y verifica el hash PBKDF2? | Infrastructure, a través de un contrato de Application |
| ¿Cómo se firma el JWT? | Infrastructure, a través de un contrato de Application |
| ¿Quién valida el JWT recibido en una ruta protegida? | Middleware de autenticación configurado en WebApi |
