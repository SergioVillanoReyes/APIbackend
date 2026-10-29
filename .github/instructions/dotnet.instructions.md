# Convenciones e Instrucciones Técnicas de .NET 10 y C#

Este documento describe las convenciones de código y patrones observados en la solución `APIbackend.slnx`.

## Convenciones de Lenguaje y Framework

- **Framework:** .NET 10 (`net10.0`) con `<Nullable>enable</Nullable>` e `<ImplicitUsings>enable</ImplicitUsings>`.
- **Espacios de Nombres:** Utilizar espacios de nombres con ámbito de archivo (*file-scoped namespaces*):
  ```csharp
  namespace Application.Authentication;
  ```
- **Inyección de Dependencias:** Preferir *primary constructors* en clases de servicio y repositorios:
  ```csharp
  public sealed class AuthenticationService(
      IUserRepository users,
      IPasswordHasher passwordHasher,
      IAccessTokenGenerator tokenGenerator)
  ```
- **Inmutabilidad y Sellado:**
  - Declarar clases como `sealed` por defecto.
  - Utilizar tipos `record` o `sealed record` para DTOs, solicitudes y respuestas HTTP.
- **Asincronía y Cancelación:**
  - Todos los métodos asíncronos deben aceptar un `CancellationToken` con valor por defecto `default`.
  - Propagar el token en todas las llamadas internas de I/O y base de datos.
- **Normalización de Cadenas:**
  - Normalizar identificadores de usuario y correos electrónicos con `Trim().ToUpperInvariant()`.
- **Criptografía:**
  - Utilizar primitivas estándar de .NET (`System.Security.Cryptography`) como `Rfc2898DeriveBytes.Pbkdf2` y `CryptographicOperations.FixedTimeEquals`.
  - Liberar memoria sensible con `CryptographicOperations.ZeroMemory`.

