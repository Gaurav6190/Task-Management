using TaskManagement.Infrastructure.Persistence;

namespace TaskManagement.Infrastructure.UnitTests.Persistence;

[TestFixture]
public class SqlScriptsTests
{
    [Test]
    public void Summary_query_is_embedded_and_excludes_deleted_tasks()
    {
        var sql = SqlScripts.Read("TaskSummary.sql");

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain("GROUP BY t.Status, t.Priority"));
            Assert.That(sql, Does.Contain("t.IsDeleted = 0"));
        });
    }

    [Test]
    public void Seed_script_is_embedded_has_30_rows_and_no_batch_separators()
    {
        // Only executable SQL matters; the header comment explains these rules in words.
        var sql = string.Join('\n', SqlScripts.Read("SeedSampleTasks.sql").Split('\n')
            .Where(line => !line.TrimStart().StartsWith("--", StringComparison.Ordinal)));
        var rows = System.Text.RegularExpressions.Regex.Matches(sql, @"^\s*\(\s*\d{1,2},\s*N'", System.Text.RegularExpressions.RegexOptions.Multiline);

        Assert.Multiple(() =>
        {
            Assert.That(rows, Has.Count.EqualTo(30));
            Assert.That(sql, Does.Contain("WHERE NOT EXISTS"), "idempotent");
            Assert.That(sql, Does.Not.Match(@"(?im)^\s*GO\s*$"), "EF runs one batch: no GO");
            Assert.That(sql, Does.Not.Contain("BEGIN TRAN"), "EF wraps migrations in a transaction");
        });
    }

    [Test]
    public void Remove_script_only_targets_the_seed_id_range() =>
        Assert.That(SqlScripts.Read("RemoveSampleTasks.sql"), Does.Contain("'5EED0000-0000-0000-0000-00000000'"));

    [Test]
    public void Unknown_script_throws_a_clear_error()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => SqlScripts.Read("DoesNotExist.sql"));
        Assert.That(ex!.Message, Does.Contain("DoesNotExist.sql"));
    }
}
