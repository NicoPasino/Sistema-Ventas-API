# 📌 API Sistema-Ventas (demo)
FrontEnd Repo: [Sistema-Ventas](https://github.com/NicoPasino/sistema-ventas)

## 🛠️ Tecnologías utilizadas:
- Herramientas: `.NET`.
- Lenguajes: `C#`.
- Frameworks: `Entity Framework`, `ASP.NET`.
- Base de datos: `MySql`.
- Estructura: `The Clean Architecture`.
- Tests: `xUnit`, `FluentAssertions`, `NSubstitute`, `coverlet`.


## 📦 Estructura principal del proyecto
```bash
├───NicoPasino #(Host web: controladores y arranque)
│   ├─── Controllers
│   │     ├─ VentasController.cs      (base: inyección de dependencias)
│   │     ├─ Ventas.Clientes.cs
│   │     ├─ Ventas.Productos.cs      (+ endpoints de Categorías)
│   │     └─ Ventas.Ventas.cs
│   └─── Program.cs                   (DI, CORS, rate limiting, Mapster, EF)
│
├───NicoPasino.Core #(Modelos, DTOs, interfaces y errores)
│   ├─── DTO/Ventas
│   ├─── Errores                     (DataException, UpdateException, …)
│   ├─── Interfaces
│   ├─── Mapper                       (MappingConfig: Mapster)
│   └─── Modelos/Ventas
│
├───NicoPasino.Infra #(EF Core + MySQL)
│   ├─── Data                         (ventasdbContext)
│   ├─── Migrations
│   └─── Repositorio                  (RepositorioGenericoVentas<T>)
│
├───NicoPasino.Servicios #(Lógica de Negocio)
│   ├─── Servicios/Ventas             (Producto, Cliente, Venta, Categoria)
│   └─── Validaciones                 (Producto, Cliente, Venta)
│
└───NicoPasino.Tests #(Tests unitarios)
    ├─── Common
    │     ├─ Fakes                    (RepositorioGenericoVentasFake<T>)
    │     ├─ Builders                 (Entidades + DTOs)
    │     ├─ BugsConocidos.cs         (catálogo de bugs caracterizados)
    │     ├─ VentasControllerHarness.cs
    │     ├─ ResultadoHttp.cs
    │     └─ InicializadorMapster.cs
    ├─── Validadores                  (validadores y DataAnnotations de DTOs)
    ├─── Servicios                    (un archivo por servicio/método)
    ├─── Controllers                  (endpoints de VentasController)
    └─── Infraestructura              (fake + repositorio real con EF InMemory)
```

## 📦 Endpoints
Todos cuelgan de `/api/ventas` (`[Route("api/[controller]")]`).

```bash
productos/...
│   ├─── (GET)                       Trae todos los productos
│   ├─── (GET /{idPublica})          Trae un producto por su id público
│   ├─── (GET /search/{campo}/{valor?}) Busca por campo (numero, nombre, otro, proveedor) y valor
│   ├─── (POST)                      Agrega un producto
│   ├─── (PUT)                       Modifica un producto
│   ├─── (PATCH /{idPublica})        Modifica campos sueltos (ProductoPatchDto)
│   └─── (DELETE /{id})              Baja lógica (usa idPublica internamente)
|
clientes/...
│   ├─── (GET)                       Trae todos los clientes
│   ├─── (GET /{id})                 Trae un cliente por documento
│   ├─── (GET /search/{campo}/{valor?}) Busca por campo (numero, nombre, otro) y valor
│   ├─── (POST)                      Agrega un cliente
│   ├─── (PUT)                       Modifica un cliente
│   ├─── (PATCH /{id})               Modifica campos sueltos (ClientePatchDto)
│   └─── (DELETE /{id})              Baja lógica
|
ventas/...
│   ├─── (GET)                       Trae todas las ventas
│   ├─── (GET /{id})                 Trae una venta (VentaDetalleDto)
│   ├─── (GET /search/{campo}/{valor?}) Busca por campo (numero, nombre, otro) y valor
│   ├─── (POST)                      Agrega una venta
│   └─── (DELETE /{id})              Elimina una venta (siempre 500: sin implementar)
|
categorias/...
    ├─── (GET)                       Trae todas las categorías
    └─── (GET /{id})                 Trae una categoría
```

Detalle: en `productos` el `{idPublica}` es el número expuesto al front
(no la PK interna); en `clientes` el `{id}` es el documento. En `ventas` el
`PUT` está **comentado** en el código y no existe en la ruta.

## 🧪 Tests

Los tests unitarios viven en `NicoPasino.Tests` y usan **xUnit** + **FluentAssertions**
(+ **NSubstitute** y **EF Core InMemory**). No levantan base de datos ni el servidor web:
los repositorios se reemplazan por un fake in-memory que evalúa los filtros y el orden
con LINQ to Objects; el repositorio real `RepositorioGenericoVentas<T>` se prueba contra
un contexto EF Core InMemory. La suite corre en CI vía `.github/workflows/tests.yml`.

[![Tests](https://github.com/NicoPasino/Sistema-Ventas-API/actions/workflows/tests.yml/badge.svg)](https://github.com/NicoPasino/Sistema-Ventas-API/actions/workflows/tests.yml)

### Qué se cubre hoy

**740 tests** (aprobados), organizados por carpeta:

| Carpeta | Cobertura |
|---|---|
| `Servicios` | `ProductoServicio`, `ClienteServicio`, `VentaServicio` y `CategoriaServicio`: lectura, create, update, patch, enable, validaciones y guardas de null |
| `Mapper` | Los 5 mapeos de `MappingConfig.VentasMappings` (`Producto↔ProductoDto`, `Venta→VentaDto`, `Venta→VentaDetalleDto`, `Ventaporproducto→VentaporproductoDto`, `Cliente→ClienteDto`) y los riesgos de navegación sin `Include` |
| `Validadores` | `ProductoValidador`, `ClienteValidador`, `VentaValidador` (incluida la rama async con categoría/producto inexistentes) y `DataAnnotationsDtoTests` (reglas y mensajes de los DTOs) |
| `Controllers` | Endpoints de `VentasController` (`.Ventas.Clientes`, `.Ventas.Productos` y `.Ventas.Ventas`): todas las rutas (GET, search, POST, PUT, PATCH, DELETE) |
| `Infraestructura` | `RepositorioGenericoVentas<T>` real sobre EF InMemory (los 8 métodos) y `RepositorioGenericoVentasFake<T>` |

Cada carpeta usa un patrón distinto a propósito: los servicios se prueban contra el
fake que **registra** responsabilidades de cada repositorio y de verdad evalúa el
`Expression<Func<T, bool>>` de los filtros; los controllers se arman con un
`VentasControllerHarness` (NSubstitute para los 4 `IServicioGenerico`, instancias
reales de `ProductoServicio`/`ClienteServicio` para los PATCH y `VentaServicio` para
los DELETE); el repositorio real se ejercita con InMemory.

```bash
# Correr toda la suite
dotnet test

# Con reporte de cobertura (formato cobertura, en XML)
dotnet test --settings .runsettings --collect:"XPlat Code Coverage"

# Filtrar por nombre
dotnet test --filter "FullyQualifiedName~ProductoValidador"
```

El reporte HTML de cobertura se genera con `reportgenerator` sobre el XML de coverlet.

### Qué NO se cubre y por qué

- **Integración HTTP** (levantar `Kestrel` y pegarle con un cliente real): los DTOs
  internos de las rutas se formatean con objetos anónimos y requiere levantar
  `Program.Main`, que lee `Environment.GetEnvironmentVariable("ventas")` y sin esa
  variable explota en `UseMySql(null, …)`.
- **Base de datos real (MySQL)**: las pruebas del repositorio usan el proveedor
  InMemory de EF Core; no hay MySQL en CI.
- **Migraciones**: no se ejercitan (`CreateDatabase`/`Migrate` quedan fuera).

### Bugs conocidos documentados con tests

Una parte de la suite son **tests de caracterización**: fijan el comportamiento
actual aunque sea erróneo y lo marcan como `// BUG: ver issue #N`, referenciando
`Common/BugsConocidos.cs`. Sirven de red de seguridad antes de corregir.

| Bug | Problema |
|---|---|
| `#35` | `GET /api/ventas/clientes/{id}` nunca devuelve 404 (rama muerta) |
| `#34` | `PATCH /productos` con `Enable=false` desincroniza `Estado` |
| `#39` | `DELETE /clientes/{id}` ignora el resultado del servicio |
| `#37` | `DELETE /ventas/{id}` siempre responde 500 (`Enable` sin implementar) |
| `#38` | `GET /api/ventas/ventas/{id}` nunca devuelve 404 (`NotFound` comentado) |
| `#36` | `search` con `campo` vacío responde 500 en vez de 400 |
| `#41` | `VentaServicio.Create` guarda sin transacción |
| `#48` | `RepositorioGenericoVentas` usa `AsNoTracking()` de forma inconsistente (rompe los `Update`/PATCH) |

### Cómo escribir un test

| Qué necesitás | Usá |
|---|---|
| Una entidad válida de arranque | `Common/Builders/EntidadesTestBuilder.cs` (`ProductoBuilder`, `ClienteBuilder`, …) |
| Un DTO de entrada válido | `Common/Builders/DtoTestBuilder.cs` |
| Reemplazar `IRepositorioGenericoVentas<T>` | `Common/Fakes/RepositorioGenericoVentasFake.cs` |

El fake registra las llamadas (`VecesUpdate`, `UltimaEntidadActualizada`, `UltimoOrden`,
…) para poder afirmar *qué* le pidió el servicio al repositorio, y evalúa de verdad el
`Expression<Func<T, bool>>` de los filtros.

`MappingConfig.VentasMappings()` se carga una sola vez por ensamblado desde
`Common/InicializadorMapster.cs`, porque `TypeAdapterConfig` es estado estático global
y en producción se inicializa en `Program.Main`.

## 🧑‍💻 Autor:
Nicolás Pasino - nico_pasino@hotmail.com

[LinkedIn](https://www.linkedin.com/in/nicolas-pasino/) | [Portfolio](https://nicopasino.space)
