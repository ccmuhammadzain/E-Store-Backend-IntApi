using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventoryApi.Migrations
{
    public partial class SyncProductColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add CreatedAt if missing
            migrationBuilder.Sql(@"IF COL_LENGTH('dbo.Products','CreatedAt') IS NULL 
                ALTER TABLE dbo.Products ADD CreatedAt datetime2 NOT NULL CONSTRAINT DF_Products_CreatedAt DEFAULT (SYSUTCDATETIME());");

            // Add UpdatedAt if missing
            migrationBuilder.Sql(@"IF COL_LENGTH('dbo.Products','UpdatedAt') IS NULL 
                ALTER TABLE dbo.Products ADD UpdatedAt datetime2 NULL;");

            // Add OwnerId if missing (nullable first)
            migrationBuilder.Sql(@"IF COL_LENGTH('dbo.Products','OwnerId') IS NULL 
                ALTER TABLE dbo.Products ADD OwnerId int NULL;");

            // Backfill OwnerId with first user if null
            migrationBuilder.Sql(@"IF COL_LENGTH('dbo.Products','OwnerId') IS NOT NULL 
                UPDATE p SET OwnerId = u.Id FROM Products p CROSS APPLY (SELECT TOP 1 Id FROM Users ORDER BY Id) u WHERE p.OwnerId IS NULL;");

            // Create index if missing
            migrationBuilder.Sql(@"IF COL_LENGTH('dbo.Products','OwnerId') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Products_OwnerId' AND object_id = OBJECT_ID('dbo.Products'))
                CREATE INDEX IX_Products_OwnerId ON dbo.Products(OwnerId);");

            // Add FK if missing
            migrationBuilder.Sql(@"IF COL_LENGTH('dbo.Products','OwnerId') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_Products_Users_OwnerId')
                ALTER TABLE dbo.Products ADD CONSTRAINT FK_Products_Users_OwnerId FOREIGN KEY (OwnerId) REFERENCES dbo.Users(Id) ON DELETE RESTRICT;");

            // Make OwnerId NOT NULL if all rows populated
            migrationBuilder.Sql(@"IF COL_LENGTH('dbo.Products','OwnerId') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Products WHERE OwnerId IS NULL)
                ALTER TABLE dbo.Products ALTER COLUMN OwnerId int NOT NULL;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse in order: drop FK, index, columns (optional keep audit columns) 
            migrationBuilder.Sql(@"IF OBJECT_ID('FK_Products_Users_OwnerId','F') IS NOT NULL ALTER TABLE dbo.Products DROP CONSTRAINT FK_Products_Users_OwnerId;");
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Products_OwnerId' AND object_id = OBJECT_ID('dbo.Products')) DROP INDEX IX_Products_OwnerId ON dbo.Products;");
            // Optionally drop columns (commented to preserve data)
            // migrationBuilder.Sql("IF COL_LENGTH('dbo.Products','OwnerId') IS NOT NULL ALTER TABLE dbo.Products DROP COLUMN OwnerId;");
            // migrationBuilder.Sql("IF COL_LENGTH('dbo.Products','CreatedAt') IS NOT NULL ALTER TABLE dbo.Products DROP COLUMN CreatedAt;");
            // migrationBuilder.Sql("IF COL_LENGTH('dbo.Products','UpdatedAt') IS NOT NULL ALTER TABLE dbo.Products DROP COLUMN UpdatedAt;");
        }
    }
}
