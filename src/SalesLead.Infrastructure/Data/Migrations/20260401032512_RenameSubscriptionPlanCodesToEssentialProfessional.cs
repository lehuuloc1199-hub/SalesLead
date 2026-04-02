using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesLead.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameSubscriptionPlanCodesToEssentialProfessional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rename subscription tier codes (PK on SubscriptionPlans, FK on TenantSubscriptions).
            migrationBuilder.Sql(@"
INSERT INTO SubscriptionPlans (PlanCode, IngestRpm, IngestBurst, BulkRowsPerDay, MaxConcurrentBulkJobs)
SELECT 'Essential', IngestRpm, IngestBurst, BulkRowsPerDay, MaxConcurrentBulkJobs FROM SubscriptionPlans WHERE PlanCode = 'Standard'
AND NOT EXISTS (SELECT 1 FROM SubscriptionPlans WHERE PlanCode = 'Essential');

UPDATE TenantSubscriptions SET PlanCode = 'Essential' WHERE PlanCode = 'Standard';

DELETE FROM SubscriptionPlans WHERE PlanCode = 'Standard';

INSERT INTO SubscriptionPlans (PlanCode, IngestRpm, IngestBurst, BulkRowsPerDay, MaxConcurrentBulkJobs)
SELECT 'Professional', IngestRpm, IngestBurst, BulkRowsPerDay, MaxConcurrentBulkJobs FROM SubscriptionPlans WHERE PlanCode = 'Vip'
AND NOT EXISTS (SELECT 1 FROM SubscriptionPlans WHERE PlanCode = 'Professional');

UPDATE TenantSubscriptions SET PlanCode = 'Professional' WHERE PlanCode = 'Vip';

DELETE FROM SubscriptionPlans WHERE PlanCode = 'Vip';
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
INSERT INTO SubscriptionPlans (PlanCode, IngestRpm, IngestBurst, BulkRowsPerDay, MaxConcurrentBulkJobs)
SELECT 'Vip', IngestRpm, IngestBurst, BulkRowsPerDay, MaxConcurrentBulkJobs FROM SubscriptionPlans WHERE PlanCode = 'Professional'
AND NOT EXISTS (SELECT 1 FROM SubscriptionPlans WHERE PlanCode = 'Vip');

UPDATE TenantSubscriptions SET PlanCode = 'Vip' WHERE PlanCode = 'Professional';

DELETE FROM SubscriptionPlans WHERE PlanCode = 'Professional';

INSERT INTO SubscriptionPlans (PlanCode, IngestRpm, IngestBurst, BulkRowsPerDay, MaxConcurrentBulkJobs)
SELECT 'Standard', IngestRpm, IngestBurst, BulkRowsPerDay, MaxConcurrentBulkJobs FROM SubscriptionPlans WHERE PlanCode = 'Essential'
AND NOT EXISTS (SELECT 1 FROM SubscriptionPlans WHERE PlanCode = 'Standard');

UPDATE TenantSubscriptions SET PlanCode = 'Standard' WHERE PlanCode = 'Essential';

DELETE FROM SubscriptionPlans WHERE PlanCode = 'Essential';
");
        }
    }
}
