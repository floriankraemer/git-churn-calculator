using System.CommandLine;
using GitChurnCalculator.Console.Cli;
using Xunit;

namespace GitChurnCalculator.Console.Tests;

[Collection("ExitCode")]
public class ChurnCliRootCommandTests
{
    [Fact]
    public async Task InvokeAsync_PassesNormalizedOptionsToApp()
    {
        var prev = Environment.ExitCode;
        Environment.ExitCode = 0;
        try
        {
            var fake = new FakeChurnCalculator();
            fake.Results.Add(TestFixtures.OneRow());
            var app = new ChurnAnalysisApp(fake);
            var root = ChurnCliRootCommand.Create(app);
            using var repo = new TempGitLikeDir();
            var outFile = Path.Combine(repo.Path, "out.json");

            var exit = await root.InvokeAsync([repo.Path, "--format", "json", "--output", outFile]);

            Assert.Equal(0, exit);
            Assert.Single(fake.Calls);
            Assert.Equal(repo.Path, fake.Calls[0].RepositoryPath);
            Assert.True(File.Exists(outFile));
            var text = await File.ReadAllTextAsync(outFile);
            Assert.Contains("src/Example.cs", text);
        }
        finally
        {
            Environment.ExitCode = prev;
        }
    }

    private sealed class TempGitLikeDir : IDisposable
    {
        public string Path { get; }

        public TempGitLikeDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gitchurn-cli-" + Guid.NewGuid());
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // best effort
            }
        }
    }
}
