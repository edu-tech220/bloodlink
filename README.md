# BloodLink

Plataforma de banco de sangre: gestiona el inventario de unidades por tipo y componente,
las citas y donaciones, y las solicitudes de unidades que hacen los hospitales, aplicando
las reglas de compatibilidad ABO/Rh, vida util por componente y concurrencia segura al
reservar unidades escasas.

## Que problema resuelve

Un banco de sangre necesita responder preguntas con reglas de negocio reales, no un CRUD:

- ¿Qué unidades de sangre son compatibles con un paciente de un tipo determinado?
- ¿Cuándo caduca una unidad según su componente (sangre total, glóbulos rojos, plasma,
  plaquetas, crioprecipitado)?
- ¿Puede un donante donar hoy, o hace falta esperar por su edad o por una donación reciente?
- Si dos hospitales piden el mismo tipo de sangre escaso al mismo tiempo, ¿cómo se evita
  reservar dos veces la misma unidad?
- ¿Qué unidades se asignan primero cuando hay varias disponibles? (la que caduca antes,
  FEFO: *first-expired, first-out*)

BloodLink modela estas reglas en el backend y las expone tanto a un panel administrativo
(personal del banco de sangre) como a hospitales que solicitan unidades.

## Arquitectura

Arquitectura de microservicios con dos aplicaciones ASP.NET Core independientes que solo
se comunican por HTTP, más un proyecto de pruebas:

```
BloodLink.sln
├── src/BloodLink.Api/      Microservicio backend (Web API) — toda la logica de negocio
├── src/BloodLink.Web/      Frontend (Razor Pages) — solo HTML/CSS, sin SPA
└── tests/BloodLink.Api.Tests/   Pruebas xUnit del backend (SQLite en memoria)
```

- **BloodLink.Api**: ASP.NET Core Web API + EF Core + SQL Server. Expone endpoints REST
  protegidos con JWT (roles `Admin` y `Hospital`), aplica las reglas de compatibilidad
  sanguínea, vida útil, elegibilidad de donantes y asignación FEFO con concurrencia
  optimista. Es la única aplicación que toca la base de datos.
- **BloodLink.Web**: ASP.NET Core Razor Pages. No tiene acceso a la base de datos ni
  conoce EF Core: todo lo que muestra lo obtiene llamando a BloodLink.Api mediante un
  `HttpClient` tipado registrado con `IHttpClientFactory`. La sesión del usuario es una
  cookie de autenticación de ASP.NET Core que guarda el JWT emitido por la API como un
  claim; un `DelegatingHandler` lo reenvía como `Authorization: Bearer` en cada llamada.
- **BloodLink.Api.Tests**: pruebas xUnit que corren con `dotnet test` sin SQL Server,
  usando SQLite en memoria a través de `WebApplicationFactory<Program>`.

Ambos servicios se despliegan y escalan por separado (cada uno con su propio Dockerfile),
y el frontend no comparte código ni modelos de EF Core con el backend: sus DTOs están
duplicados a propósito, porque un cambio interno en uno no debe forzar un despliegue
conjunto del otro.

## Decisiones técnicas

- **Compatibilidad ABO/Rh calculada, no una tabla fija.** `BloodCompatibilityService`
  modela cada tipo de sangre como `(TieneAntígenoA, TieneAntígenoB, Rh+)` y deriva la
  compatibilidad a partir de esas propiedades (para glóbulos rojos: los antígenos del
  donante deben ser subconjunto de los del receptor; para plasma, la regla es la inversa
  porque lo que viaja son anticuerpos). Es más auditable que una tabla de 64 casos y deja
  claro *por qué* O− es donante universal y AB+ receptor universal.
- **Concurrencia optimista sin `rowversion` de SQL Server.** Las pruebas corren contra
  SQLite y el motor real contra SQL Server, así que en vez de una columna `rowversion`
  (específica de SQL Server) se usa un `int Version` marcado como `IsConcurrencyToken()`
  e incrementado manualmente en cada cambio de estado. EF Core incluye el valor original
  en el `WHERE` de cada `UPDATE` en cualquier proveedor, así que el mismo código y las
  mismas pruebas de conflicto funcionan igual en ambas bases de datos.
- **Asignación FEFO con reintento ante conflictos.** `BloodAllocationService` busca las
  unidades compatibles que caducan antes, las reserva y guarda; si otra solicitud ganó la
  carrera por la misma unidad, `SaveChangesAsync` lanza `DbUpdateConcurrencyException`, el
  servicio descarta el tracking y reintenta leyendo el inventario ya actualizado (hasta 5
  veces) en lugar de fallar la petición del hospital.
