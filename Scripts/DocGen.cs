// Remember to update DocCoverage.cs to test for any new documentation patterns added to this script!

using System.Text;
using System.Text.RegularExpressions;

// Command-line arguments (optional):
//   args[0] = path to NativeMethods.cs (default: "../NativeMethods.cs")
//   args[1] = path to output .xml file   (default: "../Box3D.xml")
var nativeMethodsFile = args.Length > 0 ? args[0] : "../NativeMethods.cs";
var outputFile = args.Length > 1 ? args[1] : "../Box3D.xml";

var headersDir = "../box3d/include/box3d";
var outputDir = Path.GetDirectoryName(Path.GetFullPath(outputFile)) ?? ".";

var members = new Dictionary<string, List<string>>();

string EscapeXml(string text)
{
    return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}

// Fast alternative to Regex.Replace(line, @"^\s*\*\s?", "") — avoids regex overhead
static string StripLeadingAsterisk(string line)
{
    int i = 0;
    while (i < line.Length && (line[i] == ' ' || line[i] == '\t')) i++;
    if (i < line.Length && line[i] == '*')
    {
        i++;
        if (i < line.Length && (line[i] == ' ' || line[i] == '\t')) i++;
    }
    return line.Substring(i);
}

// Fast alternative to Regex.Replace(line, @"^///\s?", "")
static string StripTripleSlash(string line)
{
    // line is already trimmed, starts with "///"
    return (line.Length > 3 && line[3] == ' ') ? line.Substring(4) : line.Substring(3);
}

string CleanDoxygen(string? text)
{
    if (string.IsNullOrEmpty(text)) return "";

    // Combined deletion pass — strips grouping commands, @return, @code/@endcode, braces
    text = R.DoxygenDelete.Replace(text, "");

    // Strip trailing */ that may remain
    text = R.TrailingCommentEnd.Replace(text, "");
    text = text.Replace("/**", "").Replace("*/", "");

    // @brief prefix -> just the text
    text = R.Brief.Replace(text, "");

    // @note -> "Note:"
    text = R.Note.Replace(text, "Note: ");

    // @warning -> "Warning:"
    text = R.Warning.Replace(text, "Warning: ");

    // @see name or @see name::member -> just the name/cref
    text = R.See.Replace(text, m => m.Groups[1].Value);

    // Clean up extra whitespace
    text = R.WhitespaceCollapse.Replace(text, " ").Trim();

    return text;
}

string BuildMemberXml(string name, string? summary, List<(string name, string desc)>? parms, string? returns)
{
    var sb = new StringBuilder();
    sb.AppendLine($"  <member name=\"{EscapeXml(name)}\">");
    if (!string.IsNullOrEmpty(summary))
    {
        var cleaned = CleanDoxygen(summary);
        if (!string.IsNullOrEmpty(cleaned))
        {
            sb.AppendLine($"    <summary>{EscapeXml(cleaned)}</summary>");
        }
    }
    if (parms != null)
    {
        foreach (var p in parms)
        {
            var pn = EscapeXml(p.name);
            var pd = CleanDoxygen(p.desc);
            sb.AppendLine($"    <param name=\"{pn}\">{EscapeXml(pd)}</param>");
        }
    }
    if (!string.IsNullOrEmpty(returns))
    {
        var r = CleanDoxygen(returns);
        sb.AppendLine($"    <returns>{EscapeXml(r)}</returns>");
    }
    sb.AppendLine("  </member>");
    return sb.ToString();
}

// ---- Parsing state ----
string contextType = "";
string contextName = "";
var declBuffer = new StringBuilder(); // for multi-line function decls

