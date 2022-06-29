using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Evento.Db.Migrations
{
    public partial class ChangesubscriptionpkfromstringtoGuid : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "id",
                table: "subscriptions",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text"
            );
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "id",
                table: "subscriptions",
                type: "text",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid"
            );
        }
    }
}
