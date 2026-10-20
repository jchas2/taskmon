#:package Microsoft.CodeAnalysis.CSharp@4.14.0

// Reorders C# type members to match the member ordering rules in docs/CODING_STYLE.md:
//   1. fields and nested types (original relative order)
//   2. events (A-Z)
//   3. constructors and destructor (original relative order)
//   4. properties, methods, indexers and operators (one A-Z list, case-insensitive)
//
// Skipped: property-only types (data classes) and enums.
// Comments, attributes and balanced #if blocks move with their member. A member whose
// preprocessor directives are split across neighbours is reported as an error and the file is
// left untouched, to be ordered by hand.
//
// Every rewritten file is checked to contain the same non-blank lines as before, so members are
// only ever moved, never dropped or duplicated.
//
// Usage (from the repo root):
//   dotnet run eng/tools/reorder-members.cs -- [--dry-run | --check] <file-or-directory>...
//
// --dry-run reports the new order without writing.
// --check   writes nothing and exits 1 if any file is out of order (used by style.sh / style.ps1).
// Exits 1 if any file had an error.

using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

bool check  = args.Contains("--check");
bool dryRun = check || args.Contains("--dry-run");
var inputs = args.Where(a => !a.StartsWith("--")).ToList();

if (inputs.Count == 0) {
    Console.Error.WriteLine("Usage: dotnet run eng/tools/reorder-members.cs -- [--dry-run | --check] <file-or-directory>...");
    return 2;
}

var files = new List<string>();
foreach (string input in inputs) {
    if (Directory.Exists(input)) {
        files.AddRange(Directory.EnumerateFiles(input, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => segment is "obj" or "bin")));
    }
    else {
        files.Add(input);
    }
}

files.Sort(StringComparer.Ordinal);

var parseOptions = new CSharpParseOptions(LanguageVersion.Preview,
    preprocessorSymbols: ["__WIN32__", "__APPLE__"]);

int changed = 0, errors = 0;

foreach (string file in files) {
    byte[] bytes = File.ReadAllBytes(file);
    bool bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
    string text = Encoding.UTF8.GetString(bytes, bom ? 3 : 0, bytes.Length - (bom ? 3 : 0));

    SyntaxTree tree = CSharpSyntaxTree.ParseText(text, parseOptions);
    var rewriter = new Reorderer();
    SyntaxNode newRoot = rewriter.Visit(tree.GetRoot());

    if (rewriter.Errors.Count > 0) {
        errors++;
        foreach (string e in rewriter.Errors) Console.WriteLine($"ERROR {file}: {e}");
        continue;
    }

    string newText = newRoot.ToFullString();

    if (newText == text) {
        continue;
    }

    if (!LinesEquivalent(text, newText)) {
        errors++;
        Console.WriteLine($"ERROR {file}: line multiset mismatch, not written");
        continue;
    }

    changed++;
    Console.WriteLine($"{(check ? "OUT OF ORDER" : "CHANGED")} {file}");
    foreach (string l in rewriter.Log) Console.WriteLine("    " + l);

    if (!dryRun) {
        byte[] outBytes = Encoding.UTF8.GetBytes(newText);
        File.WriteAllBytes(file, bom ? [0xEF, 0xBB, 0xBF, .. outBytes] : outBytes);
    }
}

if (check) {
    Console.WriteLine($"\n{files.Count} files scanned, {changed} out of order, {errors} errors");

    if (changed > 0) {
        Console.WriteLine("Fix with: dotnet run eng/tools/reorder-members.cs -- <file-or-directory>");
    }

    return changed > 0 || errors > 0 ? 1 : 0;
}

Console.WriteLine($"\n{files.Count} files scanned, {changed} changed, {errors} errors{(dryRun ? " (dry run)" : "")}");
return errors > 0 ? 1 : 0;

static bool LinesEquivalent(string a, string b)
{
    static List<string> Lines(string s) => s.Split('\n')
        .Select(l => l.TrimEnd())
        .Where(l => l.Length > 0)
        .OrderBy(l => l, StringComparer.Ordinal)
        .ToList();

    return Lines(a).SequenceEqual(Lines(b));
}

sealed class Reorderer : CSharpSyntaxRewriter
{
    private static readonly StringComparer NameComparer = new AlphaComparer();

