using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yoma.Core.Infrastructure.Database.Migrations
{
  /// <inheritdoc />
  public partial class ApplicationDb_CourseQuality_Assessment : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.EnsureSchema(
        name: "CourseQuality");

      migrationBuilder.CreateTable(
        name: "Assessment",
        schema: "CourseQuality",
        columns: table => new
        {
          Id = table.Column<Guid>(type: "uuid", nullable: false),
          OpportunityId = table.Column<Guid>(type: "uuid", nullable: false),
          OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
          RubricVersion = table.Column<string>(type: "varchar(50)", nullable: false),
          Score = table.Column<int>(type: "integer", nullable: false),
          Confidence = table.Column<decimal>(type: "numeric(4,3)", nullable: false),
          Status = table.Column<string>(type: "varchar(20)", nullable: false),
          ReviewStatus = table.Column<string>(type: "varchar(20)", nullable: false),
          Format = table.Column<string>(type: "varchar(20)", nullable: false),
          DatesSuspect = table.Column<bool>(type: "boolean", nullable: false),
          CardReady = table.Column<bool>(type: "boolean", nullable: false),
          AIUsed = table.Column<bool>(type: "boolean", nullable: false),
          AIModel = table.Column<string>(type: "varchar(100)", nullable: true),
          AISummary = table.Column<string>(type: "text", nullable: true),
          Criteria = table.Column<string>(type: "jsonb", nullable: false),
          LearnerCard = table.Column<string>(type: "jsonb", nullable: false),
          FixNote = table.Column<string>(type: "text", nullable: true),
          ReviewedByEmail = table.Column<string>(type: "varchar(320)", nullable: true),
          ReviewNote = table.Column<string>(type: "varchar(1000)", nullable: true),
          DateReviewed = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
          DateAssessed = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
          OpportunityDateModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
          ContentFingerprint = table.Column<string>(type: "varchar(64)", nullable: false),
          FinalHost = table.Column<string>(type: "varchar(255)", nullable: true),
          DateConfirmed = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
          IsLatest = table.Column<bool>(type: "boolean", nullable: false)
        },
        constraints: table =>
        {
          table.PrimaryKey("PK_Assessment", x => x.Id);
          table.ForeignKey(
            name: "FK_Assessment_Opportunity_OpportunityId",
            column: x => x.OpportunityId,
            principalSchema: "Opportunity",
            principalTable: "Opportunity",
            principalColumn: "Id");
          table.ForeignKey(
            name: "FK_Assessment_Organization_OrganizationId",
            column: x => x.OrganizationId,
            principalSchema: "Entity",
            principalTable: "Organization",
            principalColumn: "Id");
        });

      migrationBuilder.CreateIndex(
        name: "IX_Assessment_OpportunityId",
        schema: "CourseQuality",
        table: "Assessment",
        column: "OpportunityId",
        unique: true,
        filter: "\"IsLatest\" = true");

      migrationBuilder.CreateIndex(
        name: "IX_Assessment_OrganizationId_IsLatest_ReviewStatus",
        schema: "CourseQuality",
        table: "Assessment",
        columns: new[] { "OrganizationId", "IsLatest", "ReviewStatus" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropTable(
        name: "Assessment",
        schema: "CourseQuality");
    }
  }
}
