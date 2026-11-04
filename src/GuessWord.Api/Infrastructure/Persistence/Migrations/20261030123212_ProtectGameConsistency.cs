using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GuessWord.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProtectGameConsistency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Остановиться при конфликтах, сохранив исходные данные целиком.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (
                        SELECT p."UserId" FROM "GamePlayers" p JOIN "Games" g ON g."Id" = p."GameId"
                        WHERE g."Status" = 0 AND p."Result" = 0
                        GROUP BY p."UserId" HAVING count(*) > 1
                    ) OR EXISTS (
                        SELECT 1 FROM "GameAttempts" WHERE "IsValid"
                        GROUP BY "GameId", "UserId", replace(lower(btrim("Word")), 'ё', 'е') HAVING count(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Game consistency conflicts: inspect a restored database before migration';
                    END IF;
                END $$;
                UPDATE "GameAttempts" SET "Word" = replace(lower(btrim("Word")), 'ё', 'е') WHERE "IsValid";
                """);

            migrationBuilder.DropIndex(
                name: "IX_GamePlayers_GameId",
                table: "GamePlayers");

            migrationBuilder.DropIndex(
                name: "IX_GameAttempts_GameId",
                table: "GameAttempts");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_GamePlayers_GameId_UserId",
                table: "GamePlayers",
                columns: new[] { "GameId", "UserId" });

            migrationBuilder.CreateTable(
                name: "ActiveGameParticipations",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    GameId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActiveGameParticipations", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_ActiveGameParticipations_GamePlayers_GameId_UserId",
                        columns: x => new { x.GameId, x.UserId },
                        principalTable: "GamePlayers",
                        principalColumns: new[] { "GameId", "UserId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameAttempts_GameId_UserId_Word",
                table: "GameAttempts",
                columns: new[] { "GameId", "UserId", "Word" },
                unique: true,
                filter: "\"IsValid\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_ActiveGameParticipations_GameId_UserId",
                table: "ActiveGameParticipations",
                columns: new[] { "GameId", "UserId" });

            migrationBuilder.Sql("""
                INSERT INTO "ActiveGameParticipations" ("UserId", "GameId")
                SELECT p."UserId", p."GameId" FROM "GamePlayers" p JOIN "Games" g ON g."Id" = p."GameId"
                WHERE g."Status" = 0 AND p."Result" = 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActiveGameParticipations");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_GamePlayers_GameId_UserId",
                table: "GamePlayers");

            migrationBuilder.DropIndex(
                name: "IX_GameAttempts_GameId_UserId_Word",
                table: "GameAttempts");

            migrationBuilder.CreateIndex(
                name: "IX_GamePlayers_GameId",
                table: "GamePlayers",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_GameAttempts_GameId",
                table: "GameAttempts",
                column: "GameId");
        }
    }
}
