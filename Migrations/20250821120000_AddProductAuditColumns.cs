using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventoryApi.Migrations
{
    public partial class AddProductAuditColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // CreatedAt column
            migrationBuilder.Sql(@"IF COL_LENGTH('dbo.Products','CreatedAt') IS NULL BEGIN
                ALTER TABLE dbo.Products ADD CreatedAt datetime2 NOT NULL CONSTRAINT DF_Products_CreatedAt DEFAULT (SYSUTCDATETIME());
            END");

            // UpdatedAt column
            migrationBuilder.Sql(@"IF COL_LENGTH('dbo.Products','UpdatedAt') IS NULL BEGIN
                ALTER TABLE dbo.Products ADD UpdatedAt datetime2 NULL;
            END");

            // OwnerId column (if earlier migration not applied)
            migrationBuilder.Sql(@"IF COL_LENGTH('dbo.Products','OwnerId') IS NULL BEGIN
                ALTER TABLE dbo.Products ADD OwnerId int NULL;
            END");

            // Ensure at least one user exists to assign ownership for legacy rows
            migrationBuilder.Sql(@"IF COL_LENGTH('dbo.Products','OwnerId') IS NOT NULL BEGIN
                UPDATE p SET OwnerId = u.Id
                FROM Products p
                CROSS APPLY (SELECT TOP 1 Id FROM Users ORDER BY Id) u
                WHERE p.OwnerId IS NULL;
            END");

            // Create index if missing
            migrationBuilder.Sql(@"IF COL_LENGTH('dbo.Products','OwnerId') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Products_OwnerId' AND object_id = OBJECT_ID('dbo.Products'))
                CREATE INDEX IX_Products_OwnerId ON dbo.Products(OwnerId);");

            // Add FK if missing, then enforce NOT NULL if possible
            migrationBuilder.Sql(@"IF COL_LENGTH('dbo.Products','OwnerId') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_Products_Users_OwnerId')
                ALTER TABLE dbo.Products ADD CONSTRAINT FK_Products_Users_OwnerId FOREIGN KEY(OwnerId) REFERENCES dbo.Users(Id) ON DELETE RESTRICT;");

            // Attempt to set NOT NULL if no NULLs left
            migrationBuilder.Sql(@"IF COL_LENGTH('dbo.Products','OwnerId') IS NOT NULL AND NOT EXISTS(SELECT 1 FROM Products WHERE OwnerId IS NULL)
                BEGIN
                    DECLARE @sql NVARCHAR(MAX)='ALTER TABLE dbo.Products ALTER COLUMN OwnerId int NOT NULL';
                    EXEC(@sql);
                END");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert changes (do not drop CreatedAt/UpdatedAt by default to preserve audit)
            migrationBuilder.Sql(@"IF OBJECT_ID('FK_Products_Users_OwnerId','F') IS NOT NULL ALTER TABLE dbo.Products DROP CONSTRAINT FK_Products_Users_OwnerId;");
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Products_OwnerId' AND object_id = OBJECT_ID('dbo.Products')) DROP INDEX IX_Products_OwnerId ON dbo.Products;");
            // Optional drops (commented out to keep audit history)
            // migrationBuilder.Sql("IF COL_LENGTH('dbo.Products','OwnerId') IS NOT NULL ALTER TABLE dbo.Products DROP COLUMN OwnerId;");
            // migrationBuilder.Sql("IF COL_LENGTH('dbo.Products','CreatedAt') IS NOT NULL ALTER TABLE dbo.Products DROP COLUMN CreatedAt;");
            // migrationBuilder.Sql("IF COL_LENGTH('dbo.Products','UpdatedAt') IS NOT NULL ALTER TABLE dbo.Products DROP COLUMN UpdatedAt;");
        }
    }
}
