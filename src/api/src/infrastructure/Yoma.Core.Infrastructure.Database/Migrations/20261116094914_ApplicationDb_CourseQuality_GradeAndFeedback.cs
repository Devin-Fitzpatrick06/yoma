using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yoma.Core.Infrastructure.Database.Migrations
{
  /// <inheritdoc />
  public partial class ApplicationDb_CourseQuality_GradeAndFeedback : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.AddColumn<string>(
        name: "PageEvidence",
        schema: "CourseQuality",
        table: "Assessment",
        type: "text",
        nullable: true);

      migrationBuilder.AddColumn<string>(
        name: "ReviewReasons",
        schema: "CourseQuality",
        table: "Assessment",
        type: "text",
        nullable: true);

      migrationBuilder.CreateTable(
        name: "Grade",
        schema: "CourseQuality",
        columns: table => new
        {
          Id = table.Column<Guid>(type: "uuid", nullable: false),
          OpportunityId = table.Column<Guid>(type: "uuid", nullable: false),
          Status = table.Column<string>(type: "varchar(20)", nullable: false),
          Grade = table.Column<string>(type: "varchar(20)", nullable: false),
          GradeLabel = table.Column<string>(type: "varchar(50)", nullable: false),
          Score = table.Column<int>(type: "integer", nullable: false),
          Percentile = table.Column<int>(type: "integer", nullable: false),
          FeedbackResponses = table.Column<int>(type: "integer", nullable: false),
          Factors = table.Column<string>(type: "jsonb", nullable: false),
          Caps = table.Column<string>(type: "jsonb", nullable: false),
          RubricVersion = table.Column<string>(type: "varchar(50)", nullable: false),
          DateGraded = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
        },
        constraints: table =>
        {
          table.PrimaryKey("PK_Grade", x => x.Id);
          table.ForeignKey(
            name: "FK_Grade_Opportunity_OpportunityId",
            column: x => x.OpportunityId,
            principalSchema: "Opportunity",
            principalTable: "Opportunity",
            principalColumn: "Id");
        });

      migrationBuilder.CreateTable(
        name: "GradeRubric",
        schema: "CourseQuality",
        columns: table => new
        {
          Id = table.Column<Guid>(type: "uuid", nullable: false),
          Rubric = table.Column<string>(type: "text", nullable: true),
          UpdatedByEmail = table.Column<string>(type: "varchar(320)", nullable: false),
          DateCreated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
        },
        constraints: table =>
        {
          table.PrimaryKey("PK_GradeRubric", x => x.Id);
        });

      migrationBuilder.CreateTable(
        name: "LearnerFeedback",
        schema: "CourseQuality",
        columns: table => new
        {
          Id = table.Column<Guid>(type: "uuid", nullable: false),
          UserId = table.Column<Guid>(type: "uuid", nullable: false),
          OpportunityId = table.Column<Guid>(type: "uuid", nullable: false),
          Answer = table.Column<string>(type: "varchar(20)", nullable: false),
          DateLeft = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
          DateAsked = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
          SnoozeUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
          Rating = table.Column<int>(type: "integer", nullable: true),
          Completed = table.Column<bool>(type: "boolean", nullable: true),
          WouldRecommend = table.Column<bool>(type: "boolean", nullable: true),
          MatchedDescription = table.Column<bool>(type: "boolean", nullable: true),
          Good = table.Column<string>(type: "jsonb", nullable: false),
          Bad = table.Column<string>(type: "jsonb", nullable: false),
          Comment = table.Column<string>(type: "text", nullable: true),
          DateCreated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
          DateModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
        },
        constraints: table =>
        {
          table.PrimaryKey("PK_LearnerFeedback", x => x.Id);
          table.ForeignKey(
            name: "FK_LearnerFeedback_Opportunity_OpportunityId",
            column: x => x.OpportunityId,
            principalSchema: "Opportunity",
            principalTable: "Opportunity",
            principalColumn: "Id");
          table.ForeignKey(
            name: "FK_LearnerFeedback_User_UserId",
            column: x => x.UserId,
            principalSchema: "Entity",
            principalTable: "User",
            principalColumn: "Id");
        });

      migrationBuilder.CreateIndex(
        name: "IX_Grade_Grade",
        schema: "CourseQuality",
        table: "Grade",
        column: "Grade");

      migrationBuilder.CreateIndex(
        name: "IX_Grade_OpportunityId",
        schema: "CourseQuality",
        table: "Grade",
        column: "OpportunityId",
        unique: true);

      migrationBuilder.CreateIndex(
        name: "IX_GradeRubric_DateCreated",
        schema: "CourseQuality",
        table: "GradeRubric",
        column: "DateCreated");

      migrationBuilder.CreateIndex(
        name: "IX_LearnerFeedback_OpportunityId",
        schema: "CourseQuality",
        table: "LearnerFeedback",
        column: "OpportunityId");

      migrationBuilder.CreateIndex(
        name: "IX_LearnerFeedback_UserId_OpportunityId",
        schema: "CourseQuality",
        table: "LearnerFeedback",
        columns: new[] { "UserId", "OpportunityId" },
        unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropTable(
        name: "Grade",
        schema: "CourseQuality");

      migrationBuilder.DropTable(
        name: "GradeRubric",
        schema: "CourseQuality");

      migrationBuilder.DropTable(
        name: "LearnerFeedback",
        schema: "CourseQuality");

      migrationBuilder.DropColumn(
        name: "PageEvidence",
        schema: "CourseQuality",
        table: "Assessment");

      migrationBuilder.DropColumn(
        name: "ReviewReasons",
        schema: "CourseQuality",
        table: "Assessment");
    }
  }
}
