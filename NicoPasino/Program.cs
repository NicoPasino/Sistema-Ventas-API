using dotenv.net;
using Microsoft.EntityFrameworkCore;
using NicoPasino.Core.DTO.Ventas;
using NicoPasino.Core.Interfaces;
using NicoPasino.Core.Mapper;
using NicoPasino.Core.Modelos.Ventas;
using NicoPasino.Infra.Data;
using NicoPasino.Infra.Repositorio;
using NicoPasino.Servicios.Servicios.Ventas;
using System.Threading.RateLimiting;

namespace NicoPasino
{
    public class Program
    {
        public static void Main(string[] args) {
            var builder = WebApplication.CreateBuilder(args);

            // Forzar a que escuche en todas las interfaces en el puerto 5000
            // para poder conectar dispositivos en la misma red (android)
            //builder.WebHost.UseUrls("http://0.0.0.0:5000");

            DotEnv.Load(); // leer .env

            // definir reglas CORS
            var misReglasCORS = "_misReglasCORS";
            var acceptedOrigins = Environment.GetEnvironmentVariable("ACCEPTED_ORIGINS")?.Split(',') ?? [""];
            builder.Services.AddCors(options => {
                options.AddPolicy(name: misReglasCORS, policy => { policy.WithOrigins(acceptedOrigins).AllowAnyHeader().AllowAnyMethod(); });
            });

            builder.Services.AddControllersWithViews();

            // Configuraciones para Mapster
            MappingConfig.VentasMappings();

            // conexi�n a ventas
            var ventasdb = Environment.GetEnvironmentVariable("ventas");
            builder.Services.AddDbContext<ventasdbContext>(options =>
                options.UseMySql(ventasdb, new MySqlServerVersion(new Version(8, 0, 39)))
            );

            // permitir inyeccion (Repositorio => conexion con dbContext)
            builder.Services.AddScoped(typeof(IRepositorioGenericoVentas<>), typeof(RepositorioGenericoVentas<>));

            // Servicios
            builder.Services.AddScoped<IServicioGenerico<Producto, ProductoDto, ProductoDto>, ProductoServicio>();
            builder.Services.AddScoped<ProductoServicio>();
            builder.Services.AddScoped<ClienteServicio>();
            builder.Services.AddScoped<IServicioGenerico<Venta, VentaDto, VentaDetalleDto>, VentaServicio>();
            builder.Services.AddScoped<IServicioGenerico<Cliente, ClienteDto, ClienteDto>, ClienteServicio>();
            builder.Services.AddScoped<IServicioGenerico<Categoria, CategoriaDto, CategoriaDto>, CategoriaServicio>();

            // cambiar texto de validacion de la vista
            builder.Services.AddRazorPages()
            .AddMvcOptions(options => {
                options.ModelBindingMessageProvider.SetValueMustNotBeNullAccessor(
                    _ => "El campo es requerido.");
            });

            builder.Services.AddHsts(options => {
                options.Preload = true;
                options.IncludeSubDomains = true;
                options.MaxAge = TimeSpan.FromDays(365);
            });

            builder.Services.AddRateLimiter(options => {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.AddPolicy("general", httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 100,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0,
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                        }
                    )
                );
            });

            var app = builder.Build();

            // crear una base de datos desde de una migracion
            /*using (var scope = app.Services.CreateScope()) {
                var context = scope.ServiceProvider.GetRequiredService<moviesdbContext>();
                context.Database.Migrate();
            }*/


            app.Use(async (context, next) => {
                var h = context.Response.Headers;
                h["X-Content-Type-Options"] = "nosniff";
                h["X-Frame-Options"] = "DENY";
                h["Referrer-Policy"] = "strict-origin-when-cross-origin";
                h["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
                h["Content-Security-Policy"] = "default-src 'self'; script-src 'self' 'unsafe-inline' https://fonts.googleapis.com; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; img-src 'self' data: https:; font-src 'self' https://fonts.gstatic.com data:; connect-src 'self' https://nicopasino.space https://*.nicopasino.space http://localhost:* http://127.0.0.1:*; frame-ancestors 'none'; base-uri 'self'; form-action 'self'; object-src 'none'";
                h["X-Robots-Tag"] = "noindex, nofollow";
                await next();
            });

            // Middleware para manejar códigos de estado: (re-ejecuta la petición internamente)
            app.UseStatusCodePagesWithReExecute("/Home/NotFound", "?statusCode={0}");

            // Middleware para Error 500
            if (!app.Environment.IsDevelopment()) {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseRouting();
            app.UseRateLimiter();

            // Aplicar la política de CORS
            app.UseCors(misReglasCORS);

            app.UseAuthorization();


            app.MapStaticAssets();
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}")
                .WithStaticAssets();

            app.Run();
        }
    }
}
