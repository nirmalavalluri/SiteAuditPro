using AuditVitals.Application.Parsing;

namespace AuditVitals.Application.Tests.Parsing;

public class HtmlPageAnalyzerTests
{
    [Fact]
    public void Analyze_ExtractsTitleMetaDescriptionAndH1Count()
    {
        const string html = """
            <html>
              <head>
                <title>  Example Page  </title>
                <meta name="Description" content="A short description.">
              </head>
              <body>
                <h1>Welcome</h1>
                <h1>Second heading</h1>
                <p>Some body text here.</p>
              </body>
            </html>
            """;

        var summary = HtmlPageAnalyzer.Analyze(html);

        Assert.Equal("Example Page", summary.Title);
        Assert.Equal("A short description.", summary.MetaDescription);
        Assert.Equal(2, summary.H1Count);
        Assert.True(summary.WordCount > 0);
    }

    [Fact]
    public void Analyze_MissingTitleAndDescription_ReturnsNulls()
    {
        var summary = HtmlPageAnalyzer.Analyze("<html><body><p>No head metadata.</p></body></html>");

        Assert.Null(summary.Title);
        Assert.Null(summary.MetaDescription);
        Assert.Equal(0, summary.H1Count);
    }

    [Fact]
    public void Analyze_CanonicalLink_IsExtracted()
    {
        const string html = """<html><head><link rel="canonical" href="https://example.com/canonical"></head><body></body></html>""";

        var summary = HtmlPageAnalyzer.Analyze(html);

        Assert.Equal("https://example.com/canonical", summary.CanonicalUrl);
    }

    [Fact]
    public void Analyze_NoindexRobotsMeta_SetsIsNoIndexTrue()
    {
        const string html = """<html><head><meta name="robots" content="noindex, nofollow"></head><body></body></html>""";

        var summary = HtmlPageAnalyzer.Analyze(html);

        Assert.True(summary.IsNoIndex);
    }

    [Fact]
    public void Analyze_NoRobotsMeta_IsNoIndexFalse()
    {
        var summary = HtmlPageAnalyzer.Analyze("<html><body></body></html>");

        Assert.False(summary.IsNoIndex);
    }
}
