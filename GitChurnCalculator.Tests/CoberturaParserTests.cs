using GitChurnCalculator.Services;
using Xunit;

namespace GitChurnCalculator.Tests;

public class CoberturaParserTests
{
    [Fact]
    public void Parse_BasicCoberturaXml_ExtractsLineCoverage()
    {
        var xml = """
            <?xml version="1.0"?>
            <coverage version="1" timestamp="0" lines-valid="100" lines-covered="80" line-rate="0.8" branches-covered="0" branches-valid="0" branch-rate="0" complexity="0">
              <sources>
                <source>/home/user/project</source>
              </sources>
              <packages>
                <package name="MyApp" line-rate="0.8" branch-rate="0" complexity="0">
                  <classes>
                    <class name="UserService" filename="/home/user/project/src/UserService.cs" line-rate="0.85" branch-rate="0" complexity="0">
                      <lines>
                        <line number="1" hits="1"/>
                      </lines>
                    </class>
                    <class name="OrderService" filename="/home/user/project/src/OrderService.cs" line-rate="0.40" branch-rate="0" complexity="0">
                      <lines>
                        <line number="1" hits="1"/>
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """;

        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, xml);
            var parser = new CoberturaXmlParser();
            var result = parser.Parse(tempFile);

            Assert.Equal(2, result.Count);
            Assert.True(result.ContainsKey("src/UserService.cs"));
            Assert.True(result.ContainsKey("src/OrderService.cs"));
            Assert.Equal(85.0, result["src/UserService.cs"]);
            Assert.Equal(40.0, result["src/OrderService.cs"]);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Parse_RelativeFilenames_WorksCorrectly()
    {
        var xml = """
            <?xml version="1.0"?>
            <coverage line-rate="0.5">
              <packages>
                <package name="pkg" line-rate="0.5" branch-rate="0" complexity="0">
                  <classes>
                    <class name="Foo" filename="src/Foo.cs" line-rate="0.75" branch-rate="0" complexity="0">
                      <lines/>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """;

        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, xml);
            var parser = new CoberturaXmlParser();
            var result = parser.Parse(tempFile);

            Assert.Single(result);
            Assert.Equal(75.0, result["src/Foo.cs"]);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Parse_ClassWithManyLineElements_ExtractsLineRateViaStreaming()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            using (var writer = new StreamWriter(tempFile))
            {
                writer.WriteLine("""<?xml version="1.0"?>""");
                writer.WriteLine("""<coverage line-rate="0.5">""");
                writer.WriteLine("""  <sources><source>/repo</source></sources>""");
                writer.WriteLine("""  <packages><package name="pkg" line-rate="0.5" branch-rate="0" complexity="0">""");
                writer.WriteLine("""    <classes>""");
                writer.WriteLine("""      <class name="Foo" filename="/repo/src/Foo.cs" line-rate="0.42" branch-rate="0" complexity="0">""");
                writer.WriteLine("""        <lines>""");
                for (var i = 1; i <= 10_000; i++)
                    writer.WriteLine($"""          <line number="{i}" hits="1"/>""");
                writer.WriteLine("""        </lines>""");
                writer.WriteLine("""      </class>""");
                writer.WriteLine("""    </classes>""");
                writer.WriteLine("""  </package></packages>""");
                writer.WriteLine("""</coverage>""");
            }

            var parser = new CoberturaXmlParser();
            var result = parser.Parse(tempFile);

            Assert.Single(result);
            Assert.Equal(42.0, result["src/Foo.cs"]);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Parse_MultipleClassesSameFile_KeepsMaxCoverage()
    {
        var xml = """
            <?xml version="1.0"?>
            <coverage line-rate="0.5">
              <packages>
                <package name="pkg" line-rate="0.5" branch-rate="0" complexity="0">
                  <classes>
                    <class name="Foo" filename="src/Foo.cs" line-rate="0.60" branch-rate="0" complexity="0">
                      <lines/>
                    </class>
                    <class name="Foo.Inner" filename="src/Foo.cs" line-rate="0.90" branch-rate="0" complexity="0">
                      <lines/>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """;

        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, xml);
            var parser = new CoberturaXmlParser();
            var result = parser.Parse(tempFile);

            Assert.Single(result);
            Assert.Equal(90.0, result["src/Foo.cs"]);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void MapToGitFiles_ReportsProgressInBatches()
    {
        var coverage = Enumerable.Range(1, 250)
            .ToDictionary(i => $"src/File{i}.cs", i => (double)i, StringComparer.OrdinalIgnoreCase);
        var gitFiles = coverage.Keys.ToList();
        var reports = new List<(int Processed, int Total)>();

        CoveragePathMatcher.MapToGitFiles(coverage, gitFiles, (processed, total) => reports.Add((processed, total)));

        Assert.Equal([(100, 250), (200, 250), (250, 250)], reports);
    }

    [Fact]
    public void MapToGitFiles_ExactMatch()
    {
        var coverage = new Dictionary<string, double> { ["src/Foo.cs"] = 80.0 };
        var gitFiles = new List<string> { "src/Foo.cs", "src/Bar.cs" };

        var mapped = CoveragePathMatcher.MapToGitFiles(coverage, gitFiles);

        Assert.Single(mapped);
        Assert.Equal(80.0, mapped["src/Foo.cs"]);
    }

    [Fact]
    public void MapToGitFiles_SuffixMatch()
    {
        var coverage = new Dictionary<string, double> { ["Foo.cs"] = 70.0 };
        var gitFiles = new List<string> { "src/services/Foo.cs", "src/Bar.cs" };

        var mapped = CoveragePathMatcher.MapToGitFiles(coverage, gitFiles);

        Assert.Single(mapped);
        Assert.Equal(70.0, mapped["src/services/Foo.cs"]);
    }

    [Fact]
    public void MapToGitFiles_BackslashNormalization()
    {
        var coverage = new Dictionary<string, double> { ["src\\Foo.cs"] = 60.0 };
        var gitFiles = new List<string> { "src/Foo.cs" };

        var mapped = CoveragePathMatcher.MapToGitFiles(coverage, gitFiles);

        Assert.Single(mapped);
        Assert.Equal(60.0, mapped["src/Foo.cs"]);
    }

    [Fact]
    public void MapToGitFiles_AbsoluteCoveragePathMatchesRelativeGitPath()
    {
        var coverage = new Dictionary<string, double>
        {
            [@"C:\dev\project\Services\Domain\Foo.cs"] = 82.0,
        };
        var gitFiles = new List<string> { "Services/Domain/Foo.cs", "README.md" };

        var mapped = CoveragePathMatcher.MapToGitFiles(coverage, gitFiles);

        Assert.Single(mapped);
        Assert.Equal(82.0, mapped["Services/Domain/Foo.cs"]);
    }

    [Fact]
    public void NormalizePath_ConvertsBackslashes()
    {
        Assert.Equal("src/foo/bar.cs", CoveragePathMatcher.NormalizePath("src\\foo\\bar.cs"));
    }

    [Fact]
    public void NormalizePath_TrimsTrailingSlash()
    {
        Assert.Equal("src/foo", CoveragePathMatcher.NormalizePath("src/foo/"));
    }
}
