using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventoryApi.Migrations
{
    [Migration("20250828120000_AddUserDeactivationAndProductSoftDelete")]
    public partial class AddUserDeactivationAndProductSoftDelete : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // User deactivation columns (add if missing guards)
            migrationBuilder.Sql(@"IF COL_LENGTH('dbo.Users','IsActive') IS NULL ALTER TABLE dbo.Users ADD IsActive bit NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT(1);");
            migrationBuilder.Sql(@"IF COL_LENGTH('dbo.Users','DeactivatedAt') IS NULL ALTER TABLE dbo.Users ADD DeactivatedAt datetime2 NULL;");
            migrationBuilder.Sql(@"IF COL_LENGTH('dbo.Users','DeactivatedById') IS NULL ALTER TABLE dbo.Users ADD DeactivatedById int NULL;");
            migrationBuilder.Sql(@"IF COL_LENGTH('dbo.Users','RowVersion') IS NULL ALTER TABLE dbo.Users ADD RowVersion rowversion NULL;");

            // Product soft delete column
            migrationBuilder.Sql(@"IF COL_LENGTH('dbo.Products','IsActive') IS NULL ALTER TABLE dbo.Products ADD IsActive bit NOT NULL CONSTRAINT DF_Products_IsActive DEFAULT(1);");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"IF COL_LENGTH('dbo.Users','IsActive') IS NOT NULL ALTER TABLE dbo.Users DROP CONSTRAINT DF_Users_IsActive; IF COL_LENGTH('dbo.Users','IsActive') IS NOT NULL ALTER TABLE dbo.Users DROP COLUMN IsActive;");
            migrationBuilder.Sql(@"IF COL_LENGTH('dbo.Users','DeactivatedAt') IS NOT NULL ALTER TABLE dbo.Users DROP COLUMN DeactivatedAt;");
            migrationBuilder.Sql(@"IF COL_LENGTH('dbo.Users','DeactivatedById') IS NOT NULL ALTER TABLE dbo.Users DROP COLUMN DeactivatedById;");
            migrationBuilder.Sql(@"IF COL_LENGTH('dbo.Users','RowVersion') IS NOT NULL ALTER TABLE dbo.Users DROP COLUMN RowVersion;");
            migrationBuilder.Sql(@"IF COL_LENGTH('dbo.Products','IsActive') IS NOT NULL ALTER TABLE dbo.Products DROP CONSTRAINT DF_Products_IsActive; IF COL_LENGTH('dbo.Products','IsActive') IS NOT NULL ALTER TABLE dbo.Products DROP COLUMN IsActive;");
        }
    }
}
