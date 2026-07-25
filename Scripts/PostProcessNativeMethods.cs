// PostProcessNativeMethods.cs
// Post-processes ClangSharp-generated NativeMethods.cs to fix issues
// that ClangSharp cannot handle automatically:
//   1. Convert static readonly fields that invoke a function (C macros
//      expanded with function calls) to expression-bodied properties
//   2. Fix (boolExpr) != 0 patterns that are invalid in C#
//
// Usage:
//   dotnet run PostProcessNativeMethods.cs -- <input>
//   Modifies the file in place.

using System.Text;
using System.Text.RegularExpressions;

if (args.Length < 1 || args[0] == "--help" || args[0] == "-h" || args[0] == "-?")
{
    Console.Error.WriteLine("Usage: dotnet run PostProcessNativeMethods.cs -- <input>");
    return args.Length < 1 ? 1 : 0;
}

string filePath = args[0];
string content = File.ReadAllText(filePath);
string result = PostProcessor.PostProcessCSharp(content);
File.WriteAllText(filePath, result);
Console.WriteLine($"Post-processed {filePath}");
return 0;

// ----------------------------------------------------------------

static class PostProcessor
{
    /// <summary>
    /// Post-processes ClangSharp-generated C# code:
    /// 1. Converts static readonly fields that invoke a function to properties
    /// 2. Fixes bool != 0 comparisons
    /// </summary>
    public static string PostProcessCSharp(string content)
    {
        // Step 1: Convert non-constant readonly fields that invoke a function to properties.
        // C macros that expand to expressions with function calls (e.g.
        // #define B3_HUGE (1.0e5f * b3GetLengthUnitsPerMeter())) are generated as
        // "public static readonly float B3_HUGE = ...;" by ClangSharp, but the
        // function must be called at each access site, not once at static init.
        content = ConvertReadonlyFieldsToProperties(content);

        // Step 2: Fix bool != 0 / bool == 0
        content = FixBoolComparisons(content);

        return content;
    }

    /// <summary>
    /// Finds single-line "public static readonly T name = expr;" where expr contains a
    /// method call and converts to "public static T name => expr;".
    /// Struct-initializer fields (new T { ... }) and pure-constant fields are left alone.
    /// </summary>
    static string ConvertReadonlyFieldsToProperties(string content)
    {
        // Match: public static readonly <type> <name> = <value>;
        // The type can contain generic markers like delegate*<...>, so be careful.
        var fieldRegex = new Regex(
            @"public static readonly\s+(?<typeAndName>[^=]+?)\s*=\s*(?<value>[^;]+?)\s*;",
            RegexOptions.Compiled);

        var sb = new StringBuilder(content);
        int offset = 0;

        foreach (Match match in fieldRegex.Matches(content))
        {
            string typeAndName = match.Groups["typeAndName"].Value;
            string value = match.Groups["value"].Value;

            if (!ContainsFunctionCall(value))
                continue;

            // typeAndName is "<type> <name>", split on the last space
            int lastSpace = typeAndName.LastIndexOf(' ');
            if (lastSpace < 0)
                continue;

            string type = typeAndName.AsSpan(0, lastSpace).Trim().ToString();
            string name = typeAndName.AsSpan(lastSpace + 1).Trim().ToString();

            if (type.Length == 0 || name.Length == 0)
                continue;

            int idx = match.Index + offset;
            string replacement = $"public static {type} {name} => {value};";
            sb.Remove(idx, match.Length);
            sb.Insert(idx, replacement);
            offset += replacement.Length - match.Length;
        }

        return sb.ToString();
    }

    /// <summary>
    /// Returns true when <paramref name="expr"/> contains an identifier followed by '('
    /// that looks like a method/function call (excluding language keywords such as
    /// new/typeof/sizeof/default/nameof).
    /// </summary>
    static bool ContainsFunctionCall(string expr)
    {
        // Look for identifier( patterns, but exclude C# keywords
        var callRegex = new Regex(@"(?<![a-zA-Z_])[a-zA-Z_][a-zA-Z0-9_]*\s*\(", RegexOptions.Compiled);

        foreach (Match m in callRegex.Matches(expr))
        {
            // Extract just the identifier (trim trailing spaces and '(')
            string ident = m.Value;
            int parenIdx = ident.IndexOf('(');
            if (parenIdx >= 0)
                ident = ident.AsSpan(0, parenIdx).Trim().ToString();

            if (!IsKeyword(ident))
                return true;
        }

        return false;
    }

    static bool IsKeyword(string word) => word switch
    {
        "new" or "typeof" or "sizeof" or "default" or "nameof"
        or "checked" or "unchecked" or "true" or "false" => true,
        _ => false,
    };

    static string FixBoolComparisons(string content)
    {
        var sb = new StringBuilder(content);
        const string pattern = ") != 0";
        int searchStart = 0;

        while (true)
        {
            int closeParenIdx = sb.ToString().IndexOf(pattern, searchStart, StringComparison.Ordinal);
            if (closeParenIdx < 0)
                break;

            int depth = 1;
            int openParenIdx = closeParenIdx;
            while (depth > 0 && openParenIdx > 0)
            {
                openParenIdx--;
                if (sb[openParenIdx] == ')')
                    depth++;
                else if (sb[openParenIdx] == '(')
                    depth--;
            }

            if (depth != 0)
            {
                searchStart = closeParenIdx + 1;
                continue;
            }

            int innerStart = openParenIdx + 1;
            int innerLen = closeParenIdx - openParenIdx - 1;
            string inner = sb.ToString(innerStart, innerLen).Trim();

            if (inner == "0" || inner == "1")
            {
                bool val = inner == "1";
                int start = openParenIdx;
                int len = closeParenIdx - openParenIdx + pattern.Length;
                sb.Remove(start, len);
                sb.Insert(start, val ? "true" : "false");
                searchStart = start + (val ? 4 : 5);
                continue;
            }

            bool hasBoolOps = Regex.IsMatch(inner, @"[<>=!&|]");
            if (hasBoolOps)
            {
                int start = closeParenIdx + 1;
                int len = pattern.Length - 1;
                sb.Remove(start, len);
                searchStart = start;
            }
            else
            {
                searchStart = closeParenIdx + 1;
            }
        }

        return sb.ToString();
    }
}
