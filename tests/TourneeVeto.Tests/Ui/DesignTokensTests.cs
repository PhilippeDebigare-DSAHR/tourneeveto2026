using System.Globalization;
using System.Text.RegularExpressions;

namespace TourneeVeto.Tests.Ui;

public sealed class DesignTokensTests
{
    private static string ReadResource(string name)
    {
        using var stream = typeof(DesignTokensTests).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Ressource de test absente : {name}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static Dictionary<string, string> ReadTokens() =>
        Regex.Matches(ReadResource("Design.tokens.css"), @"(--[\w-]+)\s*:\s*([^;]+);")
            .ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value.Trim());

    [Fact]
    public void ChaquePaireSemantiqueTexteFond_RespecteAaSansArrondir()
    {
        var tokens = ReadTokens();
        var pairs = tokens.Keys
            .Where(key => key.StartsWith("--color-on-", StringComparison.Ordinal))
            .Select(key => (Text: key, Background: key.Replace("--color-on-", "--color-", StringComparison.Ordinal)))
            .ToList();

        string[] surfaces =
        [
            "--color-background", "--color-surface", "--color-surface-low",
            "--color-surface-container", "--color-surface-high", "--color-surface-highest"
        ];
        string[] foregrounds =
        [
            "--color-text", "--color-text-muted", "--color-primary", "--color-urgent",
            "--color-warning", "--color-ok", "--color-info", "--color-offline", "--color-error"
        ];
        foreach (var surface in surfaces)
        {
            foreach (var foreground in foregrounds)
            {
                pairs.Add((foreground, surface));
            }
        }

        pairs.Add(("--color-on-primary", "--color-primary-hover"));
        Assert.Equal(70, pairs.Count);
        foreach (var (text, background) in pairs)
        {
            var ratio = Contrast(tokens[text], tokens[background]);
            Assert.True(ratio >= 4.5, $"{text} / {background} : {ratio:F4}:1, minimum 4.5:1.");
        }
    }

    [Fact]
    public void ContoursInteractifsEtFocus_RespectentLeContrasteNonTextuel()
    {
        var tokens = ReadTokens();
        foreach (var surface in new[]
        {
            "--color-surface", "--color-background", "--color-surface-low",
            "--color-surface-container", "--color-surface-high", "--color-surface-highest"
        })
        {
            Assert.True(Contrast(tokens["--color-border-control"], tokens[surface]) >= 3);
            Assert.True(Contrast(tokens["--color-focus"], tokens[surface]) >= 3);
        }
    }

    [Fact]
    public void Typographie_ConserveLesMetriquesDesMaquettesEtLaLisibilite()
    {
        var tokens = ReadTokens();
        Assert.Equal("0.8125rem", tokens["--font-size-label-sm"]);
        Assert.Equal("1rem", tokens["--line-height-label-sm"]);
        Assert.Equal("1rem", tokens["--font-size-label-md"]);
        Assert.Equal("1.25rem", tokens["--line-height-label-md"]);
        Assert.Equal("1.125rem", tokens["--font-size-cow-inline"]);
        Assert.Equal("1.375rem", tokens["--line-height-cow-inline"]);
        foreach (var size in tokens.Where(token => token.Key.StartsWith("--font-size-", StringComparison.Ordinal)))
        {
            Assert.EndsWith("rem", size.Value);
            var rem = double.Parse(size.Value[..^3], CultureInfo.InvariantCulture);
            Assert.True(rem >= 0.8125, $"{size.Key} : minimum 13 px avec une racine de 16 px.");
        }
    }

    [Fact]
    public void Styles_UtilisentLesJetonsEtConserventLesCiblesTactiles()
    {
        var tokens = ReadTokens();
        var css = ReadResource("Design.CowCard.css");
        foreach (Match match in Regex.Matches(css, @"var\((--[\w-]+)\)"))
        {
            Assert.Contains(match.Groups[1].Value, tokens.Keys);
        }

        Assert.Equal("44px", tokens["--touch-target"]);
        Assert.Equal("54px", tokens["--touch-target-field"]);
        Assert.Equal("52px", tokens["--touch-target-button"]);
        Assert.DoesNotMatch(@"#[0-9a-fA-F]{3,8}\b|rgba?\(", css);
        Assert.Contains("@media (min-width: 1024px)", css);
        Assert.Contains("@media (max-width: 639px)", css);
        Assert.Contains(":focus-visible", css);
        Assert.Contains("min-width: var(--touch-target);", css);
        Assert.Contains("min-height: max(var(--touch-target), var(--touch-target-button));", css);
        Assert.Contains("min-height: max(var(--touch-target-field), var(--cow-note-min-height));", css);
        Assert.DoesNotContain("@import", ReadResource("Design.tokens.css"));
        Assert.DoesNotContain("url(", ReadResource("Design.tokens.css"));
    }

    private static double Contrast(string first, string second)
    {
        var firstLuminance = Luminance(first);
        var secondLuminance = Luminance(second);
        return (Math.Max(firstLuminance, secondLuminance) + 0.05)
            / (Math.Min(firstLuminance, secondLuminance) + 0.05);
    }

    private static double Luminance(string hex)
    {
        Assert.Matches("^#[0-9a-fA-F]{6}$", hex);
        return 0.2126 * Channel(hex.AsSpan(1, 2))
            + 0.7152 * Channel(hex.AsSpan(3, 2))
            + 0.0722 * Channel(hex.AsSpan(5, 2));
    }

    private static double Channel(ReadOnlySpan<char> hex)
    {
        var value = int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
        return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
