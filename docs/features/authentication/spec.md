# Autenticación JWT: especificación

## Estado

Especificación aprobada e implementada. Las decisiones de modelo, hash,
normalización, límites, login y pruebas se registran en
[`intent.md`](./intent.md).

## Requisitos funcionales

### Registro — `POST /api/auth/register`

1. Recibe un objeto JSON con `username`, `email` y `password`.
2. Rechaza cuerpo ausente o campos obligatorios vacíos, username fuera de la
   política acordada, email con formato inválido o password fuera de los límites
   acordados.
3. Normaliza username/email según la política aprobada y consulta duplicados.
4. Persiste el usuario y el hash de la contraseña en SQL Server. La columna de
   contraseña solo contiene un hash verificable.
5. Responde `201 Created` con datos públicos del usuario (id, username y,
   email); nunca devuelve el hash ni la contraseña.
6. Si username o email ya están en uso, responde `409 Conflict` con un código
   de error específico no sensible (`username_already_exists` o
   `email_already_exists`).
7. Si dos registros concurrentes compiten por el mismo valor, la restricción
   única de SQL Server es la autoridad final; la violación se traduce al mismo
   `409`, no a un error con detalles SQL.

### Inicio de sesión — `POST /api/auth/login`

1. Recibe `usernameOrEmail` (username o email) y `password`.
2. Busca al usuario persistido usando el valor normalizado y verifica la
   contraseña contra su hash mediante el servicio aprobado.
3. Credenciales incorrectas, incluyendo usuario inexistente, producen la misma
   respuesta `401 Unauthorized` y un mensaje genérico, sin revelar cuál dato
   falló.
4. Credenciales correctas producen `200 OK` con:

   ```json
   {
     "accessToken": "<JWT>",
     "tokenType": "Bearer",
     "expiresAtUtc": "<fecha-hora UTC ISO 8601>"
   }
   ```

5. El token incluye al menos un claim de sujeto estable con el id del usuario y
   un claim de username. No incluye password, hash ni datos innecesarios.
6. Emisor, audiencia, clave de firma y expiración provienen de configuración
   externa. La configuración ausente o inválida debe impedir el inicio de la
   aplicación, no generar tokens con valores predeterminados inseguros.

### Identidad actual — `GET /api/auth/me`

1. Exige `Authorization: Bearer <JWT>`.
2. Middleware valida firma, emisor, audiencia y expiración antes de autorizar.
3. Responde `200 OK` con id y username extraídos de claims del principal
   autenticado; no usa identidad suministrada en query, ruta ni cuerpo.
4. Sin token, con token malformado, firma inválida, emisor/audiencia incorrectos
   o token vencido, responde `401 Unauthorized`.

## Requisitos no funcionales

- **Arquitectura:** Domain no referencia ASP.NET Core, EF Core ni Infrastructure.
  Application define puertos y coordina casos de uso. Infrastructure implementa
  persistencia y servicios técnicos. WebApi configura los mecanismos HTTP.
- **Persistencia:** SQL Server con tabla de usuarios, claves primarias y
  restricciones únicas para username/email normalizados. Una migración versiona
  el esquema inicial.
- **Contraseñas:** PBKDF2-HMAC-SHA-256 con sal aleatoria por contraseña,
  comparación de tiempo constante y 600 000 iteraciones. El valor almacenado
  versiona algoritmo, iteraciones, sal y
  resultado; nunca se almacena la contraseña ni se cifra de forma reversible.
  SHA-256 sin función de derivación y sal no satisface este requisito.
- **JWT:** validar firma, issuer, audience y lifetime; usar clave suficientemente
  fuerte para el algoritmo elegido. La clave no se guarda en configuración
  versionada.
- **Secretos:** User Secrets en desarrollo o variables de entorno; proveedor de
  secretos seguro en entornos compartidos/producción. La conexión con contraseña
  y la clave no se incluyen en `appsettings*.json`.
- **Errores y logs:** respuesta de error consistente (preferentemente
  ProblemDetails), sin mensajes de base de datos ni secretos. No registrar
  contraseñas, hashes, cabeceras Authorization ni tokens.
- **Concurrencia:** las restricciones únicas cubren carreras entre validación y
  escritura y sus conflictos se convierten en respuesta de negocio.
