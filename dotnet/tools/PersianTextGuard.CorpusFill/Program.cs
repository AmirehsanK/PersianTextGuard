using System.Text;
using PersianTextGuard.Conformance;

// Fills in pending corpus cases from this .NET build, and reports recorded cases it disagrees with.
// It never changes an existing "expected": a disagreement is fixed by a person, on purpose.
//
//   (no arguments)  fill pending cases, report disagreements
//   --check         report disagreements, write nothing
//   --seed          add pending cases from the existing tests and the supplementary suite, then fill

Console.OutputEncoding = Encoding.UTF8;

var check = args.Contains("--check");
var seed = args.Contains("--seed");
var unknown = args.Where(a => a is not ("--check" or "--seed")).ToList();
if (unknown.Count > 0 || (check && seed))
{
    Console.Error.WriteLine("Usage: dotnet run --project dotnet/tools/PersianTextGuard.CorpusFill [-- --check | --seed]");
    return 2;
}

var root = FindRoot();
if (root is null)
{
    Console.Error.WriteLine("Could not find the repository root (a directory with VERSION and conformance/)");
    return 2;
}

var directory = Path.Combine(root, "conformance");

try
{
    if (seed)
    {
        Seeds.Run(Corpus.Load(directory), root);
    }

    return Fill(Corpus.Load(directory), write: !check);
}
catch (Exception e) when (e is InvalidDataException or DirectoryNotFoundException)
{
    Console.Error.WriteLine(e.Message);
    return 2;
}

static int Fill(Corpus corpus, bool write)
{
    var filled = 0;
    var pending = 0;
    var disagreements = 0;
    var changed = new HashSet<string>(StringComparer.Ordinal);

    foreach (var corpusCase in corpus.Cases)
    {
        System.Text.Json.Nodes.JsonObject actual;
        try
        {
            actual = corpus.Evaluate(corpusCase);
        }
        catch (Exception e) when (e is InvalidDataException or FormatException or InvalidOperationException)
        {
            Console.WriteLine($"ERROR {corpusCase.Id} ({corpusCase.File}): {e.Message}");
            disagreements++;
            continue;
        }

        if (corpusCase.IsPending)
        {
            // A pending case whose result breaks its kind is mislabelled; recording it would pin the mistake.
            var violations = Corpus.CheckKindRules(corpusCase, actual);
            if (violations.Count > 0)
            {
                foreach (var violation in violations)
                {
                    Console.WriteLine($"KIND RULE {corpusCase.Id} ({corpusCase.File}): {violation}");
                }

                disagreements++;
            }
            else if (write)
            {
                corpusCase.Json["expected"] = actual;
                changed.Add(corpusCase.File);
                filled++;
            }
            else
            {
                Console.WriteLine($"PENDING {corpusCase.Id} ({corpusCase.File})");
                pending++;
            }

            continue;
        }

        var problems = Corpus.CheckKindRules(corpusCase)
            .Select(v => $"KIND RULE {corpusCase.Id}: {v}")
            .Concat(Corpus.Compare(corpusCase.Json["expected"], actual)
                .Select(d => $"DISAGREES {corpusCase.Id} {d.Path}: expected {d.Expected} actual {d.Actual}"))
            .ToList();

        if (problems.Count > 0)
        {
            problems.ForEach(Console.WriteLine);
            disagreements++;
        }
    }

    foreach (var file in corpus.Files.Where(f => changed.Contains(f.Name)))
    {
        File.WriteAllText(file.Path, CaseWriter.WriteFile(file.Cases), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        Console.WriteLine($"Wrote {file.Name}");
    }

    if (!write && pending > 0)
    {
        Console.WriteLine($"{pending} pending case(s)");
    }

    Console.WriteLine($"Filled {filled} case(s); {disagreements} disagreement(s)");
    return disagreements > 0 ? 1 : 0;
}

static string? FindRoot()
{
    for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
    {
        if (Directory.Exists(Path.Combine(dir.FullName, "conformance")) && File.Exists(Path.Combine(dir.FullName, "VERSION")))
        {
            return dir.FullName;
        }
    }

    return null;
}
