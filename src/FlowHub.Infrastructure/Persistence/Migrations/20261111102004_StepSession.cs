using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StepSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SessionId",
                table: "StepRun",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "StepRun");
        }
    }
}
