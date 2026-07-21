using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RoyalIdentity.UserAccounts.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PropertyScopes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RealmId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ActiveVersionId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyScopes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserAccounts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RealmId = table.Column<string>(type: "text", nullable: false),
                    SubjectId = table.Column<string>(type: "text", nullable: false),
                    Username = table.Column<string>(type: "text", nullable: false),
                    NormalizedUsername = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsBlocked = table.Column<bool>(type: "boolean", nullable: false),
                    BlockedReason = table.Column<string>(type: "text", nullable: true),
                    BlockedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    BlockStartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    BlockEndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExternalId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SecurityStamp = table.Column<string>(type: "text", nullable: false),
                    SessionsValidAfter = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                    // "xmin" is deliberately NOT listed here: it is PostgreSQL's built-in system column (present on
                    // every table automatically), used as the optimistic-concurrency token (see
                    // UserAccountsPostgreSqlModelBuilderExtensions.ApplyUserAccountsPostgreSqlMappings). EF's
                    // scaffolder does not know "xmin" is a reserved system column and would emit a CREATE TABLE
                    // column for it (PostgreSQL rejects that: "column name \"xmin\" conflicts with a system column
                    // name"). This line was manually removed after `dotnet ef migrations add`; redo this edit for
                    // any future migration that alters the UserAccounts table (dotnet ef will re-add the line).
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PropertyDefinitions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RealmId = table.Column<string>(type: "text", nullable: false),
                    PropertyScopeId = table.Column<long>(type: "bigint", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropertyDefinitions_PropertyScopes_PropertyScopeId",
                        column: x => x.PropertyScopeId,
                        principalTable: "PropertyScopes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PropertyScopeVersions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RealmId = table.Column<string>(type: "text", nullable: false),
                    PropertyScopeId = table.Column<long>(type: "bigint", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyScopeVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropertyScopeVersions_PropertyScopes_PropertyScopeId",
                        column: x => x.PropertyScopeId,
                        principalTable: "PropertyScopes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserAccountActionTokens",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RealmId = table.Column<string>(type: "text", nullable: false),
                    UserAccountId = table.Column<long>(type: "bigint", nullable: false),
                    Purpose = table.Column<string>(type: "text", nullable: false),
                    TokenHash = table.Column<string>(type: "text", nullable: false),
                    TargetValue = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    ExpiresAt = table.Column<long>(type: "bigint", nullable: false),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedReason = table.Column<string>(type: "text", nullable: true),
                    CreatedIpHash = table.Column<string>(type: "text", nullable: true),
                    ConsumedIpHash = table.Column<string>(type: "text", nullable: true),
                    UserAgentHash = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAccountActionTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserAccountActionTokens_UserAccounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserAccountCredentials",
                columns: table => new
                {
                    UserAccountId = table.Column<long>(type: "bigint", nullable: false),
                    RealmId = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    PasswordChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    MustChangePassword = table.Column<bool>(type: "boolean", nullable: false),
                    FailedPasswordAttempts = table.Column<int>(type: "integer", nullable: false),
                    LastPasswordFailureAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEndAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAccountCredentials", x => x.UserAccountId);
                    table.ForeignKey(
                        name: "FK_UserAccountCredentials_UserAccounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserAccountEmails",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RealmId = table.Column<string>(type: "text", nullable: false),
                    UserAccountId = table.Column<long>(type: "bigint", nullable: false),
                    Address = table.Column<string>(type: "text", nullable: false),
                    NormalizedAddress = table.Column<string>(type: "text", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    IsFictitious = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAccountEmails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserAccountEmails_UserAccounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserAccountPasswordHistory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RealmId = table.Column<string>(type: "text", nullable: false),
                    UserAccountId = table.Column<long>(type: "bigint", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    CreatedBySubjectId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAccountPasswordHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserAccountPasswordHistory_UserAccounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserAccountPhones",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RealmId = table.Column<string>(type: "text", nullable: false),
                    UserAccountId = table.Column<long>(type: "bigint", nullable: false),
                    Number = table.Column<string>(type: "text", nullable: false),
                    NormalizedNumber = table.Column<string>(type: "text", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAccountPhones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserAccountPhones_UserAccounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserAccountRoles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RealmId = table.Column<string>(type: "text", nullable: false),
                    UserAccountId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    NormalizedName = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAccountRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserAccountRoles_UserAccounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserAccountPropertyValues",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RealmId = table.Column<string>(type: "text", nullable: false),
                    UserAccountId = table.Column<long>(type: "bigint", nullable: false),
                    PropertyDefinitionId = table.Column<long>(type: "bigint", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    ValueType = table.Column<string>(type: "text", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAccountPropertyValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserAccountPropertyValues_PropertyDefinitions_PropertyDefin~",
                        column: x => x.PropertyDefinitionId,
                        principalTable: "PropertyDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserAccountPropertyValues_UserAccounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PropertyDefinitionVersions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RealmId = table.Column<string>(type: "text", nullable: false),
                    PropertyScopeVersionId = table.Column<long>(type: "bigint", nullable: false),
                    PropertyDefinitionId = table.Column<long>(type: "bigint", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: false),
                    ValueType = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: true),
                    Help = table.Column<string>(type: "text", nullable: true),
                    IsSensitive = table.Column<bool>(type: "boolean", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    IsCollection = table.Column<bool>(type: "boolean", nullable: false),
                    ValidationRules = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyDefinitionVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropertyDefinitionVersions_PropertyDefinitions_PropertyDefi~",
                        column: x => x.PropertyDefinitionId,
                        principalTable: "PropertyDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PropertyDefinitionVersions_PropertyScopeVersions_PropertySc~",
                        column: x => x.PropertyScopeVersionId,
                        principalTable: "PropertyScopeVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PropertyDefinitions_PropertyScopeId",
                table: "PropertyDefinitions",
                column: "PropertyScopeId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyDefinitions_RealmId_ClaimType",
                table: "PropertyDefinitions",
                columns: new[] { "RealmId", "ClaimType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PropertyDefinitions_RealmId_PropertyScopeId",
                table: "PropertyDefinitions",
                columns: new[] { "RealmId", "PropertyScopeId" });

            migrationBuilder.CreateIndex(
                name: "IX_PropertyDefinitionVersions_PropertyDefinitionId",
                table: "PropertyDefinitionVersions",
                column: "PropertyDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyDefinitionVersions_PropertyScopeVersionId_PropertyD~",
                table: "PropertyDefinitionVersions",
                columns: new[] { "PropertyScopeVersionId", "PropertyDefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PropertyDefinitionVersions_RealmId_PropertyDefinitionId",
                table: "PropertyDefinitionVersions",
                columns: new[] { "RealmId", "PropertyDefinitionId" });

            migrationBuilder.CreateIndex(
                name: "IX_PropertyScopes_ActiveVersionId",
                table: "PropertyScopes",
                column: "ActiveVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyScopes_RealmId_Name",
                table: "PropertyScopes",
                columns: new[] { "RealmId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PropertyScopeVersions_PropertyScopeId",
                table: "PropertyScopeVersions",
                column: "PropertyScopeId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyScopeVersions_RealmId_PropertyScopeId_Status",
                table: "PropertyScopeVersions",
                columns: new[] { "RealmId", "PropertyScopeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PropertyScopeVersions_RealmId_PropertyScopeId_Version",
                table: "PropertyScopeVersions",
                columns: new[] { "RealmId", "PropertyScopeId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserAccountActionTokens_RealmId_TokenHash",
                table: "UserAccountActionTokens",
                columns: new[] { "RealmId", "TokenHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserAccountActionTokens_RealmId_UserAccountId_Purpose",
                table: "UserAccountActionTokens",
                columns: new[] { "RealmId", "UserAccountId", "Purpose" });

            migrationBuilder.CreateIndex(
                name: "IX_UserAccountActionTokens_UserAccountId",
                table: "UserAccountActionTokens",
                column: "UserAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAccountCredentials_RealmId_UserAccountId",
                table: "UserAccountCredentials",
                columns: new[] { "RealmId", "UserAccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserAccountEmails_RealmId_NormalizedAddress",
                table: "UserAccountEmails",
                columns: new[] { "RealmId", "NormalizedAddress" });

            migrationBuilder.CreateIndex(
                name: "IX_UserAccountEmails_RealmId_UserAccountId_NormalizedAddress",
                table: "UserAccountEmails",
                columns: new[] { "RealmId", "UserAccountId", "NormalizedAddress" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserAccountEmails_UserAccountId",
                table: "UserAccountEmails",
                column: "UserAccountId");

            migrationBuilder.CreateIndex(
                name: "UX_UserAccountEmails_PrimaryPerAccount",
                table: "UserAccountEmails",
                columns: new[] { "RealmId", "UserAccountId" },
                unique: true,
                filter: "\"IsPrimary\"");

            migrationBuilder.CreateIndex(
                name: "IX_UserAccountPasswordHistory_RealmId_UserAccountId_CreatedAt",
                table: "UserAccountPasswordHistory",
                columns: new[] { "RealmId", "UserAccountId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserAccountPasswordHistory_UserAccountId",
                table: "UserAccountPasswordHistory",
                column: "UserAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAccountPhones_RealmId_NormalizedNumber",
                table: "UserAccountPhones",
                columns: new[] { "RealmId", "NormalizedNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_UserAccountPhones_RealmId_UserAccountId_NormalizedNumber",
                table: "UserAccountPhones",
                columns: new[] { "RealmId", "UserAccountId", "NormalizedNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserAccountPhones_UserAccountId",
                table: "UserAccountPhones",
                column: "UserAccountId");

            migrationBuilder.CreateIndex(
                name: "UX_UserAccountPhones_PrimaryPerAccount",
                table: "UserAccountPhones",
                columns: new[] { "RealmId", "UserAccountId" },
                unique: true,
                filter: "\"IsPrimary\"");

            migrationBuilder.CreateIndex(
                name: "IX_UserAccountPropertyValues_PropertyDefinitionId",
                table: "UserAccountPropertyValues",
                column: "PropertyDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAccountPropertyValues_RealmId_ClaimType",
                table: "UserAccountPropertyValues",
                columns: new[] { "RealmId", "ClaimType" });

            migrationBuilder.CreateIndex(
                name: "IX_UserAccountPropertyValues_RealmId_UserAccountId_ClaimType",
                table: "UserAccountPropertyValues",
                columns: new[] { "RealmId", "UserAccountId", "ClaimType" });

            migrationBuilder.CreateIndex(
                name: "IX_UserAccountPropertyValues_UserAccountId_PropertyDefinitionI~",
                table: "UserAccountPropertyValues",
                columns: new[] { "UserAccountId", "PropertyDefinitionId", "Ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserAccountRoles_RealmId_NormalizedName",
                table: "UserAccountRoles",
                columns: new[] { "RealmId", "NormalizedName" });

            migrationBuilder.CreateIndex(
                name: "IX_UserAccountRoles_RealmId_UserAccountId_NormalizedName",
                table: "UserAccountRoles",
                columns: new[] { "RealmId", "UserAccountId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserAccountRoles_UserAccountId",
                table: "UserAccountRoles",
                column: "UserAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAccounts_RealmId_ExternalId",
                table: "UserAccounts",
                columns: new[] { "RealmId", "ExternalId" },
                filter: "\"ExternalId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserAccounts_RealmId_NormalizedUsername",
                table: "UserAccounts",
                columns: new[] { "RealmId", "NormalizedUsername" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserAccounts_RealmId_SubjectId",
                table: "UserAccounts",
                columns: new[] { "RealmId", "SubjectId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PropertyDefinitionVersions");

            migrationBuilder.DropTable(
                name: "UserAccountActionTokens");

            migrationBuilder.DropTable(
                name: "UserAccountCredentials");

            migrationBuilder.DropTable(
                name: "UserAccountEmails");

            migrationBuilder.DropTable(
                name: "UserAccountPasswordHistory");

            migrationBuilder.DropTable(
                name: "UserAccountPhones");

            migrationBuilder.DropTable(
                name: "UserAccountPropertyValues");

            migrationBuilder.DropTable(
                name: "UserAccountRoles");

            migrationBuilder.DropTable(
                name: "PropertyScopeVersions");

            migrationBuilder.DropTable(
                name: "PropertyDefinitions");

            migrationBuilder.DropTable(
                name: "UserAccounts");

            migrationBuilder.DropTable(
                name: "PropertyScopes");
        }
    }
}
