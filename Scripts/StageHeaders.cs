// StageHeaders.cs
// Copies all .h files from a source directory to a destination directory,
// preprocessing math_functions.h to fix B3_LITERAL compound literals and
// inject B3_ASSERT / B3_VALIDATE macro redefinitions.
//
// Usage:
//   dotnet run StageHeaders.cs -- <srcDir> <dstDir>

using System.Text;
using System.Text.RegularExpressions;

if (args.Length < 2 || args[0] == "--help" || args[0] == "-h" || args[0] == "-?")
{
    Console.Error.WriteLine("Usage: dotnet run StageHeaders.cs -- <srcDir> <dstDir>");
    return args.Length < 2 ? 1 : 0;
}

string srcDir = args[0];
string dstDir = args[1];

Directory.CreateDirectory(dstDir);

// Copy all .h files from src to dst, preprocessing math_functions.h in place
foreach (string file in Directory.GetFiles(srcDir, "*.h"))
{
    string fileName = Path.GetFileName(file);
    string destPath = Path.Combine(dstDir, fileName);

    if (fileName == "math_functions.h")
    {
        // Read original, preprocess, write directly to destination
        string content = File.ReadAllText(file);
        string processed = Preprocessor.PreprocessContent(content);
        File.WriteAllText(destPath, processed);
    }
    else
        // Verbatim copy for all other headers
        File.Copy(file, destPath, overwrite: true);
}

Console.WriteLine($"Staged {Directory.GetFiles(srcDir, "*.h").Length} headers to {dstDir} (math_functions.h preprocessed)");
return 0;

// ----------------------------------------------------------------

static class Preprocessor
{
    // Struct field definitions
    // StructType = null means scalar (no further nesting)
    public record FieldDef(string Name, string? StructType);

    public static readonly Dictionary<string, FieldDef[]> StructDefs = new()
    {
        ["b3Vec3"] = new[] { new FieldDef("x", null), new FieldDef("y", null), new FieldDef("z", null) },
        ["b3Quat"] = new[] { new FieldDef("v", "b3Vec3"), new FieldDef("s", null) },
        ["b3Pos"] = new[] { new FieldDef("x", null), new FieldDef("y", null), new FieldDef("z", null) },
        ["b3Matrix3"] = new[] { new FieldDef("cx", "b3Vec3"), new FieldDef("cy", "b3Vec3"), new FieldDef("cz", "b3Vec3") },
    };

