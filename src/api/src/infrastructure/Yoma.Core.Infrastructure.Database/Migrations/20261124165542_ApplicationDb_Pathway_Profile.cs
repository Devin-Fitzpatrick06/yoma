using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yoma.Core.Infrastructure.Database.Migrations
{
  /// <inheritdoc />
  public partial class ApplicationDb_Pathway_Profile : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.EnsureSchema(
          name: "Pathway");

      migrationBuilder.CreateTable(
          name: "Profile",
          schema: "Pathway",
          columns: table => new
          {
            Id = table.Column<Guid>(type: "uuid", nullable: false),
            UserId = table.Column<Guid>(type: "uuid", nullable: false),
            GoalKey = table.Column<string>(type: "varchar(50)", nullable: false),
            Background = table.Column<string>(type: "varchar(20)", nullable: false),
            KnownSkillKeys = table.Column<string>(type: "jsonb", nullable: false),
            HoursPerWeek = table.Column<int>(type: "integer", nullable: false),
            CountryId = table.Column<Guid>(type: "uuid", nullable: true),
            DateCreated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            DateModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_Profile", x => x.Id);
            table.ForeignKey(
                      name: "FK_Profile_Country_CountryId",
                      column: x => x.CountryId,
                      principalSchema: "Lookup",
                      principalTable: "Country",
                      principalColumn: "Id");
            table.ForeignKey(
                      name: "FK_Profile_User_UserId",
                      column: x => x.UserId,
                      principalSchema: "Entity",
                      principalTable: "User",
                      principalColumn: "Id");
          });

      migrationBuilder.CreateIndex(
          name: "IX_Profile_CountryId",
          schema: "Pathway",
          table: "Profile",
          column: "CountryId");

      migrationBuilder.CreateIndex(
          name: "IX_Profile_GoalKey",
          schema: "Pathway",
          table: "Profile",
          column: "GoalKey");

      migrationBuilder.CreateIndex(
          name: "IX_Profile_UserId",
          schema: "Pathway",
          table: "Profile",
          column: "UserId",
          unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropTable(
          name: "Profile",
          schema: "Pathway");
    }
  }
}
