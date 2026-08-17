using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditVitals.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "projects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    root_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    normalized_host = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_audit_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_projects", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "audits",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    requested_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    failed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    requested_page_limit = table.Column<int>(type: "integer", nullable: false),
                    crawled_page_count = table.Column<int>(type: "integer", nullable: false),
                    discovered_page_count = table.Column<int>(type: "integer", nullable: false),
                    health_score = table.Column<int>(type: "integer", nullable: true),
                    is_sampled_audit = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audits", x => x.id);
                    table.ForeignKey(
                        name: "fk_audits_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "audit_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    audit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    available_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    locked_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    locked_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_jobs", x => x.id);
                    table.ForeignKey(
                        name: "fk_audit_jobs_audits_audit_id",
                        column: x => x.audit_id,
                        principalTable: "audits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "crawled_pages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    audit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    normalized_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    status_code = table.Column<int>(type: "integer", nullable: false),
                    content_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    meta_description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    h1_count = table.Column<int>(type: "integer", nullable: false),
                    canonical_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    word_count = table.Column<int>(type: "integer", nullable: false),
                    response_time_milliseconds = table.Column<long>(type: "bigint", nullable: false),
                    is_indexable = table.Column<bool>(type: "boolean", nullable: false),
                    crawl_depth = table.Column<int>(type: "integer", nullable: false),
                    content_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_crawled_pages", x => x.id);
                    table.ForeignKey(
                        name: "fk_crawled_pages_audits_audit_id",
                        column: x => x.audit_id,
                        principalTable: "audits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "audit_findings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    audit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    crawled_page_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rule_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    why_it_matters = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    recommended_fix = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    evidence = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_findings", x => x.id);
                    table.ForeignKey(
                        name: "fk_audit_findings_audits_audit_id",
                        column: x => x.audit_id,
                        principalTable: "audits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_audit_findings_crawled_pages_crawled_page_id",
                        column: x => x.crawled_page_id,
                        principalTable: "crawled_pages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_findings_audit_id",
                table: "audit_findings",
                column: "audit_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_findings_crawled_page_id",
                table: "audit_findings",
                column: "crawled_page_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_findings_rule_code",
                table: "audit_findings",
                column: "rule_code");

            migrationBuilder.CreateIndex(
                name: "ix_audit_findings_severity",
                table: "audit_findings",
                column: "severity");

            migrationBuilder.CreateIndex(
                name: "ix_audit_jobs_audit_id",
                table: "audit_jobs",
                column: "audit_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_audit_jobs_status_available_at_utc",
                table: "audit_jobs",
                columns: new[] { "status", "available_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_audits_project_id",
                table: "audits",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_audits_status",
                table: "audits",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_crawled_pages_audit_id",
                table: "crawled_pages",
                column: "audit_id");

            migrationBuilder.CreateIndex(
                name: "ix_crawled_pages_audit_id_normalized_url",
                table: "crawled_pages",
                columns: new[] { "audit_id", "normalized_url" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_projects_normalized_host",
                table: "projects",
                column: "normalized_host");

            migrationBuilder.CreateIndex(
                name: "ix_projects_user_id",
                table: "projects",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_findings");

            migrationBuilder.DropTable(
                name: "audit_jobs");

            migrationBuilder.DropTable(
                name: "crawled_pages");

            migrationBuilder.DropTable(
                name: "audits");

            migrationBuilder.DropTable(
                name: "projects");
        }
    }
}