    private sealed class AlphaComparer : StringComparer
    {
        public override int Compare(string? x, string? y)
        {
            int c = StringComparer.OrdinalIgnoreCase.Compare(x, y);
            return c != 0 ? c : StringComparer.Ordinal.Compare(x, y);
        }
        public override bool Equals(string? x, string? y) => Compare(x, y) == 0;
        public override int GetHashCode(string obj) => obj.GetHashCode();
    }

    // Net #if/#region balance; null if it goes negative, has #else/#elif/disabled text outside an
    // #if, or contains #pragma/#nullable.
    private static int? Balance(IEnumerable<SyntaxTrivia> trivia)
    {
        int bal = 0;
        foreach (SyntaxTrivia t in trivia) {
            switch (t.Kind()) {
                case SyntaxKind.IfDirectiveTrivia:
                case SyntaxKind.RegionDirectiveTrivia:
                    bal++;
                    break;
                case SyntaxKind.EndIfDirectiveTrivia:
                case SyntaxKind.EndRegionDirectiveTrivia:
                    if (--bal < 0) return null;
                    break;
                case SyntaxKind.ElseDirectiveTrivia:
                case SyntaxKind.ElifDirectiveTrivia:
                case SyntaxKind.DisabledTextTrivia:
                    if (bal == 0) return null;
                    break;
                case SyntaxKind.PragmaWarningDirectiveTrivia:
                case SyntaxKind.NullableDirectiveTrivia:
                    return null;
            }
        }
        return bal;
    }

    public List<string> Errors { get; } = [];

    private static bool IsDirective(SyntaxTrivia t) => t.IsDirective || t.IsKind(SyntaxKind.DisabledTextTrivia);

    public List<string> Log { get; } = [];

    private static string Name(MemberDeclarationSyntax m) => m switch {
        MethodDeclarationSyntax x             => x.Identifier.ValueText,
        PropertyDeclarationSyntax x           => x.Identifier.ValueText,
        IndexerDeclarationSyntax              => "this",
        OperatorDeclarationSyntax x           => "operator" + x.OperatorToken.ValueText,
        ConversionOperatorDeclarationSyntax x => "operator " + x.Type,
        EventFieldDeclarationSyntax x         => x.Declaration.Variables[0].Identifier.ValueText,
        EventDeclarationSyntax x              => x.Identifier.ValueText,
        ConstructorDeclarationSyntax x        => ".ctor " + x.Identifier.ValueText,
        BaseTypeDeclarationSyntax x           => "type " + x.Identifier.ValueText,
        FieldDeclarationSyntax x              => "field " + x.Declaration.Variables[0].Identifier.ValueText,
        _                                     => m.Kind().ToString()
    };

    private static int Rank(MemberDeclarationSyntax m) => m switch {
        EventFieldDeclarationSyntax or EventDeclarationSyntax                         => 1,
        ConstructorDeclarationSyntax or DestructorDeclarationSyntax                   => 2,
        MethodDeclarationSyntax or PropertyDeclarationSyntax or IndexerDeclarationSyntax
            or OperatorDeclarationSyntax or ConversionOperatorDeclarationSyntax       => 3,
        _                                                                             => 0
    };