string? ExtractCommentBlock(string[] lines, ref int idx)
{
    // Skip blank lines before comments
    while (idx < lines.Length && string.IsNullOrWhiteSpace(lines[idx])) idx++;
    if (idx >= lines.Length) return null;

    var line = lines[idx].Trim();

    // Multi-line /** ... */
    if (line.StartsWith("/**"))
    {
        var comment = new StringBuilder();
        // Check if */ is on the same line
        int endIdx = line.IndexOf("*/", StringComparison.Ordinal);
        if (endIdx >= 0)
        {
            var content = line.Substring(3, endIdx - 3).Trim();
            comment.Append(content);
            idx++;
        }
        else
        {
            // First line
            var first = line.Substring(3).Trim();
            if (first.Length > 0) comment.Append(first);
            idx++;
            while (idx < lines.Length)
            {
                var l = lines[idx];
                int ei = l.IndexOf("*/", StringComparison.Ordinal);
                if (ei >= 0)
                {
                    var cleaned = StripLeadingAsterisk(l.Substring(0, ei));
                    comment.Append(' ').Append(cleaned.Trim());
                    idx++;
                    break;
                }
                comment.Append(' ').Append(StripLeadingAsterisk(l).Trim());
                idx++;
            }
        }
        // Skip blank lines after comment
        while (idx < lines.Length && string.IsNullOrWhiteSpace(lines[idx])) idx++;
        var result = comment.ToString().Trim();
        // Skip Doxygen grouping commands
        if (string.IsNullOrEmpty(result) || R.DoxygenGroupEnd.IsMatch(result)) return null;
        return result;
    }

    // /// lines
    if (line.StartsWith("///"))
    {
        var comment = new StringBuilder();
        int j = idx;
        while (j < lines.Length)
        {
            var l = lines[j].Trim();
            if (l.StartsWith("///"))
            {
                var text = StripTripleSlash(l);
                // Check for @param or @return directives (stop accumulation)
                if (R.ParamDirective.IsMatch(text) || R.ReturnDirective.IsMatch(text))
                    break;
                if (comment.Length > 0) comment.Append(' ');
                comment.Append(text);
                j++;
            }
            else break;
        }
        idx = j;
        while (idx < lines.Length && string.IsNullOrWhiteSpace(lines[idx])) idx++;
        var res = comment.ToString().Trim();
        return string.IsNullOrEmpty(res) ? null : res;
    }

    return null;
}

(List<(string name, string desc)>? Params, string? Returns) ExtractParamBlock(string[] lines, ref int idx)
{
    var parms = new List<(string name, string desc)>();
    string? returns = null;
    int start = idx;

    while (start < lines.Length)
    {
        var l = lines[start].Trim();
        var m = R.ParamLine.Match(l);
        if (m.Success)
        {
            parms.Add((m.Groups[1].Value, m.Groups[2].Value.Trim()));
            start++;
            continue;
        }
        m = R.ReturnLine.Match(l);
        if (m.Success)
        {
            returns = m.Groups[1].Value.Trim();
            start++;
            continue;
        }
        // Continuation lines (/// without directive)
        m = R.CommentContinuation.Match(l);
        if (m.Success && !string.IsNullOrEmpty(m.Groups[1].Value.Trim()))
        {
            var text = m.Groups[1].Value.Trim();
            if (returns != null)
                returns += " " + text;
            else if (parms.Count > 0)
                parms[^1] = (parms[^1].name, parms[^1].desc + " " + text);
            start++;
            continue;
        }
        break;
    }

    idx = start;
    return (parms.Count > 0 ? parms : null, returns);
}

(string[] names, string? file) GetMemberName(string line, string ctxType, string ctxName)
{
    var t = line.Trim();

    // B3_API function
    var m = R.B3ApiFunc.Match(t);
    if (m.Success) return (new[] { "Box3D." + m.Groups[1].Value }, "Box3D.xml");

    // struct/union closing brace: } TypeName;
    m = R.StructClose.Match(t);
    if (m.Success) return (new[] { m.Groups[1].Value }, m.Groups[1].Value + ".xml");

    // struct field (inside struct/union body): return ALL field names (split by comma for multi-field)
    if (ctxType == "struct" || ctxType == "union")
    {
        m = R.StructField.Match(t);
        if (m.Success && !string.IsNullOrEmpty(ctxName))
        {
            var fieldNames = m.Groups[1].Value.Split(',').Select(f => f.Trim()).Where(f => f.Length > 0).ToArray();
            var qualified = fieldNames.Select(f => ctxName + "." + f).ToArray();
            if (qualified.Length > 0)
                return (qualified, ctxName + ".xml");
        }
    }

    // enum value
    if (ctxType == "enum")
    {
        m = R.EnumValEq.Match(t);
        if (m.Success && !string.IsNullOrEmpty(ctxName))
            return (new[] { ctxName + "." + m.Groups[1].Value }, ctxName + ".xml");
        m = R.EnumVal.Match(t);
        if (m.Success && !string.IsNullOrEmpty(ctxName))
            return (new[] { ctxName + "." + m.Groups[1].Value }, ctxName + ".xml");
    }

    // struct/union opening declaration: typedef struct|union Name (opening brace on next line)
    m = R.TypedefStructType.Match(t);
    if (m.Success)
    {
        var typeName = m.Groups[3].Value;
        if (!string.IsNullOrEmpty(typeName))
            return (new[] { typeName }, typeName + ".xml");
    }

    // enum opening declaration: typedef enum Name (opening brace on next line)
    m = R.TypedefEnumType.Match(t);
    if (m.Success)
    {
        var typeName = m.Groups[2].Value;
        if (!string.IsNullOrEmpty(typeName))
            return (new[] { typeName }, typeName + ".xml");
    }

    // function pointer typedef: typedef ... (*Name)...
    m = R.FnPtrTypedef.Match(t);
    if (m.Success) return (new[] { "Box3D." + m.Groups[1].Value }, "Box3D.xml");

    // function pointer typedef with direct syntax: typedef returnType Name(params)
    // e.g. typedef void* b3AllocFcn( int32_t size, int32_t alignment );
    m = R.TypedefFuncPtr.Match(t);
    if (m.Success) return (new[] { m.Groups[1].Value }, m.Groups[1].Value + ".xml");

    // standalone typedef: typedef ... Name;
    m = R.Typedef.Match(t);
    if (m.Success) return (new[] { m.Groups[1].Value }, m.Groups[1].Value + ".xml");

    return (Array.Empty<string>(), null);
}

