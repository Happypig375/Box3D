using System.Text.RegularExpressions;

#if LARGE_WORLDS
var xmlFile = "../Box3D.LargeWorlds/Box3D.xml";
#else
var xmlFile = "../Box3D.xml";
#endif
var headersDir = "../box3d/include/box3d";

var xml = File.ReadAllText(xmlFile);

// Extract all member names and their summary text
var memberRegex = new Regex(@"<member\s+name=""([^""]+)""(?:/>|>(.*?)</member>)", RegexOptions.Singleline);
var summaryCleaner = new Regex(@"<summary>(.*?)</summary>", RegexOptions.Singleline);
var memberSummaries = new Dictionary<string, string>();
foreach (Match m in memberRegex.Matches(xml))
{
    var name = m.Groups[1].Value;
    var innerXml = m.Groups[2].Value.Trim();
    // Extract clean summary text without XML tags
    var sm = summaryCleaner.Match(innerXml);
    var summary = sm.Success ? sm.Groups[1].Value.Trim() : innerXml;
    memberSummaries[name] = summary;
}

var allMembers = memberSummaries.Keys.ToHashSet();

// Load header files for source reference
var headerFiles = Directory.GetFiles(headersDir, "*.h").OrderBy(f => f).ToArray();
var headerText = string.Join("\n", headerFiles.Select(f => File.ReadAllText(f)));

// ---- Define test patterns ----
var results = new List<(string category, string description, bool pass, string detail)>();

void Check(string category, string description, Func<bool> test, string failDetail)
{
    var pass = test();
    results.Add((category, description, pass, pass ? "OK" : failDetail));
}

// ============================================================
// 1. STRUCT TYPE documentation
// ============================================================
Check("StructType", "b3Capacity struct is documented",
    () => allMembers.Contains("b3Capacity"),
    "Missing b3Capacity member");

Check("StructType", "b3WorldDef struct is documented",
    () => allMembers.Contains("b3WorldDef"),
    "Missing b3WorldDef member");

Check("StructType", "b3DebugShape struct is documented",
    () => allMembers.Contains("b3DebugShape"),
    "Missing b3DebugShape member");

// ============================================================
// 2. STRUCT FIELD with /// comments above
// ============================================================
Check("StructField_Summary", "b3Capacity.staticShapeCount is documented",
    () => allMembers.Contains("b3Capacity.staticShapeCount"),
    "Missing b3Capacity.staticShapeCount");

Check("StructField_Summary", "b3Capacity.dynamicShapeCount is documented",
    () => allMembers.Contains("b3Capacity.dynamicShapeCount"),
    "Missing b3Capacity.dynamicShapeCount");

Check("StructField_Summary", "b3JointDef.internalValue is documented",
    () => allMembers.Contains("b3JointDef.internalValue"),
    "Missing b3JointDef.internalValue");

// ============================================================
// 3. STRUCT FIELD with inline // comment
// ============================================================
Check("StructField_InlineSlash", "b3RecQueryInfo.aabb has inline // comment as summary",
    () => memberSummaries.GetValueOrDefault("b3RecQueryInfo.aabb") == "world-space bounds of the query, swept for casts",
    $"Summary is: {memberSummaries.GetValueOrDefault("b3RecQueryInfo.aabb")}");

Check("StructField_InlineSlash", "b3RecQueryInfo.origin has inline // comment as summary",
    () => memberSummaries.GetValueOrDefault("b3RecQueryInfo.origin") == "query origin (zero for overlap AABB)",
    $"Summary is: {memberSummaries.GetValueOrDefault("b3RecQueryInfo.origin")}");

Check("StructField_InlineSlash", "b3RecQueryInfo.translation has inline // comment as summary",
    () => memberSummaries.GetValueOrDefault("b3RecQueryInfo.translation") == "ray and cast translation",
    $"Summary is: {memberSummaries.GetValueOrDefault("b3RecQueryInfo.translation")}");

Check("StructField_InlineSlash", "b3RecQueryInfo.hitCount has inline // comment as summary",
    () => memberSummaries.GetValueOrDefault("b3RecQueryInfo.hitCount") == "number of recorded results",
    $"Summary is: {memberSummaries.GetValueOrDefault("b3RecQueryInfo.hitCount")}");

