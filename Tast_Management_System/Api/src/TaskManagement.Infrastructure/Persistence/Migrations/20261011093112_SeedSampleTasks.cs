using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManagement.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Seeds 30 sample tasks (27 active, 3 soft-deleted) in every environment (task.md T7a).
    /// The INSERT statements live in Persistence/Migrations/Sql/SeedSampleTasks.sql (embedded resource),
    /// so they can be reviewed and run on their own. Down() removes only the seed Id range.
    /// </summary>
    public partial class SeedSampleTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SqlScripts.Read("SeedSampleTasks.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SqlScripts.Read("RemoveSampleTasks.sql"));
        }
    }
}