// ---- Main parsing ----
var headerFiles = Directory.GetFiles(headersDir, "*.h").OrderBy(f => f).ToArray();
int totalMembers = 0;

foreach (var hf in headerFiles)
{
    Console.Error.WriteLine($"Parsing {Path.GetFileName(hf)}...");
    var lines = File.ReadAllLines(hf);
    int i = 0;

    while (i < lines.Length)
    {
        var line = lines[i];
        var trimmed = line.Trim();

        // Track context (inside struct/union/enum body)
        var ctxMatch = R.StructOrUnionOpen.Match(trimmed);
        if (ctxMatch.Success)
        {
            contextType = "struct";
            contextName = ctxMatch.Groups[3].Value;
            i++;
            continue;
        }
        // Also handle typedef struct|union Name when { is on the next line
        // Always matches (no guard condition) because a forward declaration like
        // "typedef struct b3DebugShape b3DebugShape;" sets context earlier and the
        // guard would prevent the real definition from overriding it.
        ctxMatch = R.TypedefStructType.Match(trimmed);
        if (ctxMatch.Success)
        {
            var typeName = ctxMatch.Groups[3].Value;
            if (!string.IsNullOrEmpty(typeName))
            {
                contextType = "struct";
                contextName = typeName;
                i++;
                continue;
            }
        }
        // Also handle typedef enum Name when { is on the next line
        ctxMatch = R.TypedefEnumType.Match(trimmed);
        if (ctxMatch.Success)
        {
            var typeName = ctxMatch.Groups[2].Value;
            if (!string.IsNullOrEmpty(typeName))
            {
                contextType = "enum";
                contextName = typeName;
                i++;
                continue;
            }
        }
        ctxMatch = R.EnumOpen.Match(trimmed);
        if (ctxMatch.Success)
        {
            contextType = "enum";
            contextName = ctxMatch.Groups[1].Value;
            i++;
            continue;
        }
        if (trimmed is "};" or "}" || R.StructClose.IsMatch(trimmed))
        {
            contextType = "";
            contextName = "";
            i++;
            continue;
        }

        int saveI = i;
        var summary = ExtractCommentBlock(lines, ref i);

        if (summary != null)
        {
            // Special case: if the comment is purely a Doxygen grouping command, skip it
            if (R.DefgroupPrefix.IsMatch(summary) || R.AddtogroupPrefix.IsMatch(summary))
            {
                // Skip - no code follows these grouping comments
                continue;
            }

            var paramBlock = ExtractParamBlock(lines, ref i);

            // Find the declaration line
            int j = i;
            while (j < lines.Length && string.IsNullOrWhiteSpace(lines[j])) j++;

            string declLine = "";
            if (j < lines.Length)
            {
                declLine = lines[j];
                // Multi-line B3_API function: keep reading until ); 
                if (declLine.Trim().StartsWith("B3_API"))
                {
                    var fullDecl = new StringBuilder();
                    int k = j;
                    while (k < lines.Length)
                    {
                        fullDecl.Append(' ').Append(lines[k].Trim());
                        if (lines[k].Trim().EndsWith(";")) break;
                        k++;
                    }
                    declLine = fullDecl.ToString().Trim();
                }
            }

            if (!string.IsNullOrEmpty(declLine))
            {
                var (memberNames, xmlFile) = GetMemberName(declLine, contextType, contextName);
                if (memberNames.Length > 0 && xmlFile != null)
                {
                    foreach (var mname in memberNames)
                    {
                        var xml = BuildMemberXml(mname, summary, paramBlock.Params, paramBlock.Returns);
                        if (!members.ContainsKey(xmlFile)) members[xmlFile] = new List<string>();
                        members[xmlFile].Add(xml);
                        totalMembers++;
                    }
                }
            }
        }
        else
        {
            // Inside a struct/union, extract inline // comments as field documentation
            if ((contextType == "struct" || contextType == "union") && !string.IsNullOrEmpty(contextName))
            {
                var fiMatch = R.StructField.Match(trimmed);
                if (fiMatch.Success)
                {
                    // Check for inline ///< (Doxygen) or // comment on the original line
                    int commentIdx = line.IndexOf("///<");
                    int skipLen = 4; // "///<"
                    if (commentIdx < 0)
                    {
                        commentIdx = line.IndexOf("//");
                        skipLen = 2; // "//"
                    }
                    if (commentIdx >= 0)
                    {
                        var inlineComment = line.Substring(commentIdx + skipLen).Trim();

                        var fieldNames = fiMatch.Groups[1].Value.Split(',')
                            .Select(f => f.Trim()).Where(f => f.Length > 0).ToArray();
                        foreach (var fname in fieldNames)
                        {
                            var mname = contextName + "." + fname;
                            var xml = BuildMemberXml(mname, inlineComment, null, null);
                            var xmlFile = contextName + ".xml";
                            if (!members.ContainsKey(xmlFile)) members[xmlFile] = new List<string>();
                            members[xmlFile].Add(xml);
                            totalMembers++;
                        }
                    }
                }
            }
            i = saveI + 1;
        }
    }
}

