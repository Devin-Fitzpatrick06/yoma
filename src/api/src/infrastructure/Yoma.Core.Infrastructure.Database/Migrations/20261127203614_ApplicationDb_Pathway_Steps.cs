using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yoma.Core.Infrastructure.Database.Migrations
{
  /// <inheritdoc />
  public partial class ApplicationDb_Pathway_Steps : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.CreateTable(
        name: "StepEvent",
        schema: "Pathway",
        columns: table => new
        {
          Id = table.Column<Guid>(type: "uuid", nullable: false),
          UserId = table.Column<Guid>(type: "uuid", nullable: false),
          OpportunityId = table.Column<Guid>(type: "uuid", nullable: false),
          GoalKey = table.Column<string>(type: "varchar(50)", nullable: true),
          StageOrder = table.Column<int>(type: "integer", nullable: true),
          Event = table.Column<string>(type: "varchar(20)", nullable: false),
          DateCreated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
        },
        constraints: table =>
        {
          table.PrimaryKey("PK_StepEvent", x => x.Id);
          table.ForeignKey(
            name: "FK_StepEvent_Opportunity_OpportunityId",
            column: x => x.OpportunityId,
            principalSchema: "Opportunity",
            principalTable: "Opportunity",
            principalColumn: "Id");
          table.ForeignKey(
            name: "FK_StepEvent_User_UserId",
            column: x => x.UserId,
            principalSchema: "Entity",
            principalTable: "User",
            principalColumn: "Id");
        });

      migrationBuilder.CreateTable(
        name: "StepLock",
        schema: "Pathway",
        columns: table => new
        {
          Id = table.Column<Guid>(type: "uuid", nullable: false),
          UserId = table.Column<Guid>(type: "uuid", nullable: false),
          OpportunityId = table.Column<Guid>(type: "uuid", nullable: false),
          DateCreated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
        },
        constraints: table =>
        {
          table.PrimaryKey("PK_StepLock", x => x.Id);
          table.ForeignKey(
            name: "FK_StepLock_Opportunity_OpportunityId",
            column: x => x.OpportunityId,
            principalSchema: "Opportunity",
            principalTable: "Opportunity",
            principalColumn: "Id");
          table.ForeignKey(
            name: "FK_StepLock_User_UserId",
            column: x => x.UserId,
            principalSchema: "Entity",
            principalTable: "User",
            principalColumn: "Id");
        });

      migrationBuilder.CreateTable(
        name: "StepSwap",
        schema: "Pathway",
        columns: table => new
        {
          Id = table.Column<Guid>(type: "uuid", nullable: false),
          UserId = table.Column<Guid>(type: "uuid", nullable: false),
          ReplacedOpportunityId = table.Column<Guid>(type: "uuid", nullable: false),
          ReplacementOpportunityId = table.Column<Guid>(type: "uuid", nullable: false),
          DateCreated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
        },
        constraints: table =>
        {
          table.PrimaryKey("PK_StepSwap", x => x.Id);
          table.ForeignKey(
            name: "FK_StepSwap_Opportunity_ReplacedOpportunityId",
            column: x => x.ReplacedOpportunityId,
            principalSchema: "Opportunity",
            principalTable: "Opportunity",
            principalColumn: "Id");
          table.ForeignKey(
            name: "FK_StepSwap_Opportunity_ReplacementOpportunityId",
            column: x => x.ReplacementOpportunityId,
            principalSchema: "Opportunity",
            principalTable: "Opportunity",
            principalColumn: "Id");
          table.ForeignKey(
            name: "FK_StepSwap_User_UserId",
            column: x => x.UserId,
            principalSchema: "Entity",
            principalTable: "User",
            principalColumn: "Id");
        });

      migrationBuilder.CreateIndex(
        name: "IX_StepEvent_Event_DateCreated",
        schema: "Pathway",
        table: "StepEvent",
        columns: new[] { "Event", "DateCreated" });

      migrationBuilder.CreateIndex(
        name: "IX_StepEvent_GoalKey_StageOrder_Event",
        schema: "Pathway",
        table: "StepEvent",
        columns: new[] { "GoalKey", "StageOrder", "Event" });

      migrationBuilder.CreateIndex(
        name: "IX_StepEvent_OpportunityId_Event",
        schema: "Pathway",
        table: "StepEvent",
        columns: new[] { "OpportunityId", "Event" });

      migrationBuilder.CreateIndex(
        name: "IX_StepEvent_UserId_OpportunityId_Event_DateCreated",
        schema: "Pathway",
        table: "StepEvent",
        columns: new[] { "UserId", "OpportunityId", "Event", "DateCreated" });

      migrationBuilder.CreateIndex(
        name: "IX_StepLock_OpportunityId",
        schema: "Pathway",
        table: "StepLock",
        column: "OpportunityId");

      migrationBuilder.CreateIndex(
        name: "IX_StepLock_UserId_OpportunityId",
        schema: "Pathway",
        table: "StepLock",
        columns: new[] { "UserId", "OpportunityId" },
        unique: true);

      migrationBuilder.CreateIndex(
        name: "IX_StepSwap_ReplacedOpportunityId",
        schema: "Pathway",
        table: "StepSwap",
        column: "ReplacedOpportunityId");

      migrationBuilder.CreateIndex(
        name: "IX_StepSwap_ReplacementOpportunityId",
        schema: "Pathway",
        table: "StepSwap",
        column: "ReplacementOpportunityId");

      migrationBuilder.CreateIndex(
        name: "IX_StepSwap_UserId_DateCreated",
        schema: "Pathway",
        table: "StepSwap",
        columns: new[] { "UserId", "DateCreated" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropTable(
        name: "StepEvent",
        schema: "Pathway");

      migrationBuilder.DropTable(
        name: "StepLock",
        schema: "Pathway");

      migrationBuilder.DropTable(
        name: "StepSwap",
        schema: "Pathway");
    }
  }
}