- **Migraciones de EF Core vs. SQLite de pruebas.** Las migraciones se generan con el
  proveedor de SQL Server y graban tipos de columna propios de ese motor
  (`nvarchar(max)`, `datetime2`); aplicarlas tal cual sobre SQLite falla. Por eso el
  entorno de pruebas usa `Database.EnsureCreated()` (crea el esquema directamente desde el
  modelo con los tipos de SQLite) mientras que el arranque real de la API sigue usando
  `Database.Migrate()` contra SQL Server.
- **PBKDF2 + JWT + rate limiting + CORS.** Las contraseñas se guardan con PBKDF2-SHA256
  (100,000 iteraciones, sal aleatoria por usuario). El login emite un JWT con el rol y,
  si aplica, el `hospitalId` del usuario. El endpoint de login tiene un limitador de tasa
  (`AddFixedWindowLimiter`) para dificultar fuerza bruta, y CORS solo permite el origen
  configurado para el frontend.
- **`BackgroundService` para expirar inventario.** `ExpirationSweepService` recorre
  periódicamente las unidades `Available`/`Quarantined` vencidas y las marca como
  `Expired`, para que el panel de inventario y la asignación FEFO nunca ofrezcan una
  unidad caducada. También existe un endpoint manual (`POST /api/inventory/sweep-expired`)
  para forzar el barrido sin esperar al intervalo.
- **Paginación real en los listados.** Donantes, citas, unidades de inventario y
  solicitudes se devuelven paginados (`page`/`pageSize`) en vez de volcar toda la tabla.

## Modelo de dominio (resumen)

| Entidad | Rol |
|---|---|
| `Donor` | Persona que dona; guarda tipo de sangre y última donación para calcular elegibilidad. |
| `DonationAppointment` | Cita de donación; al completarse genera una `BloodUnit` en cuarentena. |
| `BloodUnit` | Unidad física de un componente sanguíneo, con caducidad y máquina de estados (`Quarantined → Available → Reserved → Issued`, o `Discarded`/`Expired`). |
| `Hospital` | Entidad que solicita unidades. |
| `BloodRequest` | Pedido de un hospital; pasa por `Pending → PartiallyReserved → Reserved → Issued` (o `Cancelled`/`Rejected`). |
| `RequestAllocation` | Enlace entre una solicitud y las unidades concretas que se le reservaron. |
| `User` | Cuenta con rol `Admin` (personal del banco de sangre) o `Hospital` (ligada a un hospital). |

## Cómo correrlo

### Con Docker Compose (recomendado)

Levanta SQL Server, el backend y el frontend:

```bash
docker compose up --build
```

- Frontend: http://localhost:5000
- API + Swagger: http://localhost:5080/swagger

Al arrancar, la API aplica las migraciones de EF Core contra SQL Server y siembra datos
de prueba:

| Usuario | Contraseña | Rol |
|---|---|---|
| `admin` | `Admin123!` | Administrador del banco de sangre |
| `hospital` | `Hospital123!` | Personal del hospital sembrado |

### En local sin Docker

Requiere un SQL Server accesible (por ejemplo, el del `docker-compose.yml` levantado solo
con `docker compose up sqlserver`) y el SDK de .NET 8.

```bash
dotnet run --project src/BloodLink.Api
dotnet run --project src/BloodLink.Web
```

Ajusta `ConnectionStrings:Default` en `src/BloodLink.Api/appsettings.Development.json` si
tu SQL Server no usa la cadena por defecto.

## Cómo probarlo

```bash
dotnet test
```

Las pruebas no requieren SQL Server ni Docker: usan SQLite en memoria a través de
`WebApplicationFactory<Program>` (o conexiones SQLite independientes para la prueba de
concurrencia). Cubren:

- Reglas de compatibilidad ABO/Rh para glóbulos rojos y plasma.
- Cálculo de vida útil por componente.
- Elegibilidad de donantes (edad mínima, intervalo entre donaciones).
- Hashing de contraseñas (PBKDF2).
- Autenticación y autorización por rol (login, 401, 403 entre `Admin` y `Hospital`).
- Asignación FEFO, asignación parcial y reintento, incompatibilidad que deja una
  solicitud `Pending`, entrega (`issue`) y cancelación de solicitudes.
- Dos solicitudes reales compitiendo por la misma unidad escasa: exactamente una gana,
  sin importar el orden de ejecución (prueba de concurrencia optimista con dos conexiones
  SQLite independientes sobre una base en memoria compartida).
- Flujo completo de cita de donación → unidad en cuarentena → liberación/descarte.

## CI

`.github/workflows/ci.yml` restaura, compila y corre `dotnet test` sobre toda la solución
en cada push y pull request a `main`.
