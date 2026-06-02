using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Veritas.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClaimEvidenceAndSourceAuthorEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AuthorEntityId",
                table: "sources",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "claim_evidence_links",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvidenceItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Stance = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Note = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_claim_evidence_links", x => x.Id);
                    table.ForeignKey(
                        name: "FK_claim_evidence_links_claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_claim_evidence_links_evidence_items_EvidenceItemId",
                        column: x => x.EvidenceItemId,
                        principalTable: "evidence_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                name: "IX_sources_AuthorEntityId",
                table: "sources",
                column: "AuthorEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_claim_evidence_links_ClaimId",
                table: "claim_evidence_links",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_claim_evidence_links_ClaimId_EvidenceItemId",
                table: "claim_evidence_links",
                columns: new[] { "ClaimId", "EvidenceItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_claim_evidence_links_EvidenceItemId",
                table: "claim_evidence_links",
                column: "EvidenceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_dossier_entities_DossierId",
                table: "dossier_entities",
                column: "DossierId");

            migrationBuilder.CreateIndex(
                name: "IX_dossier_entity_relations_DossierId",
                table: "dossier_entity_relations",
                column: "DossierId");

            migrationBuilder.CreateIndex(
                name: "IX_dossier_entity_relations_FromEntityId_ToEntityId_RelationTy~",
                table: "dossier_entity_relations",
                columns: new[] { "FromEntityId", "ToEntityId", "RelationType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_dossier_entity_relations_ToEntityId",
                table: "dossier_entity_relations",
                column: "ToEntityId");

            migrationBuilder.AddForeignKey(
                name: "FK_sources_dossier_entities_AuthorEntityId",
                table: "sources",
                column: "AuthorEntityId",
                principalTable: "dossier_entities",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_sources_dossier_entities_AuthorEntityId",
                table: "sources");

            migrationBuilder.DropTable(
                name: "claim_evidence_links");

            migrationBuilder.DropTable(
                name: "dossier_entity_relations");

            migrationBuilder.DropTable(
                name: "dossier_entities");

            migrationBuilder.DropIndex(
                name: "IX_sources_AuthorEntityId",
                table: "sources");

            migrationBuilder.DropColumn(
                name: "AuthorEntityId",
                table: "sources");
        }
    }
}
