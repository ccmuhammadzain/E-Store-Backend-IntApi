using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventoryApi.Migrations
{
    /// <inheritdoc />
    public partial class AddBillLifecycleFields2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add new lifecycle / checkout columns if they do not already exist
            migrationBuilder.Sql("IF COL_LENGTH('dbo.Bills','Status') IS NULL ALTER TABLE [Bills] ADD [Status] int NOT NULL CONSTRAINT DF_Bills_Status DEFAULT 0;");
            migrationBuilder.Sql("IF COL_LENGTH('dbo.Bills','PaidAt') IS NULL ALTER TABLE [Bills] ADD [PaidAt] datetime2 NULL;");
            migrationBuilder.Sql("IF COL_LENGTH('dbo.Bills','PaymentReference') IS NULL ALTER TABLE [Bills] ADD [PaymentReference] nvarchar(max) NULL;");
            migrationBuilder.Sql("IF COL_LENGTH('dbo.Bills','CustomerName') IS NULL ALTER TABLE [Bills] ADD [CustomerName] nvarchar(max) NULL;");
            migrationBuilder.Sql("IF COL_LENGTH('dbo.Bills','AddressLine1') IS NULL ALTER TABLE [Bills] ADD [AddressLine1] nvarchar(max) NULL;");
            migrationBuilder.Sql("IF COL_LENGTH('dbo.Bills','City') IS NULL ALTER TABLE [Bills] ADD [City] nvarchar(max) NULL;");
            migrationBuilder.Sql("IF COL_LENGTH('dbo.Bills','Country') IS NULL ALTER TABLE [Bills] ADD [Country] nvarchar(max) NULL;");
            migrationBuilder.Sql("IF COL_LENGTH('dbo.Bills','Phone') IS NULL ALTER TABLE [Bills] ADD [Phone] nvarchar(max) NULL;");

            // Remove default constraint name (optional clean-up) - safe ignore if not present
            migrationBuilder.Sql("DECLARE @c sysname; SELECT @c = dc.name FROM sys.default_constraints dc JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id WHERE dc.parent_object_id = OBJECT_ID('dbo.Bills') AND c.name = 'Status'; IF @c IS NOT NULL EXEC('ALTER TABLE dbo.Bills DROP CONSTRAINT ' + @c);");

            // Clean orphan Bills referencing non-existing Users to allow FK creation
            migrationBuilder.Sql("DELETE FROM [Bills] WHERE [UserId] NOT IN (SELECT [Id] FROM [Users]);");

            // Create index if missing
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Bills_UserId' AND object_id = OBJECT_ID('dbo.Bills')) CREATE INDEX [IX_Bills_UserId] ON [Bills]([UserId]);");

            // Add FK if missing
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Bills_Users_UserId') ALTER TABLE [Bills] ADD CONSTRAINT [FK_Bills_Users_UserId] FOREIGN KEY([UserId]) REFERENCES [Users]([Id]) ON DELETE CASCADE;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop FK and index (if exist)
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Bills_Users_UserId') ALTER TABLE [Bills] DROP CONSTRAINT [FK_Bills_Users_UserId];");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Bills_UserId' AND object_id = OBJECT_ID('dbo.Bills')) DROP INDEX [IX_Bills_UserId] ON [Bills];");

            // Drop added columns (if exist)
            migrationBuilder.Sql("IF COL_LENGTH('dbo.Bills','Phone') IS NOT NULL ALTER TABLE [Bills] DROP COLUMN [Phone];");
            migrationBuilder.Sql("IF COL_LENGTH('dbo.Bills','Country') IS NOT NULL ALTER TABLE [Bills] DROP COLUMN [Country];");
            migrationBuilder.Sql("IF COL_LENGTH('dbo.Bills','City') IS NOT NULL ALTER TABLE [Bills] DROP COLUMN [City];");
            migrationBuilder.Sql("IF COL_LENGTH('dbo.Bills','AddressLine1') IS NOT NULL ALTER TABLE [Bills] DROP COLUMN [AddressLine1];");
            migrationBuilder.Sql("IF COL_LENGTH('dbo.Bills','CustomerName') IS NOT NULL ALTER TABLE [Bills] DROP COLUMN [CustomerName];");
            migrationBuilder.Sql("IF COL_LENGTH('dbo.Bills','PaymentReference') IS NOT NULL ALTER TABLE [Bills] DROP COLUMN [PaymentReference];");
            migrationBuilder.Sql("IF COL_LENGTH('dbo.Bills','PaidAt') IS NOT NULL ALTER TABLE [Bills] DROP COLUMN [PaidAt];");
            migrationBuilder.Sql("IF COL_LENGTH('dbo.Bills','Status') IS NOT NULL ALTER TABLE [Bills] DROP COLUMN [Status];");
        }
    }
}
