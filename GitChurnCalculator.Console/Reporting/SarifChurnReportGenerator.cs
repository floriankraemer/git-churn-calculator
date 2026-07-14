using System.Reflection;
using System.Text.Json.Serialization;
using GitChurnCalculator.Models;

namespace GitChurnCalculator.Console.Reporting;

public sealed class SarifChurnReportGenerator : IChurnReportGenerator
{
    private const string RuleId = "churn/file-risk";

    public string Generate(IReadOnlyList<FileChurnResult> results, string subtitle)
    {
        _ = subtitle;
        var toolVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
        var run = new SarifRun
        {
            Tool = new SarifTool
            {
                Driver = new SarifToolDriver
                {
                    Name = "GitChurnCalculator",
                    Version = toolVersion,
                    Rules =
                    [
                        new SarifRule
                        {
                            Id = RuleId,
                            Name = "File churn risk",
                            ShortDescription = new SarifText { Text = "High churn and author spread relative to coverage increases maintenance risk." },
                            FullDescription = new SarifText { Text = "Computed churn risk score per tracked file (commits, authors, coverage)." },
                        },
                    ],
                },
            },
            Results = results.Select(r => new SarifResult
            {
                RuleId = RuleId,
                Kind = "fail",
                Level = ChurnCiSeverity.SarifLevel(r.ChurnRiskScore),
                Message = new SarifText { Text = ChurnCiSeverity.BuildMessage(r) },
                Locations =
                [
                    new SarifLocation
                    {
                        PhysicalLocation = new SarifPhysicalLocation
                        {
                            ArtifactLocation = new SarifArtifactLocation
                            {
                                Uri = ChurnCiEncoding.NormalizeFilePath(r.FilePath),
                            },
                            Region = new SarifRegion { StartLine = 1, EndLine = 1 },
                        },
                    },
                ],
                Properties = new Dictionary<string, object?>
                {
                    ["churnRiskScore"] = r.ChurnRiskScore,
                    ["totalCommits"] = r.TotalCommits,
                    ["totalUniqueAuthors"] = r.TotalUniqueAuthors,
                    ["coveragePercent"] = r.CoveragePercent,
                    ["changesPerWeek"] = r.ChangesPerWeek,
                },
            }).ToList(),
        };

        var log = new SarifLog
        {
            Schema = "https://raw.githubusercontent.com/oasis-tcs/sarif-spec/master/Schemata/sarif-schema-2.1.0.json",
            Version = "2.1.0",
            Runs = [run],
        };

        return System.Text.Json.JsonSerializer.Serialize(
            log,
            ChurnReportsJsonContext.Default.SarifLog);
    }
}

internal sealed class SarifLog
{
    [JsonPropertyName("$schema")]
    public required string Schema { get; init; }

    public string Version { get; init; } = "";

    public List<SarifRun> Runs { get; init; } = [];
}

internal sealed class SarifRun
{
    public SarifTool Tool { get; init; } = new();

    public List<SarifResult> Results { get; init; } = [];
}

internal sealed class SarifTool
{
    public SarifToolDriver Driver { get; init; } = new();
}

internal sealed class SarifToolDriver
{
    public string Name { get; init; } = "";

    public string Version { get; init; } = "";

    public List<SarifRule> Rules { get; init; } = [];
}

internal sealed class SarifRule
{
    public string Id { get; init; } = "";

    public string Name { get; init; } = "";

    public SarifText? ShortDescription { get; init; }

    public SarifText? FullDescription { get; init; }
}

internal sealed class SarifText
{
    public string Text { get; init; } = "";
}

internal sealed class SarifResult
{
    public string RuleId { get; init; } = "";

    public string Kind { get; init; } = "";

    public string Level { get; init; } = "";

    public SarifText Message { get; init; } = new();

    public List<SarifLocation> Locations { get; init; } = [];

    public Dictionary<string, object?>? Properties { get; init; }
}

internal sealed class SarifLocation
{
    public SarifPhysicalLocation PhysicalLocation { get; init; } = new();
}

internal sealed class SarifPhysicalLocation
{
    public SarifArtifactLocation ArtifactLocation { get; init; } = new();

    public SarifRegion Region { get; init; } = new();
}

internal sealed class SarifArtifactLocation
{
    public string Uri { get; init; } = "";
}

internal sealed class SarifRegion
{
    public int StartLine { get; init; }

    public int EndLine { get; init; }
}
