# Ejecutar y probar la autenticación

Para entender el recorrido del login, registro y endpoint protegido, consulta
el [mapa del flujo de la aplicación](./map.md).

## Requisitos

- .NET SDK 10.
- Una instancia accesible de SQL Server 2022 o compatible. Puede ser una
  instalación local o Docker; la solución no crea ni presupone una base de
  datos existente.
- Para ejecutar la suite de integración, Docker con recursos suficientes para
  levantar SQL Server en un contenedor.

La imagen oficial `mcr.microsoft.com/mssql/server:2022-latest` debe ejecutarse
con la licencia aceptada y una contraseña SA fuerte. No se guarda una contraseña
en este repositorio. Ejemplo para una instalación local con Docker (establece
la variable `MSSQL_SA_PASSWORD` de forma privada antes de ejecutar):

```bash
docker run --name api-auth-sqlserver \
  -e ACCEPT_EULA=Y \
  -e MSSQL_PID=Developer \
  -e MSSQL_SA_PASSWORD="$MSSQL_SA_PASSWORD" \
  -p 1433:1433 \
  -d mcr.microsoft.com/mssql/server:2022-latest
```

Comprueba que el contenedor esté listo antes de iniciar la API. Si SQL Server ya
está instalado, usa su host, puerto, credenciales y opciones TLS en lugar del
contenedor. Crea la base de datos `APIbackend` o utiliza una cuenta que pueda
crearla.

## Configuración de secretos de desarrollo

`WebApi.csproj` ya declara el identificador de User Secrets. Guarda allí la
cadena de conexión y una clave aleatoria para firmar tokens:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Server=localhost,1433;Database=APIbackend;User Id=sa;Password=<local-secret>;Encrypt=True;TrustServerCertificate=True" \
  --project src/WebApi/WebApi.csproj
openssl rand -base64 48
dotnet user-secrets set "Jwt:SigningKey" "<paste-generated-key>" \
  --project src/WebApi/WebApi.csproj
dotnet user-secrets set "Jwt:Issuer" "APIbackend" \
  --project src/WebApi/WebApi.csproj
dotnet user-secrets set "Jwt:Audience" "APIbackend.Client" \
  --project src/WebApi/WebApi.csproj
dotnet user-secrets set "Jwt:ExpiresMinutes" "15" \
  --project src/WebApi/WebApi.csproj
```

Sustituye los placeholders localmente. No pegues secretos reales en documentos,
issues, historial compartido, logs ni archivos versionados. En otros entornos
usa un gestor de secretos o variables:
`ConnectionStrings__DefaultConnection`, `Jwt__Issuer`, `Jwt__Audience`,
`Jwt__SigningKey` y `Jwt__ExpiresMinutes`. La clave HMAC debe ser al menos 32
bytes UTF-8 y la expiración aceptada es de 1 a 1440 minutos.

## Crear o actualizar el esquema

La migración versionada se aplica con las herramientas EF Core locales. La
fábrica de diseño requiere la cadena de conexión en la variable de entorno
`ConnectionStrings__DefaultConnection`; User Secrets configura el proceso de la
API, pero no se exporta automáticamente a la herramienta EF:

```bash
dotnet tool restore
read -rsp "SQL Server connection string: " ConnectionStrings__DefaultConnection
export ConnectionStrings__DefaultConnection
dotnet ef database update \
  --project src/Infrastructure/Infrastructure.csproj \
  --startup-project src/WebApi/WebApi.csproj
unset ConnectionStrings__DefaultConnection
```

Para crear una migración nueva, establece `ConnectionStrings__DefaultConnection`
en el entorno del proceso `dotnet` usando el mismo método `read`/`export` y
ejecuta:

```bash
dotnet ef migrations add <MigrationName> \
  --project src/Infrastructure/Infrastructure.csproj \
  --startup-project src/WebApi/WebApi.csproj \
  --output-dir Persistence/Migrations
unset ConnectionStrings__DefaultConnection
```

El comando necesita la variable porque la fábrica de diseño verifica que se
configure explícitamente el proveedor SQL Server. No aplica cambios al iniciar
la API.

## Ejecutar la API y pruebas

```bash
dotnet restore APIbackend.slnx
dotnet build APIbackend.slnx --no-restore
dotnet test tests/Application.Tests/Application.Tests.csproj
dotnet test tests/WebApi.IntegrationTests/WebApi.IntegrationTests.csproj
dotnet run --project src/WebApi/WebApi.csproj
```

La suite de integración inicia SQL Server 2022 mediante Testcontainers, ejecuta
la migración y comprueba registro, duplicados, hash PBKDF2, login, JWT y `/me`.
Requiere Docker activo. El endpoint de desarrollo HTTP es
`http://localhost:5186`; también está disponible el perfil HTTPS definido en
`src/WebApi/Properties/launchSettings.json`.

## Probar con Postman

Importa [`authentication.postman_collection.json`](./authentication.postman_collection.json)
y configura las variables `baseUrl`, `username`, `email`, `password` y
`usernameOrEmail` en un entorno local de Postman. Usa un password de prueba
válido según la regla de 12–128 caracteres; la colección deja los valores como
placeholders. Ejecuta las solicitudes en este orden:

1. **Register**: registra la cuenta; respuesta esperada `201`.
2. **Login**: autentica con username o email; respuesta `200`. Copia
   `accessToken` a la variable local `accessToken`.
3. **Me**: envía el access token como Bearer; respuesta `200` con id y username.
4. Repite Register para verificar `409`, usa credenciales incorrectas para
   verificar `401` y llama a Me sin token para verificar `401`.

El token es una credencial temporal: no lo compartas ni lo incluyas en logs o
capturas públicas.
