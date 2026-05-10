using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Resume.Api.Migrations;

/// <inheritdoc />
public partial class FixDateTimeOffsetConverters : Migration
{
    private const long TicksUnixEpoch = 621355968000000000L;

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (migrationBuilder.ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            UpPostgres(migrationBuilder);
        else
            UpSqlite(migrationBuilder);
    }

    private static void UpPostgres(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            $"""
            ALTER TABLE "AuditLogs"
              ALTER COLUMN "CreatedAt" TYPE bigint
              USING ({TicksUnixEpoch} + floor(extract(epoch from "CreatedAt") * 10000000)::bigint);
            """);

        migrationBuilder.Sql(
            $"""
            ALTER TABLE "Candidates"
              ALTER COLUMN "UploadedAt" TYPE bigint
              USING ({TicksUnixEpoch} + floor(extract(epoch from "UploadedAt") * 10000000)::bigint);
            """);

        migrationBuilder.Sql(
            $"""
            ALTER TABLE "CandidateScores"
              ALTER COLUMN "ScoredAt" TYPE bigint
              USING ({TicksUnixEpoch} + floor(extract(epoch from "ScoredAt") * 10000000)::bigint);
            """);

        migrationBuilder.Sql(
            $"""
            ALTER TABLE "Feedbacks"
              ALTER COLUMN "CreatedAt" TYPE bigint
              USING ({TicksUnixEpoch} + floor(extract(epoch from "CreatedAt") * 10000000)::bigint);
            """);

        migrationBuilder.Sql(
            $"""
            ALTER TABLE "Jobs"
              ALTER COLUMN "CreatedAt" TYPE bigint
              USING ({TicksUnixEpoch} + floor(extract(epoch from "CreatedAt") * 10000000)::bigint);
            """);