Check("StructField_InlineSlash", "b3RecQueryInfo.key has inline // comment as summary",
    () => memberSummaries.GetValueOrDefault("b3RecQueryInfo.key") == "identity key, the hash of (id, name), 0 if untagged",
    $"Summary is: {memberSummaries.GetValueOrDefault("b3RecQueryInfo.key")}");

Check("StructField_InlineSlash", "b3RecQueryInfo.id has inline // comment as summary",
    () => memberSummaries.GetValueOrDefault("b3RecQueryInfo.id") == "query id, 0 if none",
    $"Summary is: {memberSummaries.GetValueOrDefault("b3RecQueryInfo.id")}");

Check("StructField_InlineSlash", "b3RecQueryInfo.name has inline // comment as summary",
    () => memberSummaries.GetValueOrDefault("b3RecQueryInfo.name") == "query label, NULL if none",
    $"Summary is: {memberSummaries.GetValueOrDefault("b3RecQueryInfo.name")}");

// ============================================================
// 4. STRUCT FIELD with inline ///< comment
// ============================================================
Check("StructField_InlineDoxygen", "b3ShapeCastPairInput.proxyA has clean ///< summary",
    () => memberSummaries.GetValueOrDefault("b3ShapeCastPairInput.proxyA") == "The proxy for shape A",
    $"Summary is: {memberSummaries.GetValueOrDefault("b3ShapeCastPairInput.proxyA")}");

Check("StructField_InlineDoxygen", "b3ShapeCastPairInput.proxyB has clean ///< summary",
    () => memberSummaries.GetValueOrDefault("b3ShapeCastPairInput.proxyB") == "The proxy for shape B",
    $"Summary is: {memberSummaries.GetValueOrDefault("b3ShapeCastPairInput.proxyB")}");

Check("StructField_InlineDoxygen", "b3ShapeCastPairInput.transform has clean ///< summary",
    () => memberSummaries.GetValueOrDefault("b3ShapeCastPairInput.transform") == "Transform of shape B in shape A's frame, the relative pose B in A",
    $"Summary is: {memberSummaries.GetValueOrDefault("b3ShapeCastPairInput.transform")}");

Check("StructField_InlineDoxygen", "b3ShapeCastPairInput.translationB has clean ///< summary",
    () => memberSummaries.GetValueOrDefault("b3ShapeCastPairInput.translationB") == "The translation of shape B, in A's frame",
    $"Summary is: {memberSummaries.GetValueOrDefault("b3ShapeCastPairInput.translationB")}");

Check("StructField_InlineDoxygen", "b3ShapeCastPairInput.maxFraction has clean ///< summary",
    () => memberSummaries.GetValueOrDefault("b3ShapeCastPairInput.maxFraction") == "The fraction of the translation to consider, typically 1",
    $"Summary is: {memberSummaries.GetValueOrDefault("b3ShapeCastPairInput.maxFraction")}");

Check("StructField_InlineDoxygen", "b3ShapeCastPairInput.canEncroach has clean ///< summary",
    () => memberSummaries.GetValueOrDefault("b3ShapeCastPairInput.canEncroach") == "Allows shapes with a radius to move slightly closer if already touching",
    $"Summary is: {memberSummaries.GetValueOrDefault("b3ShapeCastPairInput.canEncroach")}");

// ============================================================
// 5. ENUM TYPE documentation
// ============================================================
Check("EnumType", "b3RecQueryType enum is documented",
    () => allMembers.Contains("b3RecQueryType"),
    "Missing b3RecQueryType (typedef enum with { on next line)");

Check("EnumType", "b3JointType enum is documented",
    () => allMembers.Contains("b3JointType"),
    "Missing b3JointType enum");

Check("EnumType", "b3ShapeType enum is documented",
    () => allMembers.Contains("b3ShapeType"),
    "Missing b3ShapeType enum");

// ============================================================
// 6. ENUM VALUE documentation
// ============================================================
Check("EnumValue", "b3ShapeType.b3_capsuleShape enum value is documented",
    () => allMembers.Contains("b3ShapeType.b3_capsuleShape"),
    "Missing b3ShapeType.b3_capsuleShape");

