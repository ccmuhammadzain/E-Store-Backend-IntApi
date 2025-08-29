using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventoryApi.Migrations
{
    public partial class AddUserDeactivationModelConfig : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Users table additions if missing
            migrationBuilder.Sql(@"IF COL_LENGTH('dbo.Users','IsActive') IS NULL BEGIN ALTER TABLE dbo.Users ADD IsActive bit NOT NULL CONSTRAINT DF_Users_IsActive2 DEFAULT(1); END");
            migrationBuilder.Sql(@"IF COL_LENGTH('dbo.Users','DeactivatedAt') IS NULL BEGIN ALTER TABLE dbo.Users ADD DeactivatedAt datetime2 NULL; END");
            migrationBuilder.Sql(@"IF COL_LENGTH('dbo.Users','DeactivatedById') IS NULL BEGIN ALTER TABLE dbo.Users ADD DeactivatedById int NULL; END");
            migrationBuilder.Sql(@"IF COL_LENGTH('dbo.Users','RowVersion') IS NULL BEGIN ALTER TABLE dbo.Users ADD RowVersion rowversion NULL; END");
            // Products IsActive
            migrationBuilder.Sql(@"IF COL_LENGTH('dbo.Products','IsActive') IS NULL BEGIN ALTER TABLE dbo.Products ADD IsActive bit NOT NULL CONSTRAINT DF_Products_IsActive2 DEFAULT(1); END");

            // FK for DeactivatedById if not exists
            migrationBuilder.Sql(@"IF COL_LENGTH('dbo.Users','DeactivatedById') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_Users_Users_DeactivatedById') ALTER TABLE dbo.Users ADD CONSTRAINT FK_Users_Users_DeactivatedById FOREIGN KEY(DeactivatedById) REFERENCES dbo.Users(Id) ON DELETE SET NULL;");

            // Indexes
            migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Users_IsActive' AND object_id=OBJECT_ID('dbo.Users')) CREATE INDEX IX_Users_IsActive ON dbo.Users(IsActive);");
            migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Products_IsActive' AND object_id=OBJECT_ID('dbo.Products')) CREATE INDEX IX_Products_IsActive ON dbo.Products(IsActive);");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"IF OBJECT_ID('dbo.Users','U') IS NOT NULL BEGIN IF EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_Users_Users_DeactivatedById') ALTER TABLE dbo.Users DROP CONSTRAINT FK_Users_Users_DeactivatedById; END");
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Users_IsActive' AND object_id=OBJECT_ID('dbo.Users')) DROP INDEX IX_Users_IsActive ON dbo.Users;");
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Products_IsActive' AND object_id=OBJECT_ID('dbo.Products')) DROP INDEX IX_Products_IsActive ON dbo.Products;");
            // Leave columns (data retention) or optionally drop (commented out)
            // migrationBuilder.Sql(@"ALTER TABLE dbo.Users DROP COLUMN IsActive, DeactivatedAt, DeactivatedById, RowVersion;");
            // migrationBuilder.Sql(@"ALTER TABLE dbo.Products DROP COLUMN IsActive;");
        }
    }
}