    public static string PreprocessContent(string content)
    {
        // Inject assertion macro redefinitions after the last #include line
        content = InjectAssertionRedefinitions(content);

        var replacements = new List<(int start, int end, string replacement)>();

        int i = 0;
        while (i < content.Length)
        {
            int matchStart = content.IndexOf("B3_LITERAL", i, StringComparison.Ordinal);
            if (matchStart < 0) break;

            // After "B3_LITERAL", expect optional whitespace then '('
            int pos = matchStart + 10;
            pos = SkipWhitespace(content, pos);
            if (pos >= content.Length || content[pos] != '(') { i = matchStart + 10; continue; }
            pos++; // past '('

            // Read type name - handle optional whitespace inside parens
            pos = SkipWhitespace(content, pos);
            int typeStart = pos;
            while (pos < content.Length && (char.IsLetterOrDigit(content[pos]) || content[pos] == '_')) pos++;
            if (pos == typeStart) { i = pos; continue; }
            string typeName = content[typeStart..pos];
            pos = SkipWhitespace(content, pos);
            if (pos >= content.Length || content[pos] != ')') { i = pos; continue; }
            pos++; // past ')'
            pos = SkipWhitespace(content, pos);
            if (pos >= content.Length || content[pos] != '{') { i = pos; continue; }

            // Find matching closing brace for the initializer list
            if (!FindMatchingBrace(content, pos, out int braceEnd))
            {
                i = pos + 1;
                continue;
            }

            // Extract the initializer list content
            int initListStart = pos + 1;
            int initListEnd = braceEnd;
            string initListContent = content[initListStart..initListEnd].Trim();

            // Determine context
            bool isReturn = IsPrecededByReturn(content, matchStart);
            string? lvalue = null;
            if (!isReturn)
            {
                lvalue = ExtractLValue(content, matchStart);
            }

            // Get the indentation of the current line
            string lineIndent = GetLineIndent(content, matchStart);

            // Parse the initializer expressions
            var expressions = SplitTopLevelExpressions(initListContent);

            // Get field definitions for this type
            if (!StructDefs.TryGetValue(typeName, out var fields))
            {
                i = braceEnd + 1;
                continue;
            }

            // Generate replacement code
            string replacement;
            if (isReturn)
            {
                replacement = GenerateReturnReplacement(typeName, fields, expressions, lineIndent);
            }
            else if (lvalue != null)
            {
                replacement = GenerateAssignmentReplacement(lvalue, fields, expressions, lineIndent);
            }
            else
            {
                i = braceEnd + 1;
                continue;
            }

            int fullMatchStart = matchStart;
            int fullMatchEnd = braceEnd + 1;

            // Extend match to start of line so replacement fully owns the line content
            int lineStart = matchStart;
            while (lineStart > 0 && content[lineStart - 1] != '\n') lineStart--;
            fullMatchStart = lineStart;

            // Consume trailing semicolon
            int afterBrace = SkipWhitespace(content, braceEnd + 1);
            if (afterBrace < content.Length && content[afterBrace] == ';')
            {
                string between = content[(braceEnd + 1)..afterBrace];
                if (!between.Contains('\n'))
                {
                    fullMatchEnd = afterBrace + 1;
                }
            }

            replacements.Add((fullMatchStart, fullMatchEnd, replacement));
            i = fullMatchEnd;
        }

        if (replacements.Count == 0) return content;

        var sb = new StringBuilder(content.Length + replacements.Count * 80);
        int lastPos = 0;
        foreach (var (start, end, repl) in replacements.OrderBy(r => r.start))
        {
            sb.Append(content.AsSpan(lastPos, start - lastPos));
            sb.Append(repl);
            lastPos = end;
        }
        sb.Append(content[lastPos..]);
        return sb.ToString();
    }

    static bool IsPrecededByReturn(string content, int matchStart)
    {
        int pos = matchStart - 1;
        while (pos >= 0 && char.IsWhiteSpace(content[pos])) pos--;
        if (pos < 0) return false;

        const string returnKw = "return";
        if (pos + 1 < returnKw.Length) return false;
        int start = pos + 1 - returnKw.Length;
        if (start >= 0 && content.Substring(start, returnKw.Length) == returnKw)
        {
            if (start == 0 || !char.IsLetterOrDigit(content[start - 1]))
                return true;
        }
        return false;
    }

    static string? ExtractLValue(string content, int matchStart)
    {
        int pos = matchStart - 1;
        while (pos >= 0 && char.IsWhiteSpace(content[pos])) pos--;
        if (pos < 0) return null;

        if (content[pos] != '=') return null;
        pos--;

        while (pos >= 0 && char.IsWhiteSpace(content[pos])) pos--;
        if (pos < 0) return null;

        int lvalueEnd = pos + 1;
        while (pos >= 0)
        {
            char c = content[pos];
            if (char.IsLetterOrDigit(c) || c == '_' || c == '.')
            {
                pos--;
            }
            else if (c == '>' && pos - 1 >= 0 && content[pos - 1] == '-')
            {
                pos -= 2; // skip '->'
            }
            else
            {
                break;
            }
        }
        int lvalueStart = pos + 1;
        string lvalue = content[lvalueStart..lvalueEnd].Trim();
        return string.IsNullOrEmpty(lvalue) ? null : lvalue;
    }

    static string GetLineIndent(string content, int pos)
    {
        // Find the start of the line containing pos
        int lineStart = pos;
        while (lineStart > 0 && content[lineStart - 1] != '\n') lineStart--;
        // Extract leading whitespace
        int indent = 0;
        while (lineStart + indent < pos && (content[lineStart + indent] == ' ' || content[lineStart + indent] == '\t'))
            indent++;
        return content.Substring(lineStart, indent);
    }

