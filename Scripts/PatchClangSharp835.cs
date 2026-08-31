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
    // Decode manually so a UTF-8 BOM remains part of the content and can be
    // written back unchanged.
    string input = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
        .GetString(File.ReadAllBytes(path));
    string output = Patcher.Patch(input);
    if (!string.Equals(input, output, StringComparison.Ordinal))
        File.WriteAllBytes(path, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(output));
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

    // NativeTypeName text varies between ClangSharp platforms and versions;
    // match only the exact target public const declarations.
    private static readonly Regex Declaration = new(
        @"(?m)^(?<indent>[ \t]*)public[ \t]+const[ \t]+(?<type>[^ \t\r\n]+)[ \t]+(?<name>B3_DEFAULT_(?:CATEGORY|MASK)_BITS)\b[ \t]*=[ \t]*(?<initializer>[^;\r\n]*?)[ \t]*;",
        RegexOptions.Compiled);

    public static string Patch(string content)
    {
        MatchCollection matches = Declaration.Matches(content);
        int categoryCount = matches.Count(match => match.Groups["name"].Value == Category);
        int maskCount = matches.Count(match => match.Groups["name"].Value == Mask);
        if (categoryCount != 1 || maskCount != 1)
            throw new InvalidOperationException($"Expected exactly one public const declaration for each target; found {Category}={categoryCount}, {Mask}={maskCount}.");

        foreach (Match match in matches)
        {
            string type = match.Groups["type"].Value;
            string initializer = match.Groups["initializer"].Value.Trim();
            bool knownUnix = type == "nuint" && initializer == "nuint.MaxValue";
            bool knownWindows = type == "ulong" && initializer == "0xffffffffffffffffUL";
            if (!knownUnix && !knownWindows)
                throw new InvalidOperationException($"Unexpected {match.Groups["name"].Value} declaration: expected nuint = nuint.MaxValue or ulong = 0xffffffffffffffffUL, found {type} = {initializer}.");
        }

        return Declaration.Replace(content, match =>
        {
            string name = match.Groups["name"].Value;
            string indent = match.Groups["indent"].Value;
            return $"{indent}public const ulong {name} = 0xffffffffffffffffUL;";
        });
    }

    public static void RunSelfTest()
    {
        const string prefix = "using System;\n";
        string unix = prefix + Target(Category, "nuint", "nuint.MaxValue", "#define B3_DEFAULT_CATEGORY_BITS UINT64_MAX") + Target(Mask, "nuint", "nuint.MaxValue", "B3_DEFAULT_MASK_BITS UINT64_MAX") + "public const nuint UNRELATED = nuint.MaxValue;\n";
        string patched = Patch(unix);
        Assert(patched.Contains("public const ulong B3_DEFAULT_CATEGORY_BITS = 0xffffffffffffffffUL;"), "Unix category patch");
        Assert(patched.Contains("public const ulong B3_DEFAULT_MASK_BITS = 0xffffffffffffffffUL;"), "Unix mask patch");
        Assert(patched.Contains("public const nuint UNRELATED = nuint.MaxValue;"), "unrelated nuint preservation");
        Assert(Patch(patched) == patched, "idempotence");

        string windows = prefix + "[NativeTypeName(\"uint64_t\")]\npublic const ulong B3_DEFAULT_CATEGORY_BITS = 0xffffffffffffffffUL;\n" +
            "[NativeTypeName(\"#define B3_DEFAULT_MASK_BITS UINT64_MAX\")]\npublic const ulong B3_DEFAULT_MASK_BITS = 0xffffffffffffffffUL;\n";
        Assert(Patch(windows) == windows, "already-correct Windows output");
        const string correctCrLf = "\uFEFF" + "using System;\r\n" +
            "[NativeTypeName(\"#define B3_DEFAULT_CATEGORY_BITS UINT64_MAX\")]\r\npublic const ulong B3_DEFAULT_CATEGORY_BITS = 0xffffffffffffffffUL;\r\n" +
            "[NativeTypeName(\"#define B3_DEFAULT_MASK_BITS UINT64_MAX\")]\r\npublic const ulong B3_DEFAULT_MASK_BITS = 0xffffffffffffffffUL;\r\n";
        Assert(Patch(correctCrLf) == correctCrLf, "already-correct CRLF with BOM");
        string badCrLf = "\uFEFFusing System;\r\n" + TargetCrLf(Category, "nuint", "nuint.MaxValue", "#define B3_DEFAULT_CATEGORY_BITS UINT64_MAX") + TargetCrLf(Mask, "nuint", "nuint.MaxValue", "B3_DEFAULT_MASK_BITS UINT64_MAX") + "public const nuint UNRELATED = nuint.MaxValue;\r\n";
        string patchedCrLf = Patch(badCrLf);
        Assert(patchedCrLf[0] == '\uFEFF', "CRLF BOM preservation");
        Assert(patchedCrLf.Contains("public const ulong B3_DEFAULT_CATEGORY_BITS = 0xffffffffffffffffUL;\r\n"), "CRLF category patch");
        Assert(patchedCrLf.Contains("public const ulong B3_DEFAULT_MASK_BITS = 0xffffffffffffffffUL;\r\n"), "CRLF mask patch");
        Assert(patchedCrLf.Contains("public const nuint UNRELATED = nuint.MaxValue;\r\n"), "CRLF unrelated line preservation");
        ExpectFailure(prefix + Target(Category, "nuint", "nuint.MaxValue", "variant"), "missing target");
        ExpectFailure(prefix + Target(Category, "nuint", "nuint.MaxValue", "variant") + Target(Category, "ulong", "0xffffffffffffffffUL", "variant") + Target(Mask, "ulong", "0xffffffffffffffffUL", "variant"), "duplicate target");
        ExpectFailure(Target(Category, "uint", "nuint.MaxValue", "variant") + Target(Mask, "nuint", "nuint.MaxValue", "variant"), "unexpected type");
        ExpectFailure(Target(Category, "nuint", "UINT64_MAX", "variant") + Target(Mask, "nuint", "nuint.MaxValue", "variant"), "unexpected initializer");
        ExpectFailure(Target(Category, "nuint", "0xffffffffffffffffUL", "variant") + Target(Mask, "ulong", "nuint.MaxValue", "variant"), "mismatched known pair");
    }

    private static string Target(string name, string type, string value, string nativeTypeName) =>
        $"[NativeTypeName(\"{nativeTypeName}\")]\npublic  const\t{type}   {name}\t= {value};\n";

    private static string TargetCrLf(string name, string type, string value, string nativeTypeName) =>
        $"[NativeTypeName(\"{nativeTypeName}\")]\r\npublic const {type} {name} = {value};\r\n";

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