// ---- Ensure all members referenced in NativeMethods.cs <include> elements have entries ----
// ClangSharp generates <include> for every declaration, even undocumented ones.
// Without a matching <member>, the C# compiler emits "cannot be included" warnings.
{
    var existingMemberNames = new HashSet<string>();
    foreach (var kv in members)
    {
        foreach (var memberXml in kv.Value)
        {
            var m = Regex.Match(memberXml, @"name=""([^""]+)""");
            if (m.Success) existingMemberNames.Add(m.Groups[1].Value);
        }
    }

    if (File.Exists(nativeMethodsFile))
    {
        var nmContent = File.ReadAllText(nativeMethodsFile);
        var includeRegex = new Regex(
            @"<include\s+file='[^']*\.xml'\s+path='doc/member\[@name=""([^""]+)""\]/\*' />",
            RegexOptions.Compiled);
        int addedMembers = 0;

        foreach (Match match in includeRegex.Matches(nmContent))
        {
            var memberName = match.Groups[1].Value;
            if (!existingMemberNames.Contains(memberName))
            {
                var xml = BuildMemberXml(memberName, null, null, null);
                string xmlFile = Path.GetFileName(outputFile);
                if (!members.ContainsKey(xmlFile)) members[xmlFile] = new List<string>();
                members[xmlFile].Add(xml);
                existingMemberNames.Add(memberName);
                addedMembers++;
            }
        }

        if (addedMembers > 0)
        {
            Console.WriteLine($"Added {addedMembers} undocumented members from NativeMethods.cs includes");
            totalMembers += addedMembers;
        }
    }
}

// ---- Write single output XML file with all members ----
var outputPath = Path.Combine(outputDir, outputFile);
var allXml = new StringBuilder();
allXml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
allXml.AppendLine("<doc>");
foreach (var kv in members.OrderBy(m => m.Key))
{
    foreach (var memberXml in kv.Value)
    {
        allXml.AppendLine(memberXml);
    }
}
allXml.AppendLine("</doc>");
File.WriteAllText(outputPath, allXml.ToString());
Console.WriteLine($"Wrote {outputPath} ({totalMembers} members)");

