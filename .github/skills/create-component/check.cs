using System.Text.RegularExpressions;

if (args.Length is < 1 or > 2 ||
    !Regex.IsMatch(args[0], @"^[A-Z][A-Za-z0-9]*$") ||
    (args.Length == 2 && args[1] != "--page"))
{
    Console.Error.WriteLine("Usage : dotnet exec <check.dll> <NomPascalCase> [--page]");
    return 2;
}

var root = new DirectoryInfo(Directory.GetCurrentDirectory());
while (root is not null && !File.Exists(Path.Combine(root.FullName, "TourneeVeto.slnx")))
{
    root = root.Parent;
}

if (root is null)
{
    Console.Error.WriteLine("Racine TournéeVéto introuvable. Exécuter depuis le projet.");
    return 2;
}

var name = args[0];
var isPage = args.Length == 2;
var folder = isPage ? "Pages" : "Components";
var ui = Path.Combine(root.FullName, "src", "TourneeVeto.Ui");
var razorPath = Path.Combine(ui, folder, $"{name}.razor");
var cssPath = $"{razorPath}.css";
var codePath = $"{razorPath}.cs";
var testsPath = Path.Combine(root.FullName, "tests", "TourneeVeto.Tests", "Ui", $"{name}Tests.cs");
var tokensPath = Path.Combine(ui, "wwwroot", "tokens.css");
var errors = new List<string>();
var content = new Dictionary<string, string>();

foreach (var path in new[] { razorPath, cssPath, testsPath, tokensPath })
{
    ReadRequired(path);
}

// Le code-behind est facultatif pour les composants existants utilisant @code.
if (File.Exists(codePath))
{
    ReadRequired(codePath);
}

foreach (var (path, text) in content)
{
    if (Regex.IsMatch(text, @"__[A-Z][A-Z0-9_]*__"))
    {
        errors.Add($"{Relative(path)} : marqueur de gabarit non remplacé.");
    }
}

if (content.TryGetValue(razorPath, out var razor))
{
    var hasRoute = Regex.IsMatch(razor, @"(?m)^\s*@page\s+""[^""]+""");
    if (hasRoute != isPage)
    {
        errors.Add($"{Relative(razorPath)} : {(isPage ? "directive @page avec route requise" : "directive @page réservée aux pages")}.");
    }
}

if (content.TryGetValue(cssPath, out var css) && content.TryGetValue(tokensPath, out var tokens))
{
    var defined = Regex.Matches(tokens, @"(--[\w-]+)\s*:")
        .Select(match => match.Groups[1].Value)
        .ToHashSet(StringComparer.Ordinal);
    var references = Regex.Matches(css, @"var\(\s*(--[\w-]+)")
        .Select(match => match.Groups[1].Value)
        .Distinct(StringComparer.Ordinal)
        .ToArray();

    if (references.Length == 0)
    {
        errors.Add($"{Relative(cssPath)} : aucune référence aux jetons visuels.");
    }

    foreach (var token in references.Where(token => !defined.Contains(token)))
    {
        errors.Add($"{Relative(cssPath)} : jeton inconnu {token}.");
    }
}

foreach (var error in errors)
{
    Console.Error.WriteLine(error);
}

if (errors.Count > 0)
{
    Console.Error.WriteLine($"Contrôle échoué : {errors.Count} erreur(s).");
    return 1;
}

Console.WriteLine($"Contrôle structurel réussi : {name} ({folder}).");
Console.WriteLine("Compilation, tests, contrastes, dimensions et hors ligne restent à vérifier.");
return 0;

string Relative(string path) => Path.GetRelativePath(root.FullName, path);

void ReadRequired(string path)
{
    if (!File.Exists(path))
    {
        errors.Add($"{Relative(path)} : fichier manquant.");
        return;
    }

    try
    {
        var text = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(text))
        {
            errors.Add($"{Relative(path)} : fichier vide.");
            return;
        }

        content.Add(path, text);
    }
    catch (IOException exception)
    {
        errors.Add($"{Relative(path)} : lecture impossible ({exception.Message}).");
    }
    catch (UnauthorizedAccessException exception)
    {
        errors.Add($"{Relative(path)} : accès refusé ({exception.Message}).");
    }
}