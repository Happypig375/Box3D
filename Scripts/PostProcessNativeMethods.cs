// PostProcessNativeMethods.cs
// Post-processes ClangSharp-generated NativeMethods.cs to fix issues
// that ClangSharp cannot handle automatically:
//   1. Remap C math functions (sqrtf -> System.MathF.Sqrt, etc.)
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
    /// 1. Remaps C math functions (sqrtf->System.MathF.Sqrt, etc.)
    /// 2. Fixes bool != 0 comparisons
    /// </summary>
    public static string PostProcessCSharp(string content)
    {
        // Step 1: Math function remapping (fully qualified to avoid missing using)
        content = Regex.Replace(content, @"\bsqrtf\s*\(", "System.MathF.Sqrt(");
        content = Regex.Replace(content, @"\bremainderf\s*\(", "System.MathF.IEEERemainder(");
        content = Regex.Replace(content, @"\bfabsf\s*\(", "System.MathF.Abs(");
        content = Regex.Replace(content, @"\bnextafterf\s*\(\s*(\w+)\s*,\s*(-?)3\.402823466e\+38F\)", match =>
        {
            var arg1 = match.Groups[1].Value;
            var arg2 = match.Groups[2].Value;
            return arg2.StartsWith("-") ? $"System.MathF.BitDecrement({arg1})" : $"System.MathF.BitIncrement({arg1})";
        });

        // Step 2: Fix bool != 0 / bool == 0
        content = FixBoolComparisons(content);

        return content;
    }

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
