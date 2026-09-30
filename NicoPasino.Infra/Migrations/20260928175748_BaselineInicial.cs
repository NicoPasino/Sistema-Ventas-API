using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NicoPasino.Infra.Migrations
{
    /// <summary>
    /// Migración baseline. El esquema ya existía en la base antes de adoptar EF Core Migrations,
    /// por lo que acá NO se ejecuta ninguna operación: sólo se registra el modelo actual
    /// (ver ventasdbContextModelSnapshot.cs) para que la siguiente migración contenga
    /// únicamente el diff real contra la base.
    /// </summary>
    public partial class BaselineInicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
