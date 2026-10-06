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
├───NicoPasino #(Controladores)
│   ├─── Controllers
│   │     ├─ VentasController.cs
│   │     ├─ Ventas.Clientes.cs
│   │     ├─ Ventas.Productos.cs
│   │     └─ Ventas.Ventas.cs
│   └─── Program.cs
|
├───NicoPasino.Core #(Modelos e Interfaces)
│   ├─── DTO
│   ├─── Interfaces
│   ├─── Mapper
│   ├─── Modelos
|   |     ├─ Categoria.cs
|   |     ├─ Cliente.cs
|   |     ├─ Producto.cs
|   |     ├─ Venta.cs
|   |     └─ Ventaporproducto.cs
│   └─── Utils
|
├───NicoPasino.Infra #(Conexiones con DB)
│   ├─── Data
|   |     └─ ventasdbContext.cs
│   └─── Repositorio
|
└───NicoPasino.Servicios #(Lógica de Negocio)
    ├─── Servicios
    |     ├─ CategoriaServicio.cs
    |     ├─ ClienteServicio.cs
    |     ├─ ProductoServicio.cs
    |     └─ VentaServicio.cs
    └─── Validaciones
          ├─ ProductoValidador.cs
          ├─ ClienteValidador.cs
          └─ VentaValidador.cs

└───NicoPasino.Tests #(Tests unitarios)
    ├─── Common
    |     ├─ Fakes
    |     |     └─ RepositorioGenericoVentasFake.cs
    |     ├─ Builders
    |     |     ├─ DtoTestBuilder.cs
    |     |     └─ EntidadesTestBuilder.cs
    |     └─ InicializadorMapster.cs
    ├─── Infraestructura
    ├─── Servicios
    │     ├─ ProductoServicioLecturaTests.cs        (GetAll, GetById, busqueda)
    │     ├─ ProductoServicioCreateTests.cs         (Create, IdPublica unico)
    │     ├─ ProductoServicioUpdateTests.cs         (Update)
    │     ├─ ProductoServicioPatchEnableTests.cs    (Patch, Enable)
    │     ├─ ClienteServicioLecturaTests.cs
    │     ├─ ClienteServicioEscrituraTests.cs       (Create, Update, Patch, Enable)
    │     ├─ VentaServicioLecturaTests.cs
    │     ├─ VentaServicioCreateTests.cs
    │     ├─ VentaServicioUpdateEnableTests.cs
    │     └─ ServiciosConstructorTests.cs           (guardas de null)
    └─── Validadores
```

## 📦 Endpoints
```bash
├───/api/ventas/productos/...
│   ├─── (GET)            Trae todos los productos
│   ├─── (GET /[id])      Trae un producto por ID
│   ├─── (GET /search/[campo]/[valor?]) Trae un producto por campo y valor(opcional)
│   ├─── (POST)           Agrega un producto
│   ├─── (PUT)            Modifica un producto
│   └─── (DELETE /[id])   Elimina un producto
|
├───/api/ventas/clientes/...
│   ├─── (GET)            Trae todos los clientes
|   ├─── (GET /[id])      Trae un cliente por ID
|   ├─── (GET /search/[campo]/[valor?]) Trae un cliente por campo y valor(opcional)
|   ├─── (POST)           Agrega un cliente
|   ├─── (PUT)            Modifica un cliente
|   └─── (DELETE /[id])   Elimina un cliente
|
└───/api/ventas/ventas/...
    ├─── (GET)            Trae todas las ventas
    ├─── (GET /[id])      Trae una venta por ID
    ├─── (GET /search/[campo]/[valor?]) Trae una venta por campo y valor(opcional)
    ├─── (POST)           Agrega una venta
    ├─── (PUT)            Modifica una venta
    └─── (DELETE /[id])   Elimina una venta
```

## 🧪 Tests

Los tests unitarios viven en `NicoPasino.Tests` y usan **xUnit** + **FluentAssertions**.
No levantan base de datos ni el servidor web: los repositorios se reemplazan por un
fake in-memory que evalúa los filtros y el orden con LINQ to Objects.

```bash
# Correr toda la suite
dotnet test

# Con reporte de cobertura (formato cobertura, en XML)
dotnet test --settings .runsettings --collect:"XPlat Code Coverage"

# Filtrar por nombre
dotnet test --filter "FullyQualifiedName~ProductoValidador"
```

El reporte HTML de cobertura se genera con `reportgenerator` sobre el XML de coverlet.

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
