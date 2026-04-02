using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesLead.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SubscriptionPlans",
                columns: table => new
                {
                    PlanCode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    IngestRpm = table.Column<int>(type: "INTEGER", nullable: false),
                    IngestBurst = table.Column<int>(type: "INTEGER", nullable: false),
                    BulkRowsPerDay = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxConcurrentBulkJobs = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPlans", x => x.PlanCode);
                });

            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    IsolationModel = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LeadActivityTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TypeName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Icon = table.Column<string>(type: "TEXT", nullable: true),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadActivityTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeadActivityTypes_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LeadStatuses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StatusName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadStatuses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeadStatuses_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AggregateType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    AggregateId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ProcessedUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OutboxMessages_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TenantApiKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    KeyHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantApiKeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantApiKeys_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TenantSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlanCode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    StartsAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantSubscriptions_SubscriptionPlans_PlanCode",
                        column: x => x.PlanCode,
                        principalTable: "SubscriptionPlans",
                        principalColumn: "PlanCode",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenantSubscriptions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TenantUsageDaily",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UtcDate = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    IngestCount = table.Column<int>(type: "INTEGER", nullable: false),
                    BulkRowsAccepted = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantUsageDaily", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantUsageDaily_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TenantUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    FullName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantUsers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Leads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LeadStatusId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AssignedUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ExternalId = table.Column<string>(type: "TEXT", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "TEXT", nullable: true),
                    FirstName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    LastName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Phone = table.Column<string>(type: "TEXT", nullable: true),
                    VehicleInterest = table.Column<string>(type: "TEXT", nullable: true),
                    Source = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastContactAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Leads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Leads_LeadStatuses_LeadStatusId",
                        column: x => x.LeadStatusId,
                        principalTable: "LeadStatuses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Leads_TenantUsers_AssignedUserId",
                        column: x => x.AssignedUserId,
                        principalTable: "TenantUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Leads_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LeadActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LeadId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ActivityTypeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    ActivityDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeadActivities_LeadActivityTypes_ActivityTypeId",
                        column: x => x.ActivityTypeId,
                        principalTable: "LeadActivityTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeadActivities_Leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "Leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LeadActivities_TenantUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "TenantUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "LeadIngestionRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "TEXT", nullable: true),
                    ExternalId = table.Column<string>(type: "TEXT", nullable: true),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ResolvedLeadId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    ReceivedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadIngestionRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeadIngestionRecords_Leads_ResolvedLeadId",
                        column: x => x.ResolvedLeadId,
                        principalTable: "Leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LeadIngestionRecords_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeadActivities_ActivityTypeId",
                table: "LeadActivities",
                column: "ActivityTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadActivities_CreatedByUserId",
                table: "LeadActivities",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadActivities_LeadId_ActivityDate",
                table: "LeadActivities",
                columns: new[] { "LeadId", "ActivityDate" });

            migrationBuilder.CreateIndex(
                name: "IX_LeadActivityTypes_TenantId_TypeName",
                table: "LeadActivityTypes",
                columns: new[] { "TenantId", "TypeName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeadIngestionRecords_ResolvedLeadId",
                table: "LeadIngestionRecords",
                column: "ResolvedLeadId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadIngestionRecords_TenantId_ExternalId",
                table: "LeadIngestionRecords",
                columns: new[] { "TenantId", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeadIngestionRecords_TenantId_IdempotencyKey",
                table: "LeadIngestionRecords",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeadIngestionRecords_TenantId_ReceivedUtc",
                table: "LeadIngestionRecords",
                columns: new[] { "TenantId", "ReceivedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Leads_AssignedUserId",
                table: "Leads",
                column: "AssignedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_LeadStatusId",
                table: "Leads",
                column: "LeadStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_TenantId_CreatedUtc",
                table: "Leads",
                columns: new[] { "TenantId", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Leads_TenantId_ExternalId",
                table: "Leads",
                columns: new[] { "TenantId", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Leads_TenantId_IdempotencyKey",
                table: "Leads",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Leads_TenantId_LeadStatusId",
                table: "Leads",
                columns: new[] { "TenantId", "LeadStatusId" });

            migrationBuilder.CreateIndex(
                name: "IX_LeadStatuses_TenantId_StatusName",
                table: "LeadStatuses",
                columns: new[] { "TenantId", "StatusName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_ProcessedUtc",
                table: "OutboxMessages",
                column: "ProcessedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_TenantId",
                table: "OutboxMessages",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantApiKeys_KeyHash",
                table: "TenantApiKeys",
                column: "KeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantApiKeys_TenantId",
                table: "TenantApiKeys",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantSubscriptions_PlanCode",
                table: "TenantSubscriptions",
                column: "PlanCode");

            migrationBuilder.CreateIndex(
                name: "IX_TenantSubscriptions_TenantId",
                table: "TenantSubscriptions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantUsageDaily_TenantId_UtcDate",
                table: "TenantUsageDaily",
                columns: new[] { "TenantId", "UtcDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantUsers_TenantId_Email",
                table: "TenantUsers",
                columns: new[] { "TenantId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantUsers_TenantId_Username",
                table: "TenantUsers",
                columns: new[] { "TenantId", "Username" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeadActivities");

            migrationBuilder.DropTable(
                name: "LeadIngestionRecords");

            migrationBuilder.DropTable(
                name: "OutboxMessages");

            migrationBuilder.DropTable(
                name: "TenantApiKeys");

            migrationBuilder.DropTable(
                name: "TenantSubscriptions");

            migrationBuilder.DropTable(
                name: "TenantUsageDaily");

            migrationBuilder.DropTable(
                name: "LeadActivityTypes");

            migrationBuilder.DropTable(
                name: "Leads");

            migrationBuilder.DropTable(
                name: "SubscriptionPlans");

            migrationBuilder.DropTable(
                name: "LeadStatuses");

            migrationBuilder.DropTable(
                name: "TenantUsers");

            migrationBuilder.DropTable(
                name: "Tenants");
        }
    }
}
