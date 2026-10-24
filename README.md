# API Sistema de Ventas

[![Tests](https://github.com/NicoPasino/Sistema-Ventas-API/actions/workflows/tests.yml/badge.svg)](https://github.com/NicoPasino/Sistema-Ventas-API/actions/workflows/tests.yml)
[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4)](https://dotnet.microsoft.com/)
[![MySQL](https://img.shields.io/badge/MySQL-8.0-4479A1)](https://www.mysql.com/)

API REST para la gestión de ventas: productos, categorías, clientes y ventas.
Implementada en **.NET 9 / C#** con **Entity Framework Core + MySQL**, separada en
capas (Clean Architecture) y cubierta por una suite de tests unitarios.

> Frontend: [NicoPasino/sistema-ventas](https://github.com/NicoPasino/sistema-ventas)

## Contenido

- [Características](#características)
- [Stack](#stack)
- [Arquitectura](#arquitectura)
- [Requisitos](#requisitos)
- [Puesta en marcha](#puesta-en-marcha)
- [Endpoints](#endpoints)
- [Búsqueda y filtros](#búsqueda-y-filtros)
- [Ejemplos de uso](#ejemplos-de-uso)
- [Comportamiento transversal](#comportamiento-transversal)
- [Tests](#tests)
- [Estructura del repositorio](#estructura-del-repositorio)
- [Autor](#autor)

## Características

- CRUD de productos y clientes con **baja lógica** mediante `PATCH { "activo": bool }`.
- Registro de ventas con validación de cliente, productos, stock y descuento automático.
- Búsqueda y ordenamiento por campos (`numero`, `nombre`, `otro`, `proveedor`, …).
- DTOs de entrada/salida separados de las entidades, con mapeo vía **Mapster**.
- Validación en dos capas: **DataAnnotations** en los DTOs y validadores de negocio.
- Errores de negocio como `400`, recursos inexistentes como `404` y JSON consistente.
- CORS configurable, **rate limiting** y cabeceras de seguridad.
- **732 tests** unitarios/de integración.

## Stack

| Categoría | Tecnología |
|---|---|
| Runtime | .NET 9 (`net9.0`) |
| Framework web | ASP.NET Core |
| Acceso a datos | Entity Framework Core 9 + Pomelo MySQL |
| Base de datos | MySQL 8.0 |
| Mapeo | Mapster |
| Configuración | `dotenv.net` (archivo `.env`) |
| Tests | xUnit, FluentAssertions, NSubstitute, EF Core InMemory, WebApplicationFactory, coverlet |
| CI | GitHub Actions (`dotnet restore` → `build` → `test` + cobertura) |

## Arquitectura

El proyecto sigue una separación por capas con dependencias unidireccionales:

```
NicoPasino (Web / API)
    ├── NicoPasino.Servicios (lógica de negocio + validaciones)
    │        └── NicoPasino.Infra (EF Core + MySQL) ──┐
    └── NicoPasino.Core (modelos, DTOs, interfaces) ◄─┘
```

| Proyecto | Responsabilidad |
|---|---|
| `NicoPasino` | Host web: `Program.cs`, controladores, CORS, rate limiting, middleware de errores y seguridad. |
| `NicoPasino.Servicios` | Reglas de negocio (`ProductoServicio`, `ClienteServicio`, `VentaServicio`, `CategoriaServicio`) y validadores. |
| `NicoPasino.Infra` | `ventasdbContext`, migraciones EF Core y `RepositorioGenericoVentas<T>`. |
| `NicoPasino.Core` | Entidades, DTOs, interfaces (`IServicioGenerico`, `IRepositorioGenericoVentas`), errores y configuración de Mapster. |
| `NicoPasino.Tests` | Suite de tests y utilidades (builders, fakes, harness de controllers). |

## Requisitos

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- MySQL 8.0 en ejecución (probado con 8.0.39)
- Git

## Puesta en marcha

1. **Clonar el repositorio**

   ```bash
   git clone https://github.com/NicoPasino/Sistema-Ventas-API.git
   cd Sistema-Ventas-API
   ```

2. **Configurar variables de entorno**

   Creá `NicoPasino/.env` a partir del ejemplo:

   ```bash
   cp NicoPasino/.env.example NicoPasino/.env
   ```

   | Variable | Obligatoria | Descripción |
   |---|---|---|
   | `ventas` | Sí | Cadena de conexión de MySQL (sin ella la app falla al iniciar). |
   | `ACCEPTED_ORIGINS` | No | Orígenes permitidos por CORS, separados por coma. |

   ```env
   ventas=Server=localhost;Port=3306;Database=ventas;Uid=root;Pwd=tu_password;
   ACCEPTED_ORIGINS=http://localhost:4200,http://localhost:5173
   ```

3. **Restaurar, compilar y ejecutar**

   ```bash
   dotnet restore
   dotnet build
   dotnet run --project NicoPasino
   ```

   La API queda disponible en `http://localhost:5230` (perfil `http`) y
   `https://localhost:7267` (perfil `https`), según `launchSettings.json`.

4. **Base de datos y migraciones**

   La aplicación **no aplica migraciones automáticamente**. Las migraciones están en
   `NicoPasino.Infra/Migrations` y podés aplicarlas con:

   ```bash
   dotnet tool restore
   dotnet ef database update --project NicoPasino.Infra --startup-project NicoPasino
   ```

   Asegurate de que la variable `ventas` esté disponible en el entorno al ejecutar EF.

## Endpoints

Todos los endpoints cuelgan de `/api/Ventas` (el routing de ASP.NET Core no distingue
mayúsculas/minúsculas).

### Productos

| Método | Ruta | Descripción | Respuestas |
|---|---|---|---|
| `GET` | `/api/Ventas/Productos` | Lista todos los productos (con categoría). | `200`, `500` |
| `GET` | `/api/Ventas/Productos/search/{campo}/{valor?}` | Busca/ordena productos por campo. | `200`, `400`, `500` |
| `GET` | `/api/Ventas/Productos/{idPublica}` | Obtiene un producto por su **id público**. | `200`, `400`, `404`, `500` |
| `POST` | `/api/Ventas/Productos` | Crea un producto. | `202`, `400`, `500` |
| `PUT` | `/api/Ventas/Productos` | Modifica un producto completo. | `202`, `400`, `500` |
| `PATCH` | `/api/Ventas/Productos/{idPublica}` | Modifica campos sueltos; `activo` habilita/deshabilita. | `202`, `400`, `500` |

### Clientes

| Método | Ruta | Descripción | Respuestas |
|---|---|---|---|
| `GET` | `/api/Ventas/Clientes` | Lista todos los clientes (con cantidad de compras). | `200`, `500` |
| `GET` | `/api/Ventas/Clientes/search/{campo}/{valor?}` | Busca/ordena clientes por campo. | `200`, `400`, `500` |
| `GET` | `/api/Ventas/Clientes/{documento}` | Obtiene un cliente por su **documento (DNI)**. | `200`, `400`, `404`, `500` |
| `POST` | `/api/Ventas/Clientes` | Crea un cliente. | `200`, `400`, `500` |
| `PUT` | `/api/Ventas/Clientes` | Modifica un cliente completo. | `200`, `400`, `500` |
| `PATCH` | `/api/Ventas/Clientes/{documento}` | Modifica campos sueltos; `activo` habilita/deshabilita. | `200`, `400`, `500` |

### Ventas

| Método | Ruta | Descripción | Respuestas |
|---|---|---|---|
| `GET` | `/api/Ventas/Ventas` | Lista todas las ventas con su detalle. | `200`, `500` |
| `GET` | `/api/Ventas/Ventas/search/{campo}/{valor?}` | Busca/ordena ventas por campo. | `200`, `400`, `500` |
| `GET` | `/api/Ventas/Ventas/{id}` | Obtiene una venta por su **id interno**. | `200`, `400`, `404`, `500` |
| `POST` | `/api/Ventas/Ventas` | Registra una venta y descuenta stock. | `200`, `400`, `500` |

> `PUT /api/Ventas/Ventas` está comentado en el código y **no existe** en la práctica.

### Categorías

| Método | Ruta | Descripción | Respuestas |
|---|---|---|---|
| `GET` | `/api/Ventas/Categorias` | Lista todas las categorías. | `200`, `500` |
| `GET` | `/api/Ventas/Categorias/{id}` | Obtiene una categoría por id. | `200`, `400`, `404`, `500` |

### Notas

- **No existe `DELETE` en ningún recurso**: no hay baja física. Un `DELETE` responde
  `405 Method Not Allowed`.
- La baja lógica de productos y clientes se hace únicamente con
  `PATCH { "activo": false }`.
- `IServicioGenerico.Enable(...)` queda como contrato, pero lanza
  `NotImplementedException` en todos los servicios.
- Los errores de validación/negocio devuelven `{ "message": "..." }`.

## Búsqueda y filtros

`GET /api/Ventas/{recurso}/search/{campo}/{valor?}`. El `campo` no distingue
mayúsculas/minúsculas. Si se omite `valor`, solo se aplica el ordenamiento.

| Recurso | Campo | Filtra por | Orden |
|---|---|---|---|
| Productos | `numero` | id público | id ascendente |
| Productos | `nombre` / `nombredesc` | nombre | nombre asc/desc |
| Productos | `otro` / `otrodesc` | nombre de categoría | categoría asc/desc |
| Productos | `proveedor` / `proveedordesc` | proveedor | proveedor asc/desc |
| Clientes | `numero` / `numerodesc` | documento | documento asc/desc |
| Clientes | `nombre` / `nombredesc` | nombre | nombre asc/desc |
| Clientes | `otro` / `otrodesc` | correo | correo asc/desc |
| Ventas | `numero` | número de venta | número ascendente |
| Ventas | `nombre` / `nombredesc` | nombre del cliente | cliente asc/desc |
| Ventas | `otro` / `otrodesc` | detalle | detalle asc/desc |

Un campo no soportado devuelve `400` con el listado de campos válidos. En productos,
cuando se envía `valor`, la búsqueda solo considera productos activos.

## Ejemplos de uso

### Crear un producto

```http
POST /api/Ventas/Productos
Content-Type: application/json
```

```json
{
  "idCategoria": 1,
  "nombre": "Teclado mecánico",
  "descripcion": "Switch brown",
  "cantidad": 25,
  "precio": 54999.90,
  "stockMinimo": 5,
  "stockMaximo": 50,
  "proveedor": "Logitech"
}
```

El servidor genera `idPublica`, asigna `activo = true` y completa las fechas.

### Deshabilitar un producto

```http
PATCH /api/Ventas/Productos/1234567
Content-Type: application/json
```

```json
{ "activo": false }
```

### Crear un cliente

```json
{
  "nombre": "Ana Gómez",
  "correo": "ana@example.com",
  "documento": 30111222,
  "telefono": "+54 11 5555-5555"
}
```

### Registrar una venta

```http
POST /api/Ventas/Ventas
Content-Type: application/json
```

```json
{
  "dni": 30111222,
  "detalle": "Retiro en local",
  "fechaVenta": "2026-10-23T15:30:00Z",
  "itemsId": [1234567, 7654321],
  "itemsCant": [2, 1]
}
```

- `itemsId` son los **ids públicos** de los productos.
- `itemsId` e `itemsCant` deben tener la misma longitud.
- El cliente debe existir; los productos deben existir, estar activos y tener stock.
- La venta se registra y el stock se descuenta automáticamente.

## Comportamiento transversal

- **Rate limiting**: 100 peticiones por minuto por IP; excedido devuelve `429`.
- **CORS**: orígenes definidos en `ACCEPTED_ORIGINS`.
- **Seguridad**: HSTS en producción y cabeceras `X-Content-Type-Options`,
  `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy`,
  `Content-Security-Policy` y `X-Robots-Tag`.
- **Errores**: `404` en JSON (`{ "message": "Recurso no encontrado" }`) y
  `ProblemDetails` para excepciones no controladas en producción.
- **Validaciones**: DataAnnotations en los DTOs + validadores de negocio
  (documento/correo únicos, categoría existente, stock mínimo/máximo, fecha no futura,
  stock suficiente, etc.).
- **Inyección de dependencias**: repositorio genérico, servicios, validadores,
  `TimeProvider` y `IGeneradorCodigos`.

## Tests

[![Tests](https://github.com/NicoPasino/Sistema-Ventas-API/actions/workflows/tests.yml/badge.svg)](https://github.com/NicoPasino/Sistema-Ventas-API/actions/workflows/tests.yml)

La suite vive en `NicoPasino.Tests` y corre sin base de datos ni servidor externo:

- **xUnit** como runner, **FluentAssertions** para aserciones.
- **NSubstitute** para simular los servicios en los tests de controllers.
- **EF Core InMemory** para probar el repositorio real.
- **WebApplicationFactory** para verificar el routing (por ejemplo, los `405`).

**732 tests aprobados** y una cobertura de **99.85 % de líneas** y **99.37 % de ramas**
(proyectos `Core`, `Servicios` e `Infra`; migraciones excluidas).

| Carpeta | Cobertura |
|---|---|
| `Servicios` | Lectura, creación, actualización, patch, enable, validaciones y guardas de null de los cuatro servicios. |
| `Validadores` | `ProductoValidador`, `ClienteValidador`, `VentaValidador` y reglas de DataAnnotations de los DTOs. |
| `Mapper` | Los cinco mapeos de `MappingConfig` y riesgos de navegación sin `Include`. |
| `Controllers` | Rutas GET, search, POST, PUT y PATCH de `VentasController`, más los `405` de `DELETE`. |
| `Infraestructura` | `RepositorioGenericoVentas<T>` sobre EF InMemory y el fake de repositorio. |

Además hay **tests de caracterización** que fijan el comportamiento actual de bugs
conocidos (referenciados en `Common/BugsConocidos.cs`) para que, al corregirlos, la
suite señale exactamente qué cambió.

```bash
# Toda la suite
dotnet test

# Con cobertura (XML cobertura en TestResults/)
dotnet test --settings .runsettings --collect:"XPlat Code Coverage"

# Filtrar por nombre
dotnet test --filter "FullyQualifiedName~ProductoValidador"
```

El workflow `.github/workflows/tests.yml` restaura, compila y ejecuta la suite en
Release con cobertura en cada push y pull request.

## Estructura del repositorio

```text
├── NicoPasino/                      # Host web
│   ├── Controllers/
│   │   ├── VentasController.cs      # Ruta base, DI y rate limiting
│   │   ├── Ventas.Productos.cs      # Productos + Categorías
│   │   ├── Ventas.Clientes.cs       # Clientes
│   │   └── Ventas.Ventas.cs         # Ventas
│   ├── Program.cs                   # Arranque, DI, CORS, seguridad y middleware
│   ├── .env.example                 # Variables de entorno de ejemplo
│   └── appsettings.json
│
├── NicoPasino.Core/                 # Núcleo
│   ├── DTO/Ventas/                  # ProductoDto, ClienteDto, VentaDto, Patch DTOs…
│   ├── Errores/                     # DataException, UpdateException
│   ├── Interfaces/                  # IServicioGenerico, IRepositorioGenericoVentas…
│   ├── Mapper/MappingConfig.cs      # Configuración de Mapster
│   └── Modelos/Ventas/              # Producto, Cliente, Venta, Categoria, Ventaporproducto
│
├── NicoPasino.Infra/                # Infraestructura
│   ├── Data/ventasdbContext.cs
│   ├── Migrations/
│   └── Repositorio/RepositorioGenericoVentas.cs
│
├── NicoPasino.Servicios/            # Lógica de negocio
│   ├── Servicios/Ventas/            # Producto, Cliente, Venta y Categoria
│   ├── Servicios/GeneradorCodigos.cs
│   └── Validaciones/                # ProductoValidador, ClienteValidador, VentaValidador
│
├── NicoPasino.Tests/                # Tests
│   ├── Common/                      # Builders, fakes, harness, BugsConocidos
│   ├── Controllers/
│   ├── Infraestructura/
│   ├── Mapper/
│   ├── Servicios/
│   └── Validadores/
│
├── .github/workflows/tests.yml      # CI: build + tests + cobertura
├── .runsettings                     # Configuración de cobertura
├── dotnet-tools.json                # dotnet-ef local
└── SistemaVentas.sln
```

## Autor

Nicolás Pasino — nico_pasino@hotmail.com

[LinkedIn](https://www.linkedin.com/in/nicolas-pasino/) · [Portfolio](https://nicopasino.space)
