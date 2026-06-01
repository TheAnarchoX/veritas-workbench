using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Veritas.Infrastructure.Persistence;

#nullable disable

namespace Veritas.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(VeritasDbContext))]
    [Migration("20260601170000_AddDossierEntities")]
    public partial class AddDossierEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dossier_entities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DossierId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Name = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    Handle = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: true),
                    Platform = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    Confidence = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dossier_entities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_dossier_entities_dossiers_DossierId",
                        column: x => x.DossierId,
                        principalTable: "dossiers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_dossier_entities_DossierId",
                table: "dossier_entities",
                column: "DossierId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "dossier_entities");
        }
    }
}