- **Pruebas:** cubrir lógica de aplicación sin SQL en unit tests y probar
  persistencia, emisión/validación del token y endpoints HTTP con SQL Server real
  desechable en integración.

## Validación de entrada

Política aprobada:

| Campo | Validación |
|---|---|
| `username` | Obligatorio, longitud 3–32; alfanumérico con `.`, `_` y `-`; comparar mediante representación normalizada invariantemente en mayúsculas. |
| `email` | Obligatorio, formato email válido, máximo 254 caracteres; persistir valor original y normalizado. |
| `password` | Obligatorio, 12–128 caracteres; no recortarlo ni normalizarlo silenciosamente. |
| `usernameOrEmail` | Obligatorio; acepta username o email normalizado. |

El servidor debe validar también JSON inválido, tipos incorrectos y límites de
tamaño razonables. Los errores de validación no deben incluir el valor de
password. Login limita `usernameOrEmail` a 254 caracteres y la password a un
máximo de 128 antes de verificarla.

## Respuestas HTTP y errores

| Operación | Estado | Caso |
|---|---:|---|
| Registro | `201 Created` | Usuario guardado; cuerpo con campos públicos solamente. |
| Registro | `400 Bad Request` | JSON inválido o validaciones fallidas; errores por campo. |
| Registro | `409 Conflict` | Username o email duplicado, tanto en comprobación previa como al aplicar índice único. |
| Login | `200 OK` | Credenciales válidas; access token, tipo y expiración. |
| Login | `400 Bad Request` | JSON inválido o campos obligatorios inválidos. |
| Login | `401 Unauthorized` | Usuario desconocido o contraseña incorrecta; respuesta indistinguible. |
| Me | `200 OK` | JWT válido; id y username del principal autenticado. |
| Me | `401 Unauthorized` | Token ausente, malformado, inválido o expirado. |
| Cualquier operación | `500 Internal Server Error` | Error inesperado; detalle técnico solo en logs protegidos y sin secretos. |

No se prescribe `403 Forbidden`: no hay roles ni autorización por permisos en
esta entrega. No se distingue entre username duplicado y email duplicado en un
login fallido. En registro, el código específico de duplicado facilita corregir
el formulario y no sustituye la restricción de base de datos.

## Escenarios de aceptación

1. **Registro válido:** dado un username/email no existentes y campos válidos,
   cuando se registra, entonces se crea una fila persistente, el hash difiere de
   la contraseña original y la respuesta es `201` sin datos secretos.
2. **Registro duplicado:** dado un username o email existente, cuando se
   intenta registrar nuevamente, entonces la respuesta es `409`; tras una
   carrera concurrente el índice único conserva la misma respuesta.
3. **Validación de registro:** dado un campo obligatorio ausente o inválido,
   cuando se registra, entonces responde `400`, indica los campos inválidos y
   no crea usuario.
4. **Login válido:** dado un usuario persistido, cuando presenta identificador
   y password correctos, entonces recibe `200`, JWT firmado y expiración
   calculada desde la configuración.
5. **Login inválido:** dado un identificador inexistente o password incorrecto,
   cuando intenta iniciar sesión, entonces responde `401` con el mismo mensaje
   genérico en ambos casos.
6. **Acceso autenticado:** dado un JWT emitido con la configuración activa,
   cuando lo envía a `/api/auth/me`, entonces responde `200` con id/username
   leídos de sus claims validados.
7. **Acceso no autenticado:** dado token ausente, manipulado, vencido o firmado
   por una clave/emisor/audiencia distintos, cuando llama a `/me`, entonces
   responde `401`.
8. **Configuración incompleta:** dado un emisor, audiencia, clave o conexión
   requerida ausente/inválida, al arrancar o conectar el servicio, entonces no
   se continúa con configuración silenciosamente insegura; se notifica el error
   de configuración sin imprimir secretos.
9. **Persistencia real:** después de registrar, al consultar el usuario tras
   reiniciar el proceso o abrir otro contexto, los datos siguen en SQL Server y
   el login correcto continúa funcionando.

## Estado de implementación

Las decisiones de esta especificación están aprobadas; la implementación,
migración y pruebas de aceptación se describen en [`plan.md`](./plan.md).
