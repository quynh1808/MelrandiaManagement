using MelrandiaManagement.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MelrandiaManagement.Migrations;

[DbContext(typeof(ManagementDbContext))]
[Migration("20261107100000_AddArticleTags")]
public partial class AddArticleTags : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Tags",
            table: "articles",
            type: "character varying(1000)",
            maxLength: 1000,
            nullable: false,
            defaultValue: "");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Tags",
            table: "articles");
    }
}
