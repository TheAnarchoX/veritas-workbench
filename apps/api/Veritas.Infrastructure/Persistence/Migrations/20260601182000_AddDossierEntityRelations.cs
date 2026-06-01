using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Veritas.Infrastructure.Persistence;

#nullable disable

namespace Veritas.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(VeritasDbContext))]
    [Migration("20260601182000_AddDossierEntityRelations")]
    public partial class AddDossierEntityRelations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dossier_entity_relations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DossierId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    RelationType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Confidence = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    EvidenceBasis = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dossier_entity_relations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_dossier_entity_relations_dossier_entities_FromEntityId",
                        column: x => x.FromEntityId,
                        principalTable: "dossier_entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_dossier_entity_relations_dossier_entities_ToEntityId",
                        column: x => x.ToEntityId,
                        principalTable: "dossier_entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_dossier_entity_relations_dossiers_DossierId",
                        column: x => x.DossierId,
                        principalTable: "dossiers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_dossier_entity_relations_DossierId",
                table: "dossier_entity_relations",
                column: "DossierId");

            migrationBuilder.CreateIndex(
                name: "IX_dossier_entity_relations_FromEntityId_ToEntityId_RelationType",
                table: "dossier_entity_relations",
                columns: new[] { "FromEntityId", "ToEntityId", "RelationType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_dossier_entity_relations_ToEntityId",
                table: "dossier_entity_relations",
                column: "ToEntityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "dossier_entity_relations");
        }
    }
}