Check("EnumValue", "b3ShapeType.b3_hullShape enum value is documented",
    () => allMembers.Contains("b3ShapeType.b3_hullShape"),
    "Missing b3ShapeType.b3_hullShape");

Check("EnumValue", "b3ShapeType.b3_sphereShape enum value is documented",
    () => allMembers.Contains("b3ShapeType.b3_sphereShape"),
    "Missing b3ShapeType.b3_sphereShape");

// ============================================================
// 7. MULTI-FIELD declarations (e.g., b3Vec3 cx, cy, cz;)
// ============================================================
Check("MultiField", "b3Sweep has multi-field entries (c1, c2, q1, q2)",
    () => allMembers.Contains("b3Sweep.c1") && allMembers.Contains("b3Sweep.c2")
        && allMembers.Contains("b3Sweep.q1") && allMembers.Contains("b3Sweep.q2"),
    "Missing one or more multi-field entries in b3Sweep");

Check("MultiField", "b3SimplexVertex has multi-field entries (wA, wB, w, a, indexA, indexB)",
    () => allMembers.Contains("b3SimplexVertex.wA") && allMembers.Contains("b3SimplexVertex.wB")
        && allMembers.Contains("b3SimplexVertex.w") && allMembers.Contains("b3SimplexVertex.a")
        && allMembers.Contains("b3SimplexVertex.indexA") && allMembers.Contains("b3SimplexVertex.indexB"),
    "Missing one or more multi-field entries in b3SimplexVertex");

// ============================================================
// 8. B3_API function documentation
// ============================================================
Check("B3ApiFunction", "Box3D.b3World_Step is documented",
    () => allMembers.Contains("Box3D.b3World_Step"),
    "Missing Box3D.b3World_Step");

Check("B3ApiFunction", "Box3D.b3SetAllocator is documented",
    () => allMembers.Contains("Box3D.b3SetAllocator"),
    "Missing Box3D.b3SetAllocator");

Check("B3ApiFunction", "Box3D.b3World_Create is documented",
    () => allMembers.Contains("Box3D.b3CreateWorld"),
    "Missing Box3D.b3CreateWorld");

Check("B3ApiFunction", "Box3D.b3DefaultDistanceJointDef is documented",
    () => allMembers.Contains("Box3D.b3DefaultDistanceJointDef"),
    "Missing Box3D.b3DefaultDistanceJointDef");

// ============================================================
// 9. FORWARD DECLARATION fix
// ============================================================
Check("ForwardDecl", "b3Capacity.staticShapeCount is NOT wrongly attributed as b3DebugShape.staticShapeCount",
    () => !allMembers.Contains("b3DebugShape.staticShapeCount"),
    "b3DebugShape.staticShapeCount still exists (forward decl bug)");

Check("ForwardDecl", "b3Capacity fields exist (not shadowed by forward decl)",
    () => allMembers.Contains("b3Capacity.staticShapeCount")
        && allMembers.Contains("b3Capacity.dynamicShapeCount")
        && allMembers.Contains("b3Capacity.staticBodyCount")
        && allMembers.Contains("b3Capacity.dynamicBodyCount")
        && allMembers.Contains("b3Capacity.contactCount"),
    "One or more b3Capacity fields missing");

// ============================================================
// 10. POINTER TYPE fields (const char*, type*, etc.)
// ============================================================
Check("PointerField", "b3RecQueryInfo.name (const char*) is documented",
    () => allMembers.Contains("b3RecQueryInfo.name"),
    "Missing b3RecQueryInfo.name (const char* field)");

Check("PointerField", "b3QueryFilter.name (const char*) is documented",
    () => allMembers.Contains("b3QueryFilter.name"),
    "Missing b3QueryFilter.name (const char* field)");

Check("PointerField", "b3SensorEvents.beginEvents (pointer field) is documented",
    () => allMembers.Contains("b3SensorEvents.beginEvents"),
    "Missing b3SensorEvents.beginEvents");

Check("PointerField", "b3SensorEvents.endEvents (pointer field) is documented",
    () => allMembers.Contains("b3SensorEvents.endEvents"),
    "Missing b3SensorEvents.endEvents");

