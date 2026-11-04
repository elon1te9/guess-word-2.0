using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GuessWord.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomMembers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "Rooms" WHERE "HostUserId" = "GuestUserId") THEN
                        RAISE EXCEPTION 'Ambiguous legacy room membership; resolve before migration';
                    END IF;
                    IF EXISTS (SELECT "GameId" FROM "Rooms" WHERE "GameId" IS NOT NULL GROUP BY "GameId" HAVING count(*) > 1) THEN
                        RAISE EXCEPTION 'Legacy match belongs to multiple rooms; resolve before migration';
                    END IF;
                END $$;
                """);

            migrationBuilder.AddColumn<int>(
                name: "RoomId",
                table: "Games",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RoomMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoomId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoomMembers_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RoomMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql("""
                INSERT INTO "RoomMembers" ("RoomId", "UserId", "JoinedAt")
                SELECT "Id", "HostUserId", "CreatedAt" FROM "Rooms" ORDER BY "Id";
                INSERT INTO "RoomMembers" ("RoomId", "UserId", "JoinedAt")
                SELECT "Id", "GuestUserId", "CreatedAt" FROM "Rooms" WHERE "GuestUserId" IS NOT NULL ORDER BY "Id";
                UPDATE "Games" g SET "RoomId" = r."Id" FROM "Rooms" r WHERE r."GameId" = g."Id";
                UPDATE "Rooms" SET "Status" = 0 WHERE "Status" = 1;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Rooms_Users_GuestUserId",
                table: "Rooms");

            migrationBuilder.DropIndex(
                name: "IX_Rooms_GuestUserId",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "GuestUserId",
                table: "Rooms");

            migrationBuilder.CreateIndex(
                name: "IX_Games_RoomId",
                table: "Games",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_RoomMembers_RoomId_UserId",
                table: "RoomMembers",
                columns: new[] { "RoomId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoomMembers_UserId",
                table: "RoomMembers",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Games_Rooms_RoomId",
                table: "Games",
                column: "RoomId",
                principalTable: "Rooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT "RoomId" FROM "RoomMembers" GROUP BY "RoomId" HAVING count(*) > 2)
                       OR EXISTS (SELECT 1 FROM "Games" g JOIN "Rooms" r ON g."RoomId" = r."Id" WHERE g."Id" IS DISTINCT FROM r."GameId") THEN
                        RAISE EXCEPTION 'Rollback would lose room history; restore a verified backup instead';
                    END IF;
                END $$;
                ALTER TABLE "Rooms" ADD COLUMN "GuestUserId" integer NULL;
                UPDATE "Rooms" r SET "GuestUserId" = m."UserId" FROM "RoomMembers" m
                    WHERE m."RoomId" = r."Id" AND m."UserId" <> r."HostUserId";
                UPDATE "Rooms" SET "Status" = 1 WHERE "Status" = 0 AND "GuestUserId" IS NOT NULL;
                """);
            migrationBuilder.DropForeignKey(name: "FK_Games_Rooms_RoomId", table: "Games");
            migrationBuilder.DropTable(name: "RoomMembers");
            migrationBuilder.DropIndex(name: "IX_Games_RoomId", table: "Games");
            migrationBuilder.DropColumn(name: "RoomId", table: "Games");
            migrationBuilder.CreateIndex(name: "IX_Rooms_GuestUserId", table: "Rooms", column: "GuestUserId");
            migrationBuilder.AddForeignKey(name: "FK_Rooms_Users_GuestUserId", table: "Rooms", column: "GuestUserId",
                principalTable: "Users", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        }
    }
}
