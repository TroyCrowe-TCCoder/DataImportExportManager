namespace DataImportExportManager.Tests;

using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using DataImportExportManager.Importers;

/// <summary>
/// Keeps CONFIGURATION.md in step with the options classes. A library may carry defaults only when they can be
/// found without reading code, so a listed default that differs from the code fails the build.
/// </summary>
public sealed class ConfigurationReferenceTests
{
    private const string ReferenceFileName = "CONFIGURATION.md";
    private const string SolutionFileName = "DataImportExportManager.sln";
    private const string ProjectFileName = "DataImportExportManager.csproj";
    private const string NoValue = "none";
    private const string Yes = "Yes";
    private const string No = "No";
    private const string Conditional = "Conditional";

    private static readonly Regex SecretName = new("ConnectionString|Salt|Secret|Password|Token|ApiKey|AccessKey", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex SecretValue = new(@"(Password|Pwd|AccountKey|SharedAccessKey|Server|Data Source)\s*=", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly string[] RequiredValues = [Yes, No, Conditional];

    private static readonly string[] SecretValues = [Yes, No];

    private sealed record OptionRow(string Default, string Required, string Secret);

    public static TheoryData<string> OptionsTypeNames()
    {
        var typeNames = new TheoryData<string>();
        foreach (var type in FindOptionsTypes())
        {
            typeNames.Add(type.Name);
        }

        return typeNames;
    }

    [Fact]
    public void WhenOptionsTypesAreDiscoveredThenAllElevenAreFound()
    {
        Assert.Equal(11, FindOptionsTypes().Count);
    }

    [Theory]
    [MemberData(nameof(OptionsTypeNames))]
    public void WhenReferenceDiffersFromCodeThenOptionNamesMismatchIsReported(string typeName)
    {
        var type = FindOptionsType(typeName);
        var documented = ReadSection(type);

        var inCode = ReadProperties(type).Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
        var problems = new List<string>();
        problems.AddRange(inCode.Except(documented.Keys).Select(name => $"{typeName}.{name} is in the code but not in {ReferenceFileName}."));
        problems.AddRange(documented.Keys.Except(inCode).Select(name => $"{typeName}.{name} is in {ReferenceFileName} but not in the code."));

        Assert.Empty(problems);
    }

    [Theory]
    [MemberData(nameof(OptionsTypeNames))]
    public void WhenDefaultDiffersFromCodeThenDefaultMismatchIsReported(string typeName)
    {
        var type = FindOptionsType(typeName);
        var documented = ReadSection(type);
        var instance = Activator.CreateInstance(type)!;

        var problems = new List<string>();
        foreach (var property in ReadProperties(type))
        {
            if (!documented.TryGetValue(property.Name, out var row))
            {
                continue;
            }

            var actual = FormatDefault(property.GetValue(instance));
            if (!string.Equals(row.Default, actual, StringComparison.Ordinal))
            {
                problems.Add($"{typeName}.{property.Name}: {ReferenceFileName} says '{row.Default}' but the code default is '{actual}'.");
            }
        }

        Assert.Empty(problems);
    }

    [Theory]
    [MemberData(nameof(OptionsTypeNames))]
    public void WhenRequiredOrSecretMarkingIsInvalidThenEveryProblemIsReported(string typeName)
    {
        var type = FindOptionsType(typeName);
        var documented = ReadSection(type);

        var problems = new List<string>();
        foreach (var (name, row) in documented)
        {
            if (!RequiredValues.Contains(row.Required))
            {
                problems.Add($"{typeName}.{name}: Required must be Yes, No or Conditional.");
            }
            else if (row.Required != No && row.Default != NoValue)
            {
                problems.Add($"{typeName}.{name}: a required option cannot have a default.");
            }

            if (!SecretValues.Contains(row.Secret))
            {
                problems.Add($"{typeName}.{name}: Secret must be Yes or No.");
            }
            else if (SecretName.IsMatch(name) && row.Secret != Yes)
            {
                problems.Add($"{typeName}.{name}: the name suggests a secret, so Secret must be Yes.");
            }
        }

        Assert.Empty(problems);
    }

    [Fact]
    public void WhenProjectIsPackedThenItIncludesTheReference()
    {
        var project = File.ReadAllText(Path.Combine(RepositoryRoot(), ProjectFileName));

        Assert.Contains(ReferenceFileName, project, StringComparison.Ordinal);
    }

    [Fact]
    public void WhenReferenceContainsCredentialTextThenItIsReported()
    {
        var text = File.ReadAllText(ReferencePath());

        var match = SecretValue.Match(text);

        Assert.False(match.Success, $"{ReferenceFileName} contains '{match.Value}', which looks like a connection string or credential.");
    }

    private static List<Type> FindOptionsTypes() => typeof(CsvImporterOptions).Assembly
        .GetExportedTypes()
        .Where(type => type.IsClass && type.Name.EndsWith("Options", StringComparison.Ordinal))
        .ToList();

    private static Type FindOptionsType(string typeName) => FindOptionsTypes().Single(type => type.Name == typeName);

    private static List<PropertyInfo> ReadProperties(Type type) => type
        .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
        .Where(property => property.GetMethod is not null)
        .ToList();

    private static string FormatDefault(object? value) => value switch
    {
        null => NoValue,
        string text => text.Length == 0 ? NoValue : text,
        bool flag => flag ? "true" : "false",
        Encoding encoding => encoding.WebName,
        IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? NoValue,
    };

    private static Dictionary<string, OptionRow> ReadSection(Type type)
    {
        var path = ReferencePath();
        Assert.True(File.Exists(path), $"{path} does not exist.");

        var heading = $"## `{type.Name}`";
        var rows = new Dictionary<string, OptionRow>(StringComparer.Ordinal);
        var inSection = false;
        var found = false;

        foreach (var line in File.ReadAllLines(path))
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                inSection = string.Equals(line.Trim(), heading, StringComparison.Ordinal);
                found |= inSection;
                continue;
            }

            if (!inSection || !line.StartsWith('|'))
            {
                continue;
            }

            var cells = line.Trim().Trim('|').Split('|').Select(cell => cell.Trim()).ToArray();
            if (cells[0] == "Option" || cells[0].StartsWith("---", StringComparison.Ordinal))
            {
                continue;
            }

            Assert.True(cells.Length == 5, $"{ReferenceFileName} row '{line}' must have 5 columns: Option, Default, Required, Secret, Meaning.");
            rows[cells[0].Trim('`')] = new OptionRow(cells[1].Trim('`'), cells[2], cells[3]);
        }

        Assert.True(found, $"{path} has no section {heading}.");
        return rows;
    }

    private static string ReferencePath() => Path.Combine(RepositoryRoot(), ReferenceFileName);

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, SolutionFileName)))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException($"{SolutionFileName} was not found above the test output folder.");
    }
}