// ============================================================
// 11. SUMMARY CLEANLINESS (no /< prefix from "///<")
// ============================================================
Check("CleanSummary", "No summary starts with '/<' (from broken ///< extraction)",
    () => !memberSummaries.Values.Any(s => s.StartsWith("/<")),
    "Found summaries starting with '/< -- ///< extraction bug still present");

// ============================================================
// 12. Struct close pattern (} Name;)
// ============================================================
Check("StructClose", "b3JointDef is documented (from struct close pattern)",
    () => allMembers.Contains("b3JointDef"),
    "Missing b3JointDef");

Check("StructClose", "b3RayResult is documented",
    () => allMembers.Contains("b3RayResult"),
    "Missing b3RayResult");

Check("StructClose", "b3CastOutput is documented",
    () => allMembers.Contains("b3CastOutput"),
    "Missing b3CastOutput");

// ============================================================
// 13. Typedef function pointers
// ============================================================
Check("TypedefFunc", "b3AllocFcn (function pointer typedef) is documented",
    () => allMembers.Contains("b3AllocFcn"),
    "Missing b3AllocFcn");

Check("TypedefFunc", "b3AssertFcn (function pointer typedef) is documented",
    () => allMembers.Contains("b3AssertFcn"),
    "Missing b3AssertFcn");

// ============================================================
// 14. Typedef with { on next line (struct and enum)
// ============================================================
Check("TypedefNextLine", "b3RecQueryInfo (typedef struct with { on next line) is documented",
    () => allMembers.Contains("b3RecQueryInfo"),
    "Missing b3RecQueryInfo (typedef struct { next line)");

Check("TypedefNextLine", "b3ContactBeginTouchEvent (typedef struct with { on next line) is documented",
    () => allMembers.Contains("b3ContactBeginTouchEvent"),
    "Missing b3ContactBeginTouchEvent");

Check("TypedefNextLine", "b3ContactEndTouchEvent is documented",
    () => allMembers.Contains("b3ContactEndTouchEvent"),
    "Missing b3ContactEndTouchEvent");

// ============================================================
// 15. No residual forward-declared ghost members
// ============================================================
Check("NoGhost", "No spurious b3DebugShape fields from forward decl (only real fields)",
    () =>
    {
        var debugShapeFields = allMembers.Where(m => m.StartsWith("b3DebugShape.")).ToList();
        // b3DebugShape has real fields: shapeId, type, ... we just check no capacity fields leak in
        return !debugShapeFields.Any(f => f.Contains("ShapeCount") || f.Contains("BodyCount") || f.Contains("contactCount"));
    },
    "b3DebugShape has spurious fields leaked from b3Capacity");

// ============================================================
// 16. Overall member count sanity
// ============================================================
Check("Sanity", "Total documented members > 1000",
    () => allMembers.Count > 1000,
    $"Only {allMembers.Count} members -- expected > 1000");

Check("Sanity", "Total documented members < 2000",
    () => allMembers.Count < 2000,
    $"Too many members: {allMembers.Count} (might indicate duplication)");

// ============================================================
// Generate report
// ============================================================
Console.WriteLine("========================================");
Console.WriteLine("  Box3D.xml Documentation Coverage Report");
Console.WriteLine($"  Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
Console.WriteLine($"  Total members in XML: {allMembers.Count}");
Console.WriteLine("========================================");
Console.WriteLine("");

var categories = results.Select(r => r.category).Distinct().OrderBy(c => c).ToList();
int totalPass = 0, totalFail = 0;

foreach (var cat in categories)
{
    var catResults = results.Where(r => r.category == cat).ToList();
    int pass = catResults.Count(r => r.pass);
    int fail = catResults.Count(r => !r.pass);
    totalPass += pass;
    totalFail += fail;

    Console.WriteLine($"--- {cat} ({pass}/{pass + fail}) ---");
    foreach (var r in catResults)
    {
        var status = r.pass ? "PASS" : "FAIL";
        Console.WriteLine($"  [{status}] {r.description}");
        if (!r.pass)
            Console.WriteLine($"         {r.detail}");
    }
    Console.WriteLine("");
}

Console.WriteLine("========================================");
Console.WriteLine($"  Total: {totalPass} passed, {totalFail} failed out of {totalPass + totalFail}");
Console.WriteLine("========================================");

if (totalFail > 0)
    Environment.Exit(1);
