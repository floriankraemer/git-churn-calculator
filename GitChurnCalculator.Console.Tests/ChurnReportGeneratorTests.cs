using System.Text.Json;
using GitChurnCalculator.Console.Reporting;
using GitChurnCalculator.Models;
using Xunit;

namespace GitChurnCalculator.Console.Tests;

public class ChurnReportGeneratorTests
{
    private static FileChurnResult SampleFile(string path, double churnRisk)
    {
        return new FileChurnResult
        {
            FilePath = path,
            TotalCommits = 5,
            LinesAdded = 0,
            LinesRemoved = 0,
            FirstCommitDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            LastCommitDate = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc),
            AgeDays = 14,
            ChangesPerWeek = 2.5,
            ChangesPerMonth = 10.71,
            ChangesPerYear = 130.35,
            CommitsLast7Days = 1,
            CommitsLast30Days = 4,
            CommitsLast365Days = 5,
            TotalUniqueAuthors = 1,
            UniqueAuthorsLast7Days = 1,
            UniqueAuthorsLast30Days = 1,
            UniqueAuthorsLast365Days = 1,
            CoveragePercent = null,
            ChurnRiskScore = churnRisk,
        };
    }

    [Fact]
    public void Factory_SupportsTableFormat()
    {
        Assert.True(ChurnReportGeneratorFactory.TryGet("table", out var generator));
        Assert.IsType<SpectreConsoleTableChurnReportGenerator>(generator);
    }

    [Fact]
    public void SpectreConsoleTableChurn_ReturnsEmptyString()
    {
        var gen = new SpectreConsoleTableChurnReportGenerator();
        var text = gen.Generate(new[] { SampleFile("src/A.cs", 0.5) }, "repo");
        Assert.Equal(string.Empty, text);
    }

    [Fact]
    public void Factory_SupportsSarifGithubGitlab()
    {
        Assert.True(ChurnReportGeneratorFactory.TryGet("sarif", out _));
        Assert.True(ChurnReportGeneratorFactory.TryGet("github", out _));
        Assert.True(ChurnReportGeneratorFactory.TryGet("gitlab", out _));
    }

    [Fact]
    public void SarifChurn_ContainsSchemaVersionAndRule()
    {
        var gen = new SarifChurnReportGenerator();
        var json = gen.Generate(new[] { SampleFile("src/A.cs", 0.5) }, "repo");

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("$schema", out _));
        Assert.Equal("2.1.0", root.GetProperty("version").GetString());

        var run = root.GetProperty("runs")[0];
        Assert.Equal("GitChurnCalculator", run.GetProperty("tool").GetProperty("driver").GetProperty("name").GetString());
        Assert.Contains(
            run.GetProperty("tool").GetProperty("driver").GetProperty("rules").EnumerateArray(),
            r => r.GetProperty("id").GetString() == "churn/file-risk"
        );

        var result = run.GetProperty("results")[0];
        Assert.Equal("churn/file-risk", result.GetProperty("ruleId").GetString());
        Assert.Equal("fail", result.GetProperty("kind").GetString());
        Assert.Equal("note", result.GetProperty("level").GetString());
        Assert.Contains("src/A.cs", result.GetProperty("locations")[0].GetProperty("physicalLocation").GetProperty("artifactLocation").GetProperty("uri").GetString());
        Assert.True(result.TryGetProperty("properties", out var props));
        Assert.True(props.TryGetProperty("churnRiskScore", out _));
    }

    [Fact]
    public void SarifChurn_HighScore_UsesErrorLevel()
    {
        var gen = new SarifChurnReportGenerator();
        var json = gen.Generate(new[] { SampleFile("x.cs", 15.0) }, "r");
        using var doc = JsonDocument.Parse(json);
        var result = doc.RootElement.GetProperty("runs")[0].GetProperty("results")[0];
        Assert.Equal("error", result.GetProperty("level").GetString());
        Assert.Contains(result.GetProperty("level").GetString(), new[] { "note", "warning", "error", "none" });
    }

    [Fact]
    public void GithubActionsChurn_ContainsWorkflowCommand()
    {
        var gen = new GithubActionsChurnReportGenerator();
        var text = gen.Generate(new[] { SampleFile("src/B.cs", 5.0) }, "r");

        Assert.StartsWith("::warning file=src/B.cs,line=1,", text.Trim());
        Assert.Contains("::", text);
    }

    [Theory]
    [InlineData(0.0, "notice")]
    [InlineData(0.999, "notice")]
    [InlineData(1.0, "warning")]
    [InlineData(5.0, "warning")]
    [InlineData(9.999, "warning")]
    [InlineData(10.0, "error")]
    [InlineData(100.0, "error")]
    public void GithubActionsChurn_CommandKind_UsesScoreBands(double score, string kind)
    {
        var gen = new GithubActionsChurnReportGenerator();
        var text = gen.Generate(new[] { SampleFile("src/B.cs", score) }, "r").TrimStart();
        Assert.StartsWith($"::{kind} file=", text, StringComparison.Ordinal);
    }

    [Fact]
    public void GithubActionsChurn_BuildMessage_ContainsCoverageNaWhenAbsent()
    {
        var gen = new GithubActionsChurnReportGenerator();
        var text = gen.Generate(new[] { SampleFile("src/B.cs", 1.0) }, "r");
        Assert.Contains("coverage=n/a", text, StringComparison.Ordinal);
        Assert.Contains("+0/-0", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0.0, "note")]
    [InlineData(0.999, "note")]
    [InlineData(1.0, "warning")]
    [InlineData(9.999, "warning")]
    [InlineData(10.0, "error")]
    public void SarifChurn_Level_UsesScoreBands(double score, string expectedLevel)
    {
        var gen = new SarifChurnReportGenerator();
        var json = gen.Generate(new[] { SampleFile("band.cs", score) }, "r");
        using var doc = JsonDocument.Parse(json);
        var level = doc.RootElement.GetProperty("runs")[0].GetProperty("results")[0].GetProperty("level").GetString();
        Assert.Equal(expectedLevel, level);
    }

    [Theory]
    [InlineData(0.5, "info")]
    [InlineData(1.0, "minor")]
    [InlineData(10.0, "major")]
    public void GitlabChurn_Severity_UsesScoreBands(double score, string expectedSeverity)
    {
        var gen = new GitlabCodeQualityChurnReportGenerator();
        var json = gen.Generate(new[] { SampleFile("gl.cs", score) }, "r");
        using var doc = JsonDocument.Parse(json);
        var sev = doc.RootElement[0].GetProperty("severity").GetString();
        Assert.Equal(expectedSeverity, sev);
    }

    [Fact]
    public void GithubActionsChurn_NormalizesBackslashesInWorkflowFileParameter()
    {
        var row = SampleFile(@"src\Dept\File.cs", 5.0);
        var gen = new GithubActionsChurnReportGenerator();
        var text = gen.Generate(new[] { row }, "r");

        Assert.Contains("file=src/Dept/File.cs", text, StringComparison.Ordinal);
    }

    [Fact]
    public void SarifChurn_NormalizesBackslashInArtifactUri()
    {
        var gen = new SarifChurnReportGenerator();
        var json = gen.Generate(new[] { SampleFile(@"x\y\Z.cs", 0.5) }, "repo");
        Assert.Contains(@"""uri"": ""x/y/Z.cs""", json, StringComparison.Ordinal);
    }

    [Fact]
    public void JsonChurnReportGenerator_EmptyResults_IsEmptyArray()
    {
        var gen = new JsonChurnReportGenerator();
        var json = gen.Generate(Array.Empty<FileChurnResult>(), "sub");
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal(0, doc.RootElement.GetArrayLength());
    }

    [Fact]
    public void GitlabCodeQualityChurn_IsJsonArrayWithFingerprint()
    {
        var gen = new GitlabCodeQualityChurnReportGenerator();
        var json = gen.Generate(new[] { SampleFile("p/q.cs", 2.0) }, "r");

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        var first = doc.RootElement[0];
        Assert.Equal("issue", first.GetProperty("type").GetString());
        Assert.True(first.GetProperty("fingerprint").GetString()!.Length >= 32);
        Assert.Equal(1, first.GetProperty("location").GetProperty("lines").GetProperty("begin").GetInt32());
    }

    [Fact]
    public void TimeSeriesFactory_DoesNotIncludeCiFormats()
    {
        Assert.False(TimeSeriesReportGeneratorFactory.TryGet("sarif", out _));
        Assert.False(TimeSeriesReportGeneratorFactory.TryGet("github", out _));
        Assert.False(TimeSeriesReportGeneratorFactory.TryGet("gitlab", out _));
    }

    [Fact]
    public void TimeSeriesFactory_SupportsGraphFormat()
    {
        Assert.True(TimeSeriesReportGeneratorFactory.TryGet("graph", out var generator));
        Assert.IsType<HtmlTimeSeriesGraphReportGenerator>(generator);
    }
}
