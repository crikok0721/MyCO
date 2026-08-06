using System.Text.RegularExpressions;

namespace MyCO.Tests;

public sealed class RequirementsLedgerTests
{
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "MyCO.sln")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new DirectoryNotFoundException(
            "Repository root could not be located.");
    }

    [Fact]
    public void LedgerHasUniqueIdsAndLegalStatuses()
    {
        var path = Path.Combine(RepositoryRoot(), "docs", "REQUIREMENTS.md");
        var text = File.ReadAllText(path);
        var rows = text.Split('\n')
            .Where(line => line.StartsWith('|') && !line.StartsWith("|---"))
            .Skip(1)
            .ToArray();
        var ids = rows.Select(row => row.Split('|')[1].Trim()).ToArray();
        Assert.NotEmpty(ids);
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.All(ids, id => Assert.Matches("^[A-Z][A-Z0-9-]+$", id));
        var legalStatuses = new HashSet<string>(StringComparer.Ordinal)
        {
            "Verified Implemented",
            "Implemented Unverified",
            "Partial",
            "Regressed",
            "Pending",
            "Superseded",
            "Rejected",
            "Unknown"
        };
        foreach (var row in rows)
        {
            var fields = row.Split('|').Select(value => value.Trim()).ToArray();
            Assert.True(fields.Length >= 14, $"Ledger row is missing fields: {row}");
            Assert.Contains(fields[6], legalStatuses);
            Assert.False(string.IsNullOrWhiteSpace(fields[7]));
            Assert.False(string.IsNullOrWhiteSpace(fields[8]));
            Assert.False(string.IsNullOrWhiteSpace(fields[9]));
            if (fields[6] == "Superseded")
            {
                Assert.False(string.IsNullOrWhiteSpace(fields[12]));
            }
        }
    }

    [Fact]
    public void AuditReferencesLedgerIdsAndDoesNotDuplicateRequirementTruth()
    {
        var root = RepositoryRoot();
        var ledger = File.ReadAllText(Path.Combine(root, "docs", "REQUIREMENTS.md"));
        var audit = File.ReadAllText(Path.Combine(root, "docs", "REQUIREMENTS_AUDIT.md"));
        var ids = Regex.Matches(ledger, @"\| ([A-Z][A-Z0-9-]+) \|")
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
        var errorLedger = File.ReadAllText(Path.Combine(root, "docs", "ERROR_LEDGER.md"));
        foreach (Match match in Regex.Matches(errorLedger, @"\| (ERR-[0-9]{3}) \|"))
        {
            ids.Add(match.Groups[1].Value);
        }
        var references = Regex.Matches(audit, @"\b[A-Z][A-Z0-9-]+-[0-9]{3}\b")
            .Select(match => match.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        Assert.NotEmpty(references);
        Assert.All(references, id => Assert.Contains(id, ids));
        Assert.DoesNotContain("| Requirement |", audit, StringComparison.Ordinal);
    }

    [Fact]
    public void ErrorLedgerHasUniqueIdsRequiredFieldsAndResolvableRequirements()
    {
        var root = RepositoryRoot();
        var requirements = File.ReadAllText(Path.Combine(root, "docs", "REQUIREMENTS.md"));
        var requirementIds = Regex.Matches(
                requirements,
                @"\| ([A-Z][A-Z0-9-]+) \|")
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
        var errorPath = Path.Combine(root, "docs", "ERROR_LEDGER.md");
        Assert.True(File.Exists(errorPath));
        var rows = File.ReadAllLines(errorPath)
            .Where(line => line.StartsWith('|') && !line.StartsWith("|---"))
            .Skip(1)
            .ToArray();
        Assert.NotEmpty(rows);
        var ids = rows.Select(row => row.Split('|')[1].Trim()).ToArray();
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.All(ids, id => Assert.Matches("^ERR-[0-9]{3}$", id));
        foreach (var row in rows)
        {
            var fields = row.Split('|').Select(value => value.Trim()).ToArray();
            Assert.True(fields.Length >= 12, $"Error row is missing fields: {row}");
            Assert.False(string.IsNullOrWhiteSpace(fields[2]));
            Assert.False(string.IsNullOrWhiteSpace(fields[3]));
            Assert.False(string.IsNullOrWhiteSpace(fields[4]));
            Assert.False(string.IsNullOrWhiteSpace(fields[5]));
            foreach (var requirementId in fields[2].Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                Assert.Contains(requirementId.Trim(), requirementIds);
            }
        }
    }

    [Fact]
    public void AutonomousWorkflowSupersedesTheOldConfirmationGate()
    {
        var root = RepositoryRoot();
        var agents = File.ReadAllText(Path.Combine(root, "AGENTS.md"));
        var claude = File.ReadAllText(Path.Combine(root, "CLAUDE.md"));

        Assert.Contains("自动执行", agents, StringComparison.Ordinal);
        Assert.Contains("superseded", claude, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("automatically", claude, StringComparison.OrdinalIgnoreCase);
    }
}