        migrationBuilder.Sql(
            $"""
            ALTER TABLE "RefreshTokens"
              ALTER COLUMN "CreatedAt" TYPE bigint
              USING ({TicksUnixEpoch} + floor(extract(epoch from "CreatedAt") * 10000000)::bigint),
              ALTER COLUMN "ExpiresAt" TYPE bigint
              USING ({TicksUnixEpoch} + floor(extract(epoch from "ExpiresAt") * 10000000)::bigint),
              ALTER COLUMN "RevokedAt" TYPE bigint
              USING (
                CASE WHEN "RevokedAt" IS NULL THEN NULL
                ELSE {TicksUnixEpoch} + floor(extract(epoch from "RevokedAt") * 10000000)::bigint END);
            """);
    }

    /// <summary>SQLite cannot alter DateTimeOffset columns in place; rebuild tables with INTEGER PKs (not bigint+AUTOINCREMENT).</summary>
    private static void UpSqlite(MigrationBuilder migrationBuilder)
    {
        var ticksFromCol = (string col) =>
            $"{TicksUnixEpoch} + CAST(ROUND((julianday([{col}]) - 2440587.5) * 864000000000.0) AS INTEGER)";
        var ticksCreated = ticksFromCol("CreatedAt");
        var ticksUploaded = ticksFromCol("UploadedAt");
        var ticksScored = ticksFromCol("ScoredAt");
        var ticksExpires = ticksFromCol("ExpiresAt");
        var ticksRevokedNullable =
            $"CASE WHEN [RevokedAt] IS NULL THEN NULL ELSE {ticksFromCol("RevokedAt")} END";

        migrationBuilder.Sql("PRAGMA foreign_keys = 0;");

        migrationBuilder.Sql(
            $"""
            CREATE TABLE "ef_new_CandidateScores" (
                "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                "CandidateId" INTEGER NOT NULL,
                "JobId" INTEGER NOT NULL,
                "Score" INTEGER NOT NULL,
                "Rank" INTEGER NOT NULL,
                "MatchedKeywords" TEXT NOT NULL,
                "MissingKeywords" TEXT NOT NULL,
                "TotalJobKeywords" INTEGER NOT NULL,
                "CoreMatchedKeywords" TEXT NOT NULL,
                "CoreMissingKeywords" TEXT NOT NULL,
                "SecondaryMatchedKeywords" TEXT NOT NULL,
                "SecondaryMissingKeywords" TEXT NOT NULL,
                "HardFilters" TEXT NOT NULL,
                "ScoreReasons" TEXT NOT NULL,
                "TotalCoreKeywords" INTEGER NOT NULL,
                "TotalSecondaryKeywords" INTEGER NOT NULL,
                "ScoredAt" INTEGER NOT NULL,
                CONSTRAINT "FK_CandidateScores_Candidates_CandidateId" FOREIGN KEY ("CandidateId") REFERENCES "Candidates" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_CandidateScores_Jobs_JobId" FOREIGN KEY ("JobId") REFERENCES "Jobs" ("Id") ON DELETE CASCADE
            );
            INSERT INTO "ef_new_CandidateScores" SELECT "Id", "CandidateId", "JobId", "Score", "Rank", "MatchedKeywords", "MissingKeywords", "TotalJobKeywords", "CoreMatchedKeywords", "CoreMissingKeywords", "SecondaryMatchedKeywords", "SecondaryMissingKeywords", "HardFilters", "ScoreReasons", "TotalCoreKeywords", "TotalSecondaryKeywords", {ticksScored} FROM "CandidateScores";
            DROP TABLE "CandidateScores";
            ALTER TABLE "ef_new_CandidateScores" RENAME TO "CandidateScores";
            CREATE INDEX "IX_CandidateScores_CandidateId" ON "CandidateScores" ("CandidateId");
            CREATE UNIQUE INDEX "IX_CandidateScores_JobId_CandidateId" ON "CandidateScores" ("JobId", "CandidateId");
            CREATE INDEX "IX_CandidateScores_JobId_Rank" ON "CandidateScores" ("JobId", "Rank");
            """);

        migrationBuilder.Sql(
            $"""
            CREATE TABLE "ef_new_Feedbacks" (
                "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                "CandidateId" INTEGER NOT NULL,
                "JobId" INTEGER NOT NULL,
                "Type" INTEGER NOT NULL,
                "CreatedAt" INTEGER NOT NULL,
                CONSTRAINT "FK_Feedbacks_Candidates_CandidateId" FOREIGN KEY ("CandidateId") REFERENCES "Candidates" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_Feedbacks_Jobs_JobId" FOREIGN KEY ("JobId") REFERENCES "Jobs" ("Id") ON DELETE CASCADE
            );
            INSERT INTO "ef_new_Feedbacks" SELECT "Id", "CandidateId", "JobId", "Type", {ticksCreated} FROM "Feedbacks";
            DROP TABLE "Feedbacks";
            ALTER TABLE "ef_new_Feedbacks" RENAME TO "Feedbacks";
            CREATE INDEX "IX_Feedbacks_JobId" ON "Feedbacks" ("JobId");
            CREATE UNIQUE INDEX "IX_Feedbacks_CandidateId_JobId" ON "Feedbacks" ("CandidateId", "JobId");
            """);

        migrationBuilder.Sql(
            $"""
            CREATE TABLE "ef_new_Candidates" (
                "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                "FileName" TEXT NOT NULL,
                "ParsedText" TEXT NOT NULL,
                "Name" TEXT NOT NULL,
                "Email" TEXT NOT NULL,
                "ExtractedSkills" TEXT NOT NULL,
                "UploadedAt" INTEGER NOT NULL
            );
            INSERT INTO "ef_new_Candidates" SELECT "Id", "FileName", "ParsedText", "Name", "Email", "ExtractedSkills", {ticksUploaded} FROM "Candidates";
            DROP TABLE "Candidates";
            ALTER TABLE "ef_new_Candidates" RENAME TO "Candidates";
            """);

        migrationBuilder.Sql(
            $"""
            CREATE TABLE "ef_new_Jobs" (
                "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                "UserId" TEXT NOT NULL,
                "Title" TEXT NOT NULL,
                "Description" TEXT NOT NULL,
                "CreatedAt" INTEGER NOT NULL,
                CONSTRAINT "FK_Jobs_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
            );
            INSERT INTO "ef_new_Jobs" SELECT "Id", "UserId", "Title", "Description", {ticksCreated} FROM "Jobs";
            DROP TABLE "Jobs";
            ALTER TABLE "ef_new_Jobs" RENAME TO "Jobs";
            CREATE INDEX "IX_Jobs_UserId" ON "Jobs" ("UserId");
            """);

        migrationBuilder.Sql(
            $"""
            CREATE TABLE "ef_new_AuditLogs" (
                "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                "Action" TEXT NOT NULL,
                "ResourceType" TEXT NOT NULL,
                "ResourceId" TEXT NOT NULL,
                "UserId" TEXT NULL,
                "Metadata" TEXT NULL,
                "CreatedAt" INTEGER NOT NULL
            );
            INSERT INTO "ef_new_AuditLogs" SELECT "Id", "Action", "ResourceType", "ResourceId", "UserId", "Metadata", {ticksCreated} FROM "AuditLogs";
            DROP TABLE "AuditLogs";
            ALTER TABLE "ef_new_AuditLogs" RENAME TO "AuditLogs";
            """);

        migrationBuilder.Sql(
            $"""
            CREATE TABLE "ef_new_RefreshTokens" (
                "Id" TEXT NOT NULL PRIMARY KEY,
                "UserId" TEXT NOT NULL,
                "TokenHash" TEXT NOT NULL,
                "CreatedAt" INTEGER NOT NULL,
                "ExpiresAt" INTEGER NOT NULL,
                "RevokedAt" INTEGER NULL,
                "ReplacedByTokenHash" TEXT NULL,
                CONSTRAINT "FK_RefreshTokens_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
            );
            INSERT INTO "ef_new_RefreshTokens" SELECT "Id", "UserId", "TokenHash", {ticksCreated}, {ticksExpires}, {ticksRevokedNullable}, "ReplacedByTokenHash" FROM "RefreshTokens";
            DROP TABLE "RefreshTokens";
            ALTER TABLE "ef_new_RefreshTokens" RENAME TO "RefreshTokens";
            CREATE UNIQUE INDEX "IX_RefreshTokens_TokenHash" ON "RefreshTokens" ("TokenHash");
            CREATE INDEX "IX_RefreshTokens_UserId" ON "RefreshTokens" ("UserId");
            """);

        migrationBuilder.Sql("PRAGMA foreign_keys = 1;");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        if (migrationBuilder.ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            DownPostgres(migrationBuilder);
        else
            DownSqlite(migrationBuilder);
    }

    private static void DownPostgres(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE "AuditLogs"
              ALTER COLUMN "CreatedAt" TYPE timestamp with time zone
              USING to_timestamp(("CreatedAt" - 621355968000000000) / 10000000.0) AT TIME ZONE 'UTC';
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE "Candidates"
              ALTER COLUMN "UploadedAt" TYPE timestamp with time zone
              USING to_timestamp(("UploadedAt" - 621355968000000000) / 10000000.0) AT TIME ZONE 'UTC';
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE "CandidateScores"
              ALTER COLUMN "ScoredAt" TYPE timestamp with time zone
              USING to_timestamp(("ScoredAt" - 621355968000000000) / 10000000.0) AT TIME ZONE 'UTC';
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE "Feedbacks"
              ALTER COLUMN "CreatedAt" TYPE timestamp with time zone
              USING to_timestamp(("CreatedAt" - 621355968000000000) / 10000000.0) AT TIME ZONE 'UTC';
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE "Jobs"
              ALTER COLUMN "CreatedAt" TYPE timestamp with time zone
              USING to_timestamp(("CreatedAt" - 621355968000000000) / 10000000.0) AT TIME ZONE 'UTC';
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE "RefreshTokens"
              ALTER COLUMN "CreatedAt" TYPE timestamp with time zone
              USING to_timestamp(("CreatedAt" - 621355968000000000) / 10000000.0) AT TIME ZONE 'UTC',
              ALTER COLUMN "ExpiresAt" TYPE timestamp with time zone
              USING to_timestamp(("ExpiresAt" - 621355968000000000) / 10000000.0) AT TIME ZONE 'UTC',
              ALTER COLUMN "RevokedAt" TYPE timestamp with time zone
              USING CASE WHEN "RevokedAt" IS NULL THEN NULL
                ELSE to_timestamp(("RevokedAt" - 621355968000000000) / 10000000.0) AT TIME ZONE 'UTC' END;
            """);
    }

    private static void DownSqlite(MigrationBuilder migrationBuilder)
    {
        var isoFromCol = (string col) =>
            $"strftime('%Y-%m-%dT%H:%M:%f', datetime(([{col}] - 621355968000000000) / 10000000.0, 'unixepoch')) || '+00:00'";
        var isoCreated = isoFromCol("CreatedAt");
        var isoUploaded = isoFromCol("UploadedAt");
        var isoScored = isoFromCol("ScoredAt");
        var isoExpires = isoFromCol("ExpiresAt");
        var isoRevokedNullable =
            $"CASE WHEN [RevokedAt] IS NULL THEN NULL ELSE {isoFromCol("RevokedAt")} END";

        migrationBuilder.Sql("PRAGMA foreign_keys = 0;");

        migrationBuilder.Sql(
            $"""
            CREATE TABLE "ef_new_CandidateScores" (
                "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                "CandidateId" INTEGER NOT NULL,
                "JobId" INTEGER NOT NULL,
                "Score" INTEGER NOT NULL,
                "Rank" INTEGER NOT NULL,
                "MatchedKeywords" TEXT NOT NULL,
                "MissingKeywords" TEXT NOT NULL,
                "TotalJobKeywords" INTEGER NOT NULL,
                "CoreMatchedKeywords" TEXT NOT NULL,
                "CoreMissingKeywords" TEXT NOT NULL,
                "SecondaryMatchedKeywords" TEXT NOT NULL,
                "SecondaryMissingKeywords" TEXT NOT NULL,
                "HardFilters" TEXT NOT NULL,
                "ScoreReasons" TEXT NOT NULL,
                "TotalCoreKeywords" INTEGER NOT NULL,
                "TotalSecondaryKeywords" INTEGER NOT NULL,
                "ScoredAt" TEXT NOT NULL,
                CONSTRAINT "FK_CandidateScores_Candidates_CandidateId" FOREIGN KEY ("CandidateId") REFERENCES "Candidates" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_CandidateScores_Jobs_JobId" FOREIGN KEY ("JobId") REFERENCES "Jobs" ("Id") ON DELETE CASCADE
            );
            INSERT INTO "ef_new_CandidateScores" SELECT "Id", "CandidateId", "JobId", "Score", "Rank", "MatchedKeywords", "MissingKeywords", "TotalJobKeywords", "CoreMatchedKeywords", "CoreMissingKeywords", "SecondaryMatchedKeywords", "SecondaryMissingKeywords", "HardFilters", "ScoreReasons", "TotalCoreKeywords", "TotalSecondaryKeywords", {isoScored} FROM "CandidateScores";
            DROP TABLE "CandidateScores";
            ALTER TABLE "ef_new_CandidateScores" RENAME TO "CandidateScores";
            CREATE INDEX "IX_CandidateScores_CandidateId" ON "CandidateScores" ("CandidateId");
            CREATE UNIQUE INDEX "IX_CandidateScores_JobId_CandidateId" ON "CandidateScores" ("JobId", "CandidateId");
            CREATE INDEX "IX_CandidateScores_JobId_Rank" ON "CandidateScores" ("JobId", "Rank");
            """);

        migrationBuilder.Sql(
            $"""
            CREATE TABLE "ef_new_Feedbacks" (
                "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                "CandidateId" INTEGER NOT NULL,
                "JobId" INTEGER NOT NULL,
                "Type" INTEGER NOT NULL,
                "CreatedAt" TEXT NOT NULL,
                CONSTRAINT "FK_Feedbacks_Candidates_CandidateId" FOREIGN KEY ("CandidateId") REFERENCES "Candidates" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_Feedbacks_Jobs_JobId" FOREIGN KEY ("JobId") REFERENCES "Jobs" ("Id") ON DELETE CASCADE
            );
            INSERT INTO "ef_new_Feedbacks" SELECT "Id", "CandidateId", "JobId", "Type", {isoCreated} FROM "Feedbacks";
            DROP TABLE "Feedbacks";
            ALTER TABLE "ef_new_Feedbacks" RENAME TO "Feedbacks";
            CREATE INDEX "IX_Feedbacks_JobId" ON "Feedbacks" ("JobId");
            CREATE UNIQUE INDEX "IX_Feedbacks_CandidateId_JobId" ON "Feedbacks" ("CandidateId", "JobId");
            """);

        migrationBuilder.Sql(
            $"""
            CREATE TABLE "ef_new_Candidates" (
                "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                "FileName" TEXT NOT NULL,
                "ParsedText" TEXT NOT NULL,
                "Name" TEXT NOT NULL,
                "Email" TEXT NOT NULL,
                "ExtractedSkills" TEXT NOT NULL,
                "UploadedAt" TEXT NOT NULL
            );
            INSERT INTO "ef_new_Candidates" SELECT "Id", "FileName", "ParsedText", "Name", "Email", "ExtractedSkills", {isoUploaded} FROM "Candidates";
            DROP TABLE "Candidates";
            ALTER TABLE "ef_new_Candidates" RENAME TO "Candidates";
            """);

        migrationBuilder.Sql(
            $"""
            CREATE TABLE "ef_new_Jobs" (
                "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                "UserId" TEXT NOT NULL,
                "Title" TEXT NOT NULL,
                "Description" TEXT NOT NULL,
                "CreatedAt" TEXT NOT NULL,
                CONSTRAINT "FK_Jobs_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
            );
            INSERT INTO "ef_new_Jobs" SELECT "Id", "UserId", "Title", "Description", {isoCreated} FROM "Jobs";
            DROP TABLE "Jobs";
            ALTER TABLE "ef_new_Jobs" RENAME TO "Jobs";
            CREATE INDEX "IX_Jobs_UserId" ON "Jobs" ("UserId");
            """);

        migrationBuilder.Sql(
            $"""
            CREATE TABLE "ef_new_AuditLogs" (
                "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                "Action" TEXT NOT NULL,
                "ResourceType" TEXT NOT NULL,
                "ResourceId" TEXT NOT NULL,
                "UserId" TEXT NULL,
                "Metadata" TEXT NULL,
                "CreatedAt" TEXT NOT NULL
            );
            INSERT INTO "ef_new_AuditLogs" SELECT "Id", "Action", "ResourceType", "ResourceId", "UserId", "Metadata", {isoCreated} FROM "AuditLogs";
            DROP TABLE "AuditLogs";
            ALTER TABLE "ef_new_AuditLogs" RENAME TO "AuditLogs";
            """);

        migrationBuilder.Sql(
            $"""
            CREATE TABLE "ef_new_RefreshTokens" (
                "Id" TEXT NOT NULL PRIMARY KEY,
                "UserId" TEXT NOT NULL,
                "TokenHash" TEXT NOT NULL,
                "CreatedAt" TEXT NOT NULL,
                "ExpiresAt" TEXT NOT NULL,
                "RevokedAt" TEXT NULL,
                "ReplacedByTokenHash" TEXT NULL,
                CONSTRAINT "FK_RefreshTokens_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
            );
            INSERT INTO "ef_new_RefreshTokens" SELECT "Id", "UserId", "TokenHash", {isoCreated}, {isoExpires}, {isoRevokedNullable}, "ReplacedByTokenHash" FROM "RefreshTokens";
            DROP TABLE "RefreshTokens";
            ALTER TABLE "ef_new_RefreshTokens" RENAME TO "RefreshTokens";
            CREATE UNIQUE INDEX "IX_RefreshTokens_TokenHash" ON "RefreshTokens" ("TokenHash");
            CREATE INDEX "IX_RefreshTokens_UserId" ON "RefreshTokens" ("UserId");
            """);

        migrationBuilder.Sql("PRAGMA foreign_keys = 1;");
    }
}