    static bool FindMatchingBrace(string content, int openBracePos, out int closeBracePos)
    {
        closeBracePos = -1;
        if (content[openBracePos] != '{') return false;

        int depth = 1;
        int i = openBracePos + 1;
        bool inString = false;

        while (i < content.Length && depth > 0)
        {
            char c = content[i];
            if (inString)
            {
                if (c == '"') inString = false;
                else if (c == '\\') i++;
            }
            else
            {
                if (c == '"') inString = true;
                else if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0) { closeBracePos = i; return true; }
                }
            }
            i++;
        }
        return false;
    }

    static int SkipWhitespace(string content, int pos)
    {
        while (pos < content.Length && (content[pos] == ' ' || content[pos] == '\t' || content[pos] == '\n' || content[pos] == '\r'))
            pos++;
        return pos;
    }

    static string[] SplitTopLevelExpressions(string text)
    {
        var results = new List<string>();
        int depth = 0;
        int parenDepth = 0;
        int start = 0;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '{') depth++;
            else if (c == '}') depth--;
            else if (c == '(') parenDepth++;
            else if (c == ')') parenDepth--;
            else if (c == ',' && depth == 0 && parenDepth == 0)
            {
                results.Add(text[start..i].Trim());
                start = i + 1;
            }
        }

        string last = text[start..].Trim();
        if (last.Length > 0)
            results.Add(last);

        return results.ToArray();
    }

    static string GenerateReturnReplacement(string typeName, FieldDef[] fields, string[] expressions, string indent)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{indent}{typeName} _b3r;");
        GenerateMemberAssignments(sb, indent, "_b3r", fields, expressions);
        sb.Append($"{indent}return _b3r;");
        return sb.ToString();
    }

    static string GenerateAssignmentReplacement(string lvalue, FieldDef[] fields, string[] expressions, string indent)
    {
        var sb = new StringBuilder();
        GenerateMemberAssignments(sb, indent, lvalue, fields, expressions);
        // Trim trailing newline from the last assignment
        if (sb.Length > 0 && sb[^1] == '\n') sb.Length--;
        return sb.ToString();
    }

    static void GenerateMemberAssignments(StringBuilder sb, string indent, string prefix, FieldDef[] fields, string[] expressions)
    {
        int count = Math.Min(fields.Length, expressions.Length);
        for (int idx = 0; idx < count; idx++)
        {
            string expr = expressions[idx].Trim();
            var field = fields[idx];

            if (field.StructType != null && StructDefs.TryGetValue(field.StructType, out var nestedFields))
            {
                // Nested struct - the expression should be a brace-enclosed initializer list
                if (expr.StartsWith('{') && expr.EndsWith('}'))
                {
                    string inner = expr[1..^1].Trim();
                    var subExprs = SplitTopLevelExpressions(inner);
                    GenerateMemberAssignments(sb, indent, $"{prefix}.{field.Name}", nestedFields, subExprs);
                }
                else
                {
                    sb.AppendLine($"{indent}{prefix}.{field.Name} = {expr};");
                }
            }
            else
            {
                sb.AppendLine($"{indent}{prefix}.{field.Name} = {expr};");
            }
        }
    }

    /// Injects empty macro redefinitions for B3_ASSERT and B3_VALIDATE
    /// after the last #include directive. This prevents ClangSharp from
    /// generating invalid C# ((void)(...)) expressions.
    static string InjectAssertionRedefinitions(string content)
    {
        // Find the last #include line
        int lastIncludeEnd = -1;
        int searchPos = 0;
        while (true)
        {
            int idx = content.IndexOf("#include", searchPos, StringComparison.Ordinal);
            if (idx < 0) break;
            // Find the end of the #include line
            int lineEnd = content.IndexOf('\n', idx);
            if (lineEnd < 0) { lastIncludeEnd = content.Length; break; }
            lastIncludeEnd = lineEnd + 1; // include the newline
            searchPos = lineEnd + 1;
        }

        if (lastIncludeEnd < 0)
        {
            // No includes found, insert at start
            lastIncludeEnd = 0;
        }

        string redefinitions =
            "\n// Assertion macro redefinitions for ClangSharp compatibility\n" +
            "#ifdef B3_ASSERT\n#undef B3_ASSERT\n#endif\n" +
            "#define B3_ASSERT(condition)\n" +
            "#ifdef B3_VALIDATE\n#undef B3_VALIDATE\n#endif\n" +
            "#define B3_VALIDATE(condition)\n\n";

        return content.Insert(lastIncludeEnd, redefinitions);
    }
}
