// Patches the two UINT64_MAX-backed constants emitted by ClangSharp.
// Usage: dotnet run PatchClangSharp835.cs -- <NativeMethods.cs>

using System.Text;
using System.Text.RegularExpressions;

if (args.Length == 1 && (args[0] == "--help" || args[0] == "-h" || args[0] == "-?"))
{
    Console.WriteLine("Usage: dotnet run PatchClangSharp835.cs -- <NativeMethods.cs>");
    Console.WriteLine("       dotnet run PatchClangSharp835.cs -- --self-test");
    return 0;
}

if (args.Length == 1 && args[0] == "--self-test")
{
    try
    {
        Patcher.RunSelfTest();
        Console.WriteLine("PatchClangSharp835 self-test passed.");
        return 0;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"Self-test failed: {exception.Message}");
        return 1;
    }
}

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: dotnet run PatchClangSharp835.cs -- <NativeMethods.cs>");
    return 1;
}

try
{
    string path = args[0];
    string input = File.ReadAllText(path, Encoding.UTF8);
    string output = Patcher.Patch(input);
    if (!string.Equals(input, output, StringComparison.Ordinal))
        File.WriteAllText(path, output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    Console.WriteLine($"Patched ClangSharp #835 constants in {path}");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"PatchClangSharp835 failed: {exception.Message}");
    return 1;
}

static class Patcher
{
    private const string Category = "B3_DEFAULT_CATEGORY_BITS";
    private const string Mask = "B3_DEFAULT_MASK_BITS";

    private static readonly Regex Attribute = new(
        @"(?m)^(?<indent>[ \t]*)\[NativeTypeName\(""#define (?<name>B3_DEFAULT_(?:CATEGORY|MASK)_BITS) UINT64_MAX""\)\](?<newline>\r\n|\n|\r)(?<declaration>[ \t]*public[ \t]+const[ \t]+(?<type>[^ \t]+)[ \t]+\k<name>[ \t]*=[^;\r\n]*;)",
        RegexOptions.Compiled);

    public static string Patch(string content)
    {
        MatchCollection matches = Attribute.Matches(content);
        int categoryCount = matches.Count(match => match.Groups["name"].Value == Category);
        int maskCount = matches.Count(match => match.Groups["name"].Value == Mask);
        if (categoryCount != 1 || maskCount != 1)
            throw new InvalidOperationException($"Expected exactly one NativeTypeName declaration for each target; found {Category}={categoryCount}, {Mask}={maskCount}.");

        return Attribute.Replace(content, match =>
        {
            string name = match.Groups["name"].Value;
            string indent = match.Groups["indent"].Value;
            string newline = match.Groups["newline"].Value;
            return $"{indent}[NativeTypeName(\"#define {name} UINT64_MAX\")]{newline}{indent}public const ulong {name} = 0xffffffffffffffffUL;";
        });
    }

    public static void RunSelfTest()
    {
        const string prefix = "using System;\n";
        string unix = prefix + Target(Category, "nuint", "nuint.MaxValue") + Target(Mask, "nuint", "nuint.MaxValue") + "public const nuint UNRELATED = nuint.MaxValue;\n";
        string patched = Patch(unix);
        Assert(patched.Contains("public const ulong B3_DEFAULT_CATEGORY_BITS = 0xffffffffffffffffUL;"), "Unix category patch");
        Assert(patched.Contains("public const ulong B3_DEFAULT_MASK_BITS = 0xffffffffffffffffUL;"), "Unix mask patch");
        Assert(patched.Contains("public const nuint UNRELATED = nuint.MaxValue;"), "unrelated nuint preservation");
        Assert(Patch(patched) == patched, "idempotence");

        string windows = prefix + Target(Category, "ulong", "0xffffffffffffffffUL") + Target(Mask, "ulong", "0xffffffffffffffffUL");
        Assert(Patch(windows) == windows, "already-correct Windows output");
        const string correctCrLf = "\uFEFF" + "using System;\r\n" +
            "[NativeTypeName(\"#define B3_DEFAULT_CATEGORY_BITS UINT64_MAX\")]\r\npublic const ulong B3_DEFAULT_CATEGORY_BITS = 0xffffffffffffffffUL;\r\n" +
            "[NativeTypeName(\"#define B3_DEFAULT_MASK_BITS UINT64_MAX\")]\r\npublic const ulong B3_DEFAULT_MASK_BITS = 0xffffffffffffffffUL;\r\n";
        Assert(Patch(correctCrLf) == correctCrLf, "already-correct CRLF with BOM");
        string badCrLf = "using System;\r\n" + TargetCrLf(Category, "nuint", "nuint.MaxValue") + TargetCrLf(Mask, "nuint", "nuint.MaxValue") + "public const nuint UNRELATED = nuint.MaxValue;\r\n";
        string patchedCrLf = Patch(badCrLf);
        Assert(patchedCrLf.Contains("public const ulong B3_DEFAULT_CATEGORY_BITS = 0xffffffffffffffffUL;\r\n"), "CRLF category patch");
        Assert(patchedCrLf.Contains("public const ulong B3_DEFAULT_MASK_BITS = 0xffffffffffffffffUL;\r\n"), "CRLF mask patch");
        Assert(patchedCrLf.Contains("public const nuint UNRELATED = nuint.MaxValue;\r\n"), "CRLF unrelated line preservation");
        ExpectFailure(prefix + Target(Category, "nuint", "nuint.MaxValue"), "missing target");
        ExpectFailure(prefix + Target(Category, "nuint", "nuint.MaxValue") + Target(Category, "ulong", "0xffffffffffffffffUL") + Target(Mask, "ulong", "0xffffffffffffffffUL"), "duplicate target");
    }

    private static string Target(string name, string type, string value) =>
        $"[NativeTypeName(\"#define {name} UINT64_MAX\")]\npublic const {type} {name} = {value};\n";

    private static string TargetCrLf(string name, string type, string value) =>
        $"[NativeTypeName(\"#define {name} UINT64_MAX\")]\r\npublic const {type} {name} = {value};\r\n";

    private static void ExpectFailure(string input, string label)
    {
        try { Patch(input); }
        catch (InvalidOperationException) { return; }
        throw new InvalidOperationException($"Expected {label} failure.");
    }

    private static void Assert(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException($"Assertion failed: {label}");
    }
}