    private TypeDeclarationSyntax Reorder(TypeDeclarationSyntax node)
    {
        var members = node.Members.ToList();
        string typeName = node.Identifier.ValueText;

        if (members.Count < 2) return node;

        var sortables = members.Where(m => Rank(m) == 3).ToList();

        if (!sortables.Any(m => m is not PropertyDeclarationSyntax)) {
            return node; // property-only DTO: leave as-is
        }

        var ordered = members
            .Select((m, i) => (m, i))
            .OrderBy(x => Rank(x.m))
            .ThenBy(x => Rank(x.m) is 1 or 3 ? Name(x.m) : "", NameComparer)
            .ThenBy(x => x.i)
            .Select(x => x.i)
            .ToList();

        if (ordered.SequenceEqual(Enumerable.Range(0, members.Count))) return node;

        // Split anchored directive prefix (file-level #if / #pragma) off the first member.
        SyntaxTriviaList firstLead = members[0].GetLeadingTrivia();
        int lastDirective = -1;
        for (int k = 0; k < firstLead.Count; k++) {
            if (IsDirective(firstLead[k])) lastDirective = k;
        }

        var prefix = new List<SyntaxTrivia>();
        var leads  = members.Select(m => m.GetLeadingTrivia().ToList()).ToList();

        if (lastDirective >= 0) {
            prefix   = firstLead.Take(lastDirective + 1).ToList();
            leads[0] = firstLead.Skip(lastDirective + 1).ToList();
        }

        // Every member must carry balanced directives so it can move safely.
        for (int k = 0; k < members.Count; k++) {
            int pos = ordered.IndexOf(k);
            if (pos == k && (k == 0 || ordered[k - 1] == k - 1) && (k + 1 >= members.Count || ordered[k + 1] == k + 1)) {
                continue; // neighbours unchanged
            }
            var trivia = leads[k].Concat(members[k].DescendantTrivia().Skip(members[k].GetLeadingTrivia().Count));
            int? bal = Balance(trivia);

            if (bal != 0) {
                Errors.Add($"{typeName}.{Name(members[k])} has unbalanced/unsupported preprocessor directives; reorder by hand");
            }
        }

        if (Errors.Count > 0) return node;

        SyntaxTrivia eol = SyntaxFactory.EndOfLine("\n");
        var result = new List<MemberDeclarationSyntax>();

        for (int pos = 0; pos < ordered.Count; pos++) {
            int k = ordered[pos];
            List<SyntaxTrivia> lead = leads[k];
            int originalPredecessor = k - 1;
            int newPredecessor = pos == 0 ? -1 : ordered[pos - 1];

            List<SyntaxTrivia> newLead;

            if (pos == 0) {
                newLead = [.. prefix, .. (k == 0 ? lead : StripBlank(lead))];
            }
            else if (newPredecessor == originalPredecessor && k != 0) {
                newLead = lead;
            }
            else {
                newLead = [eol, .. StripBlank(lead)];

                string? comment = lead.Where(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
                                                  t.IsKind(SyntaxKind.MultiLineCommentTrivia) ||
                                                  t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia))
                                      .Select(t => t.ToString().Trim())
                                      .FirstOrDefault();
                if (comment != null) {
                    Log.Add($"note: {typeName}.{Name(members[k])} moved with leading comment: {comment.Split('\n')[0]}");
                }
            }

            result.Add(members[k].WithLeadingTrivia(newLead));
        }

        Log.Add($"{typeName}: " + string.Join(", ", ordered.Where(i => Rank(members[i]) == 3).Select(i => Name(members[i]))));

        var movedNonSortables = ordered
            .Select((origIdx, pos) => (origIdx, pos))
            .Where(x => Rank(members[x.origIdx]) != 3 &&
                        members.Take(x.origIdx).Any(m => Rank(m) == 3))
            .Select(x => Name(members[x.origIdx]))
            .ToList();
        if (movedNonSortables.Count > 0) {
            Log.Add($"note: {typeName} moved above methods/properties: {string.Join(", ", movedNonSortables)}");
        }

        return node.WithMembers(SyntaxFactory.List(result));
    }

    // Remove leading blank lines (whitespace/EOL run up to its last EOL), keep indentation.
    private static List<SyntaxTrivia> StripBlank(List<SyntaxTrivia> lead)
    {
        int lastEol = -1;
        for (int i = 0; i < lead.Count; i++) {
            if (lead[i].IsKind(SyntaxKind.EndOfLineTrivia)) lastEol = i;
            else if (!lead[i].IsKind(SyntaxKind.WhitespaceTrivia)) break;
        }
        return lead.Skip(lastEol + 1).ToList();
    }

    public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node) =>
        Reorder((TypeDeclarationSyntax)base.VisitClassDeclaration(node)!);

    public override SyntaxNode? VisitInterfaceDeclaration(InterfaceDeclarationSyntax node) =>
        Reorder((TypeDeclarationSyntax)base.VisitInterfaceDeclaration(node)!);

    public override SyntaxNode? VisitRecordDeclaration(RecordDeclarationSyntax node) =>
        Reorder((TypeDeclarationSyntax)base.VisitRecordDeclaration(node)!);

    public override SyntaxNode? VisitStructDeclaration(StructDeclarationSyntax node) =>
        Reorder((TypeDeclarationSyntax)base.VisitStructDeclaration(node)!);
}