// ---- Patch include directives in NativeMethods.cs ----
if (File.Exists(nativeMethodsFile))
{
    var nmContent = File.ReadAllText(nativeMethodsFile);
    // Replace <include file='*.xml' with <include file='Box3D.xml'
    nmContent = Regex.Replace(nmContent,
        @"<include\s+file='[^']*\.xml'",
        $"<include file='{outputFile}'");
    File.WriteAllText(nativeMethodsFile, nmContent);
    Console.WriteLine($"Patched include directives in {nativeMethodsFile} to point to {outputFile}");
}
else
{
    Console.Error.WriteLine($"Warning: {nativeMethodsFile} not found, skipping include patching");
}

// Pre-compiled regexes for performance — constructed once, used repeatedly
static class R
{
    // CleanDoxygen — deletion patterns (all combined into one pass)
    internal static readonly Regex DoxygenDelete = new(
        @"@(?:defgroup\s+\S+(?:\s+.*?)?(?:@\{)?|addtogroup\s+\S+(?:\s+.*?)?|ingroup\s+\S+|\{|\}|returns?\s*|code\s*(?:\{\.?\w+\})?|endcode)",
        RegexOptions.Compiled);
    internal static readonly Regex Brief = new(@"@brief\s+", RegexOptions.Compiled);
    internal static readonly Regex Note = new(@"@note\s+", RegexOptions.Compiled);
    internal static readonly Regex Warning = new(@"@warning\s+", RegexOptions.Compiled);
    internal static readonly Regex See = new(@"@see\s+(\S+(?:::\S+)?)", RegexOptions.Compiled);
    internal static readonly Regex TrailingCommentEnd = new(@"\*/\s*$", RegexOptions.Compiled);
    internal static readonly Regex WhitespaceCollapse = new(@"\s+", RegexOptions.Compiled);

    // ExtractCommentBlock
    internal static readonly Regex DoxygenGroupEnd = new(@"^@\}\s*/?\s*$", RegexOptions.Compiled);
    internal static readonly Regex ParamDirective = new(@"^@param\s+\S+", RegexOptions.Compiled);
    internal static readonly Regex ReturnDirective = new(@"^@return\s*", RegexOptions.Compiled);

    // Main parsing loop (context tracking & grouping comments)
    internal static readonly Regex StructOrUnionOpen = new(@"^(typedef\s+)?(struct|union)\s+(\w+)?\s*\{", RegexOptions.Compiled);
    internal static readonly Regex EnumOpen = new(@"^enum\s+(\w+)?\s*\{", RegexOptions.Compiled);
    internal static readonly Regex DefgroupPrefix = new(@"^@defgroup\s", RegexOptions.Compiled);
    internal static readonly Regex AddtogroupPrefix = new(@"^@addtogroup\s", RegexOptions.Compiled);

    // ExtractParamBlock
    internal static readonly Regex ParamLine = new(@"^///\s*@param\s+(\S+)\s*(.*)", RegexOptions.Compiled);
    internal static readonly Regex ReturnLine = new(@"^///\s*@returns?\s*(.*)", RegexOptions.Compiled);
    internal static readonly Regex CommentContinuation = new(@"^///\s*(.*)", RegexOptions.Compiled);

    // GetMemberName
    internal static readonly Regex B3ApiFunc = new(@"^B3_API\s+.*?\b(\w+)\s*\(", RegexOptions.Compiled);
    internal static readonly Regex StructField = new(@"^.*?(\w+(?:\s*,\s*\w+)*)\s*;", RegexOptions.Compiled);
    internal static readonly Regex StructClose = new(@"^}\s*(\w+)\s*;", RegexOptions.Compiled);
    internal static readonly Regex EnumValEq = new(@"^(\w+)\s*=\s*\d+", RegexOptions.Compiled);
    internal static readonly Regex EnumVal = new(@"^(\w+)\s*,?", RegexOptions.Compiled);
    internal static readonly Regex FnPtrTypedef = new(@"typedef\s+.*?\(\*\s*(\w+)\)", RegexOptions.Compiled);
    internal static readonly Regex TypedefFuncPtr = new(@"^typedef\s+(?!struct|union)[^;]*?\b(\w+)\s*\(", RegexOptions.Compiled);
    internal static readonly Regex TypedefStructType = new(@"^(typedef\s+)?(struct|union)\s+(\w+)", RegexOptions.Compiled);
    internal static readonly Regex TypedefEnumType = new(@"^(typedef\s+)?enum\s+(\w+)", RegexOptions.Compiled);
    internal static readonly Regex Typedef = new(@"^typedef\s+.*?\s+(\w+)\s*;", RegexOptions.Compiled);
}
