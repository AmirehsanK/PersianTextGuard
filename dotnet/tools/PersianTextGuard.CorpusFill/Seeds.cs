using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using PersianTextGuard.Tests;
using Xunit;

namespace PersianTextGuard.Conformance;

/// <summary>
/// <c>--seed</c>: builds the initial corpus as pending cases from the existing .NET tests, literals
/// from their <c>[Fact]</c> bodies, and a hand-labelled supplementary suite (research R9).
/// </summary>
/// <remarks>
/// A case whose content key (configuration, built input and masks, or the equivalent for other
/// kinds) is already in the corpus is skipped, whatever its id, so re-running adds nothing twice.
/// </remarks>
internal static class Seeds
{
    /// <summary>
    /// Hand-labelled messages from the 1.2.0 package test run (<c>D:\Git\PtgTest\Program.cs</c>),
    /// copied so the repository does not depend on that folder. <c>Bad</c> is the human label.
    /// </summary>
    private static readonly (string Cat, string Text, bool Bad, string Note)[] SupplementarySuite =
    [
    // ---------------- PERSIAN: plain ----------------
    ("FA plain", "کیر", true, ""),
    ("FA plain", "کیرم تو این سایت", true, ""),
    ("FA plain", "کسکش", true, ""),
    ("FA plain", "کس کش", true, "space"),
    ("FA plain", "کس‌کش", true, "ZWNJ"),
    ("FA plain", "جنده", true, ""),
    ("FA plain", "مادرجنده", true, ""),
    ("FA plain", "مادر جنده", true, "space"),
    ("FA plain", "جاکش", true, ""),
    ("FA plain", "حرومزاده", true, ""),
    ("FA plain", "بی‌ناموس", true, "ZWNJ"),
    ("FA plain", "بیشرف", true, ""),
    ("FA plain", "کسشعر نگو", true, ""),
    ("FA plain", "کس‌شعر", true, "ZWNJ"),
    ("FA plain", "کونی", true, ""),
    ("FA plain", "گاییدمت", true, ""),
    ("FA plain", "قرمساق", true, ""),
    ("FA plain", "دیوث", true, ""),
    ("FA plain", "لاشی", true, ""),
    ("FA plain", "پدرسگ", true, ""),
    ("FA plain", "اسکل", true, ""),
    ("FA plain", "خارکسده", true, ""),
    ("FA plain", "کصافت", true, ""),
    ("FA plain", "تو یه جنده ای", true, "in sentence"),
    ("FA plain", "برو بمیر کسخل", true, "end of sentence"),
    ("FA plain", "جنده!", true, "punctuation"),
    ("FA plain", "«کیر»", true, "guillemets"),
    ("FA plain", "کیر؟", true, "Persian ?"),

    // ---------------- PERSIAN: inflections / variants not literally listed ----------------
    ("FA variant", "جنده‌ها", true, "plural with ZWNJ"),
    ("FA variant", "جندها", true, "plural no ZWNJ"),
    ("FA variant", "کیرتون", true, "suffix -etoon"),
    ("FA variant", "کونیا", true, "plural colloquial"),
    ("FA variant", "کسکشا", true, "plural colloquial (Anywhere)"),
    ("FA variant", "کسخول", true, "alt spelling"),
    ("FA variant", "کثافت", true, "common spelling of kesafat"),
    ("FA variant", "بی‌پدر", true, "bi-pedar"),
    ("FA variant", "گه نخور", true, "ge"),
    ("FA variant", "گوه‌خوری", true, "goh-khori"),
    ("FA variant", "ریدی", true, "ridi"),
    ("FA variant", "بگا رفتی", true, "bega"),
    ("FA variant", "کیرخور", true, "kir-khor"),
    ("FA variant", "جاکشی", true, "Anywhere"),
    ("FA variant", "حرامزاده‌ها", true, "Anywhere + ZWNJ"),

    // ---------------- PERSIAN: evasion ----------------
    ("FA evasion", "كير", true, "Arabic kaf+yeh"),
    ("FA evasion", "كسكش", true, "Arabic kaf"),
    ("FA evasion", "جندة", true, "teh marbuta"),
    ("FA evasion", "ک\u200Cی\u200Cر", true, "ZWNJ between letters"),
    ("FA evasion", "ک\u200Bیر", true, "zero-width space"),
    ("FA evasion", "کی\u00ADر", true, "soft hyphen"),
    ("FA evasion", "ج\u200Fنده", true, "RLM bidi"),
    ("FA evasion", "کــــیر", true, "tatweel"),
    ("FA evasion", "کِیر", true, "kasra"),
    ("FA evasion", "جَنْدِه", true, "harakat"),
    ("FA evasion", "ﻛﻴﺮ", true, "presentation forms"),
    ("FA evasion", "ڪير", true, "Urdu kaf"),
    ("FA evasion", "جندھ", true, "Urdu heh doachashmee"),
    ("FA evasion", "کیییییییر", true, "repeated"),
    ("FA evasion", "جننننده", true, "repeated"),
    ("FA evasion", "ک ی ر", true, "spaced"),
    ("FA evasion", "ک.ی.ر", true, "dotted"),
    ("FA evasion", "ک-ی-ر", true, "hyphens"),
    ("FA evasion", "ک_ی_ر", true, "underscore"),
    ("FA evasion", "ک*ی*ر", true, "stars"),
    ("FA evasion", "ک/ی/ر", true, "slashes"),
    ("FA evasion", "ج ن د ه", true, "spaced 4"),
    ("FA evasion", "ک س ک ش", true, "spaced 4"),
    ("FA evasion", "کی ر", true, "partial split"),
    ("FA evasion", "جن ده", true, "partial split"),
    ("FA evasion", "ک*یر", true, "filler inside Persian word"),
    ("FA evasion", "ج.نده", true, "one dot inside"),
    ("FA evasion", "ک🍆ر", true, "emoji inside"),
    ("FA evasion", "ک ی ر ت و ن", true, "spaced inflection"),
    ("FA evasion", "كِــيـــيـير", true, "combo: Arabic+kasra+tatweel+repeats"),
    ("FA evasion", "ک\nی\nر", true, "newlines"),

    // ---------------- FINGLISH ----------------
    ("Finglish", "kir", true, ""),
    ("Finglish", "KIR", true, "upper"),
    ("Finglish", "kiram too in site", true, ""),
    ("Finglish", "koskesh", true, ""),
    ("Finglish", "kos kesh", true, "space"),
    ("Finglish", "kos nanat", true, "space"),
    ("Finglish", "kosnanet", true, "alt spelling"),
    ("Finglish", "koskhol", true, ""),
    ("Finglish", "kosshér", true, "accent"),
    ("Finglish", "kossher", true, "kos-sher"),
    ("Finglish", "kosher nagoo", true, "kos-sher (collides w/ English kosher)"),
    ("Finglish", "jende", true, ""),
    ("Finglish", "jendeh", true, ""),
    ("Finglish", "madar jende", true, ""),
    ("Finglish", "jakesh", true, ""),
    ("Finglish", "binamoos", true, ""),
    ("Finglish", "bi-namoos", true, "hyphen"),
    ("Finglish", "pedarsag", true, ""),
    ("Finglish", "pedar sokhte", true, "pedarsookhte (mild)"),
    ("Finglish", "haroomzadeh", true, ""),
    ("Finglish", "lashi", true, ""),
    ("Finglish", "koni", true, "alt spelling of kooni"),
    ("Finglish", "kooni", true, ""),
    ("Finglish", "kuni", true, "alt spelling"),
    ("Finglish", "keer", true, "alt spelling of kir"),
    ("Finglish", "kiir", true, ""),
    ("Finglish", "k1r", true, "leet"),
    ("Finglish", "k!r", true, "leet"),
    ("Finglish", "k0s", true, "leet"),
    ("Finglish", "k0sk3sh", true, "leet"),
    ("Finglish", "j3nd3", true, "leet"),
    ("Finglish", "kiiiiiir", true, "repeats"),
    ("Finglish", "k o s k e s h", true, "spaced"),
    ("Finglish", "k.i.r", true, "dotted"),
    ("Finglish", "kоs", true, "Cyrillic о"),
    ("Finglish", "kіr", true, "Ukrainian і"),
    ("Finglish", "goh nakhor", true, "goh"),
    ("Finglish", "ridi", true, ""),
    ("Finglish", "kesafat", true, ""),
    ("Finglish", "oskol", true, ""),
    ("Finglish", "eskol", true, ""),
    ("Finglish", "khar kosde", true, "space"),
    ("Finglish", "gaidamet", true, "Anywhere gaidam"),
    ("Finglish", "bokonamet", true, ""),
    ("Finglish", "kos_kesh", true, "underscore"),
    ("Finglish", "k*s kesh", true, "star"),

    // ---------------- ENGLISH ----------------
    ("EN plain", "fuck", true, ""),
    ("EN plain", "FUCK YOU", true, ""),
    ("EN plain", "motherfucker", true, "Anywhere"),
    ("EN plain", "fucking idiot", true, ""),
    ("EN plain", "shit", true, ""),
    ("EN plain", "bullshit", true, "Anywhere"),
    ("EN plain", "bitch", true, ""),
    ("EN plain", "sonofabitch", true, ""),
    ("EN plain", "asshole", true, ""),
    ("EN plain", "cunt", true, ""),
    ("EN plain", "dickhead", true, ""),
    ("EN plain", "whore", true, ""),
    ("EN plain", "slut", true, ""),
    ("EN plain", "pornhub", true, ""),
    ("EN plain", "go kys", true, ""),
    ("EN plain", "kill yourself", true, ""),
    ("EN plain", "nigger", true, ""),
    ("EN plain", "faggot", true, ""),
    ("EN plain", "retard", true, ""),
    ("EN plain", "wtf", true, "mild"),
    ("EN plain", "damn", true, "mild"),
    ("EN plain", "bastard", true, ""),
    ("EN plain", "piece of shit", true, ""),
    ("EN plain", "shitty", true, ""),
    ("EN plain", "fuckers", true, ""),
    ("EN plain", "assholes", true, ""),
    ("EN plain", "dicks", true, ""),
    ("EN plain", "cocksucker", true, ""),
    ("EN plain", "kill urself", true, "variant"),

    ("EN evasion", "f*ck", true, ""),
    ("EN evasion", "f**k", true, ""),
    ("EN evasion", "fu*k", true, ""),
    ("EN evasion", "f@ck", true, ""),
    ("EN evasion", "F.U.C.K", true, ""),
    ("EN evasion", "f u c k", true, ""),
    ("EN evasion", "f-u-c-k", true, ""),
    ("EN evasion", "f_u_c_k", true, ""),
    ("EN evasion", "fuuuuuuck", true, ""),
    ("EN evasion", "fcuk", true, "transposed"),
    ("EN evasion", "fu ck", true, "partial split"),
    ("EN evasion", "fuc k", true, "partial split"),
    ("EN evasion", "fu\u200Bck", true, "ZWSP"),
    ("EN evasion", "fuсk", true, "Cyrillic с"),
    ("EN evasion", "ｆｕｃｋ", true, "fullwidth"),
    ("EN evasion", "𝐟𝐮𝐜𝐤", true, "math bold"),
    ("EN evasion", "fück", true, "umlaut"),
    ("EN evasion", "ƒuck", true, "f-hook"),
    ("EN evasion", "sh1t", true, ""),
    ("EN evasion", "$hit", true, ""),
    ("EN evasion", "5hit", true, ""),
    ("EN evasion", "sh!t", true, ""),
    ("EN evasion", "s.h.i.t", true, ""),
    ("EN evasion", "shíť", true, "diacritics"),
    ("EN evasion", "b1tch", true, ""),
    ("EN evasion", "b!tch", true, ""),
    ("EN evasion", "8itch", true, ""),
    ("EN evasion", "a$$hole", true, ""),
    ("EN evasion", "@$$", true, ""),
    ("EN evasion", "4ss", true, ""),
    ("EN evasion", "c*nt", true, ""),
    ("EN evasion", "cvnt", true, ""),
    ("EN evasion", "d1ck", true, ""),
    ("EN evasion", "n1gg3r", true, ""),
    ("EN evasion", "f@gg0t", true, ""),
    ("EN evasion", "wh0re", true, ""),
    ("EN evasion", "$lut", true, ""),
    ("EN evasion", "p0rn", true, ""),
    ("EN evasion", "k y s", true, ""),
    ("EN evasion", "fuk u", true, ""),
    ("EN evasion", "phuck", true, ""),
    ("EN evasion", "f🖕ck", true, "emoji filler"),
    ("EN evasion", "FuCkInG", true, "mixed case"),
    ("EN evasion", "f\nu\nc\nk", true, "newlines"),
    ("EN evasion", "fúck", true, "acute accent"),
    ("EN evasion", "f\u0336u\u0336c\u0336k\u0336", true, "strikethrough combining"),
    ("EN evasion", "🅵🆄🅲🅺", true, "enclosed letters"),
    ("EN evasion", "ᶠᵘᶜᵏ", true, "superscript"),

    // ---------------- CLEAN: Persian (must NOT flag) ----------------
    ("FA clean", "سلام، سفارشم کی میرسه؟", false, ""),
    ("FA clean", "کسی اینجا نیست", false, "کسی"),
    ("FA clean", "کسب و کار", false, "کسب"),
    ("FA clean", "کسر مالیات", false, ""),
    ("FA clean", "کشور ایران", false, ""),
    ("FA clean", "سکسکه گرفتم", false, "hiccup contains سکس"),
    ("FA clean", "اسکله بندر", false, "pier contains اسکل"),
    ("FA clean", "گوهر شب چراغ", false, "jewel contains گوه"),
    ("FA clean", "کوثر خانم", false, "name contains کوث"),
    ("FA clean", "کثیف شده", false, ""),
    ("FA clean", "توله سگ خریدم", false, "puppy!"),
    ("FA clean", "پدر سگ داره", false, "father has a dog"),
    ("FA clean", "این ساک زدن نداره", false, "bag"),
    ("FA clean", "جقجقه بچه", false, "rattle"),
    ("FA clean", "مسکن مهر", false, ""),
    ("FA clean", "کوه دماوند", false, ""),
    ("FA clean", "کونگ فو", false, ""),
    ("FA clean", "کیری ایروینگ", false, "Kyrie Irving (کیری listed)"),
    ("FA clean", "جا کش رو بده", false, "the drawer/stretch"),
    ("FA clean", "این کتاب و ن ... ", false, "single-letter و"),
    ("FA clean", "ب ه ت و چه", false, "spaced word 'به تو چه'"),
    ("FA clean", "می‌روم خانه", false, ""),
    ("FA clean", "کیوسک", false, ""),
    ("FA clean", "کوسه", false, "shark"),
    ("FA clean", "کون فیکون", false, "Quranic phrase"),
    ("FA clean", "هرزگرد", false, ""),
    ("FA clean", "لاشه", false, "carcass"),
    ("FA clean", "دیوس", false, "listed (Zeus?)"),
    ("FA clean", "شهوتی", false, "listed mild"),
    ("FA clean", "کیرکگور فیلسوف", false, "Kierkegaard"),
    ("FA clean", "خایه‌دوزی", false, ""),
    ("FA clean", "ممه", false, "listed (baby-talk)"),
    ("FA clean", "فاکتور خرید", false, "invoice contains فاک"),
    ("FA clean", "فاک", false, "listed"),
    ("FA clean", "اک ی ر", false, ""),
    ("FA clean", "کد ۴۵۵", false, "digits"),

    // ---------------- CLEAN: Finglish ----------------
    ("Finglish clean", "salam khubi?", false, ""),
    ("Finglish clean", "kasi hast?", false, ""),
    ("Finglish clean", "koskhodam", false, ""),
    ("Finglish clean", "koss headphones", false, "brand Koss"),
    ("Finglish clean", "kose", false, "listed; also a surname"),
    ("Finglish clean", "Kir Royale cocktail", false, "listed"),
    ("Finglish clean", "tokhme morgh", false, ""),
    ("Finglish clean", "kosar", false, "name"),
    ("Finglish clean", "koondi", false, ""),
    ("Finglish clean", "kiryat gat", false, ""),
    ("Finglish clean", "Kusadasi", false, ""),
    ("Finglish clean", "jaa kesh", false, "listed phrase (stretch)"),

    // ---------------- CLEAN: English (Scunthorpe tests) ----------------
    ("EN clean", "Scunthorpe United", false, ""),
    ("EN clean", "class assignment", false, ""),
    ("EN clean", "I assume you passed", false, ""),
    ("EN clean", "shiitake mushrooms", false, "squeeze -> shitake"),
    ("EN clean", "he mishit the ball", false, "Anywhere shit"),
    ("EN clean", "push it real good", false, ""),
    ("EN clean", "Pussycat Dolls", false, "Anywhere pussy"),
    ("EN clean", "he sniggered", false, "Anywhere nigger"),
    ("EN clean", "summa cum laude", false, ""),
    ("EN clean", "Cumming, Georgia", false, ""),
    ("EN clean", "Dick Van Dyke", false, "name"),
    ("EN clean", "a cock crowed", false, "rooster"),
    ("EN clean", "Sussex county", false, ""),
    ("EN clean", "grab a garden hoe", false, ""),
    ("EN clean", "raccoon", false, ""),
    ("EN clean", "spick and span", false, ""),
    ("EN clean", "rapeseed oil", false, ""),
    ("EN clean", "fire retardant", false, ""),
    ("EN clean", "I paid 455 dollars", false, "digits fold to 'ass'"),
    ("EN clean", "room 455", false, ""),
    ("EN clean", "call 5417", false, ""),
    ("EN clean", "version 5.1.7", false, ""),
    ("EN clean", "$hit points", false, "hmm $"),
    ("EN clean", "A S S E T S", false, "spaced heading"),
    ("EN clean", "I got a B, you got a C", false, ""),
    ("EN clean", "therapist", false, "contains rapist"),
    ("EN clean", "grape", false, "contains rape"),
    ("EN clean", "cocktail party", false, ""),
    ("EN clean", "Dickens novel", false, ""),
    ("EN clean", "assassin", false, ""),
    ("EN clean", "bass guitar", false, ""),
    ("EN clean", "hello world", false, ""),
    ("EN clean", "Matsushita", false, ""),
    ("EN clean", "cockpit", false, ""),
    ("EN clean", "shitake", false, "spelling variant of shiitake"),
    ("EN clean", "Penistone", false, ""),
    ("EN clean", "arsenal fc", false, ""),
    ("EN clean", "a s s", false, "hmm spaced"),
    ("EN clean", "whoredom", false, "archaic, Anywhere whore"),
    ("EN clean", "hancock", false, ""),
    ("EN clean", "analyst", false, ""),
    ("EN clean", "execute", false, ""),
    ("EN clean", "Documenting", false, ""),
    ("EN clean", "Buttercup", false, ""),
    ("EN clean", "Hitchcock", false, ""),
    ("EN clean", "Shitenn", false, ""),
    ("EN clean", "flick", false, "l->? "),
    ("EN clean", "f1ck", false, ""),
    ("EN clean", "fork", false, ""),
    ("EN clean", "duck", false, ""),
    ("EN clean", "shot", false, ""),
    ("EN clean", "sh0t", false, ""),
    ("EN clean", "pass", false, ""),
    ("EN clean", "cum", false, "listed; Latin 'with'"),
    ("EN clean", "Twatt, Orkney", false, "real place"),
    ("EN clean", "kys = key size", false, "context"),
    ("EN clean", "8 inch", false, "digit fold"),
    ("EN clean", "b1 tch", false, ""),
    ("EN clean", "i t s h i t s", false, "spaced 'its hits' -> itshits contains shit"),
    ("EN clean", "s h i t a k e", false, ""),
    ];

    public static void Run(Corpus corpus, string root) => new Seeder(corpus, root).SeedAll();

    private sealed class Seeder
    {
        private const string Default = "default";

        private readonly Corpus _corpus;
        private readonly string _root;
        private readonly Dictionary<string, JsonArray> _files = new(StringComparer.Ordinal);
        private readonly HashSet<string> _keys = new(StringComparer.Ordinal);
        private readonly HashSet<string> _ids = new(StringComparer.Ordinal);
        private readonly HashSet<string> _changed = new(StringComparer.Ordinal);
        private readonly List<(string File, IReadOnlyList<BannedWord> Words)> _lists = [];

        public Seeder(Corpus corpus, string root)
        {
            _corpus = corpus;
            _root = root;

            foreach (var file in corpus.Files)
            {
                _files[file.Name] = file.Cases;
            }

            foreach (var corpusCase in corpus.Cases)
            {
                _ids.Add(corpusCase.Id);
                _keys.Add(ContentKey(corpusCase.Kind, corpusCase.Json));
            }

            foreach (var (file, list) in new[] { ("matching-persian.json", "persian.txt"), ("matching-finglish.json", "finglish.txt"), ("matching-english.json", "english.txt") })
            {
                using var stream = File.OpenRead(Path.Combine(root, "wordlists", list));
                _lists.Add((file, WordList.Load(stream)));
            }
        }

        public void SeedAll()
        {
            var added = 0;
            added += SeedInlineData();
            added += SeedLiterals();
            var (included, disagreements, duplicates) = SeedSupplementarySuite();
            added += included;

            foreach (var name in _changed)
            {
                var path = Path.Combine(_root, "conformance", "cases", name);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, CaseWriter.WriteFile(_files[name]), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }

            Console.WriteLine($"Seeded {added} pending case(s) into {_changed.Count} file(s)");
            Console.WriteLine($"Supplementary suite: {SupplementarySuite.Length} tuples; {included} included, {disagreements} skipped as disagreeing with 1.2.0, {duplicates} skipped as duplicates");
        }

        // -----------------------------------------------------------------------------------------
        // [InlineData], by reflection
        // -----------------------------------------------------------------------------------------

        private int SeedInlineData()
        {
            var added = 0;

            foreach (var text in InlineStrings(typeof(DefaultListTests), nameof(DefaultListTests.Ordinary_messages_pass)))
            {
                added += Matching(Source(nameof(DefaultListTests.Ordinary_messages_pass)), "ordinary", Default, text);
            }

            foreach (var method in new[]
                     {
                         nameof(DefaultListTests.Plain_entries_match), nameof(DefaultListTests.Evasions_are_caught),
                         nameof(DefaultListTests.Evasions_found_by_testing_1_0_1_are_caught), nameof(DefaultListTests.Persian_suffixes_are_matched),
                         nameof(DefaultListTests.Spellings_missing_from_1_0_1_are_covered),
                         nameof(DefaultListTests.Entries_from_the_persian_swear_words_dataset_match),
                         nameof(DefaultListTests.Anywhere_entries_match_inside_longer_words),
                     })
            {
                foreach (var text in InlineStrings(typeof(DefaultListTests), method))
                {
                    added += Matching(Source(method), "must-match", Default, text);
                }
            }

            foreach (var method in new[] { nameof(DefaultListTests.Additional_persian_entries_match), nameof(DefaultListTests.Web_sourced_finglish_entries_match) })
            {
                foreach (var text in InlineStrings(typeof(DefaultListTests), method))
                {
                    added += Matching(Source(method), "must-match", "all", text);
                }
            }

            foreach (var method in new[] { nameof(FindMatchesTests.Regions_cover_whole_words), nameof(FindMatchesTests.Results_are_ordered_non_overlapping_and_inside_the_message) })
            {
                foreach (var text in InlineStrings(typeof(FindMatchesTests), method))
                {
                    added += Matching(Source(method), "must-match", Default, text);
                }
            }

            var blank = nameof(FindMatchesTests.Missing_or_blank_text_returns_an_empty_list);
            foreach (var text in InlineStrings(typeof(FindMatchesTests), blank))
            {
                added += Matching(Source(blank), "robustness", Default, text);
            }

            var banned = nameof(CensorTests.Banned_words_are_replaced_by_the_mask);
            foreach (var text in InlineStrings(typeof(CensorTests), banned))
            {
                added += Matching(Source(banned), "must-match", Default, text);
            }

            var clean = nameof(CensorTests.Clean_text_comes_back_as_the_same_instance);
            foreach (var text in InlineStrings(typeof(CensorTests), clean))
            {
                added += Matching(Source(clean), text.Trim().Length == 0 ? "robustness" : "ordinary", Default, text);
            }

            var masks = nameof(CensorTests.Letters_digits_whitespace_controls_and_surrogates_are_refused_as_masks);
            foreach (var row in InlineRows(typeof(CensorTests), masks))
            {
                // A lone surrogate cannot be a JSON string; the hand-written build case covers it.
                if (row?[0] is char mask && !char.IsSurrogate(mask))
                {
                    added += Add("mask-validation.json", Source(masks), new JsonObject { ["kind"] = "mask-validation", ["mask"] = mask.ToString() });
                }
            }

            var evasion = nameof(ProfanityFilterTests.The_match_says_which_evasion_was_undone);
            foreach (var text in InlineStrings(typeof(ProfanityFilterTests), evasion))
            {
                added += Matching(Source(evasion), "must-match", "custom-damn", text);
            }

            foreach (var method in new[]
                     {
                         nameof(PersianNormalizerTests.Look_alike_letters_from_other_scripts_fold_to_persian),
                         nameof(PersianNormalizerTests.Invisible_and_decorative_characters_are_removed_for_comparison),
                         nameof(PersianNormalizerTests.All_three_digit_ranges_fold_to_ascii),
                     })
            {
                foreach (var text in InlineStrings(typeof(PersianNormalizerTests), method))
                {
                    added += Normalization(Source(method), "comparison", text);
                }
            }

            var readme = nameof(ReadmeExampleTests.Ordinary_text_named_in_the_readme_passes);
            foreach (var text in InlineStrings(typeof(ReadmeExampleTests), readme))
            {
                added += Matching(Source(readme), "ordinary", Default, text);
            }

            return added;
        }

        private static IEnumerable<object?[]> InlineRows(Type type, string methodName)
        {
            var method = type.GetMethod(methodName) ?? throw new InvalidOperationException($"{type.Name}.{methodName} not found.");
            return method.GetCustomAttributes<InlineDataAttribute>().SelectMany(attribute => attribute.GetData(method));
        }

        /// <summary>The first string argument of every row, in order, once each.</summary>
        private static IEnumerable<string> InlineStrings(Type type, string methodName) =>
            InlineRows(type, methodName).Select(row => row?.OfType<string>().FirstOrDefault()).OfType<string>().Distinct(StringComparer.Ordinal);

        // -----------------------------------------------------------------------------------------
        // Literals from [Fact] bodies, which reflection cannot see
        // -----------------------------------------------------------------------------------------

        private int SeedLiterals()
        {
            var added = 0;

            var findMatches = Source(nameof(FindMatchesTests));
            foreach (var text in new[] { "you bitch, kos kesh", "kir kir kir", "sh1t and f u c k", "ﻛﻴﺮ", "motherfucker", "جنده‌ها رو ببین", "پدر سگ پدر", "fuckfuck", "this is kir" })
            {
                added += Matching(findMatches, "must-match", Default, text);
            }

            added += Matching(findMatches, "ordinary", Default, "سلام، سفارشم کی میرسه؟");
            added += Matching(findMatches, "ordinary", "no-folding", "sh1t");
            added += Matching(findMatches, "robustness", Default, Build(Text("hi "), Utf16("D83D"), Text(" kir")));

            var censor = Source(nameof(CensorTests));
            added += Matching(censor, "must-match", Default, "k kos i kos r");
            added += Matching(censor, "must-match", Default, "kir", "#");
            added += Matching(censor, "robustness", Default, null);

            var filter = Source(nameof(ProfanityFilterTests));
            added += Matching(filter, "ordinary", "custom-kesafat", "چه کثافتی");
            added += Matching(filter, "must-match", "custom-kesafat", "این کثافت");
            foreach (var configuration in new[] { "custom-k0s-whole", "custom-k0s-anywhere" })
            {
                added += Matching(filter, "must-match", configuration, "ye kos inja");
                added += Matching(filter, "must-match", configuration, "ye k0s inja");
            }

            added += Matching(filter, "ordinary", "custom-shit", "push it");
            added += Matching(filter, "must-match", "custom-shit", "s h i t");
            added += Matching(filter, "must-match", "custom-fuck-anywhere", "clusterfuck");
            added += Matching(filter, "ordinary", "custom-ass", "classic");
            added += Matching(filter, "must-match", "custom-fuck-whole", "fuuuuck");
            added += Matching(filter, "ordinary", "custom-fuck-whole-no-squeezing", "fuuuuck");
            added += Matching(filter, "ordinary", "custom-fuck-whole-no-folding", "f*ck");
            added += Matching(filter, "ordinary", "custom-fuck-whole-no-joining", "f u c k");
            added += Matching(filter, "robustness", "custom-x", null);
            added += Matching(filter, "robustness", "custom-x", "   ");
            added += Matching(filter, "ordinary", "custom-empty", "anything");
            foreach (var text in new[] { "# a comment\r\n\r\nwhole\n~anywhere\n  ~  spaced  \n", "کیر\n~fuck\n", "[nonsense]\nword\n", "[insult]\n~x\ny\n" })
            {
                added += Add("word-list-parsing.json", filter, new JsonObject { ["kind"] = "word-list-parsing", ["text"] = text });
            }

            var consistency = Source(nameof(ConsistencyTests));
            const string longClean = "سلام این یک متن معمولی است و هیچ مشکلی ندارد. hello this is fine. ";
            added += Matching(consistency, "robustness", Default, Build(Text("hi "), Utf16("D83D")));
            added += Matching(consistency, "robustness", Default, Build(Utf16("DE00"), Text(" hi")));
            added += Matching(consistency, "robustness", Default, Build(Text("kir "), Utf16("D83D"), Text(" kos")));
            added += Matching(consistency, "robustness", Default, Build(Repeat(longClean, 2000)));
            added += Matching(consistency, "robustness", Default, Build(Repeat(longClean, 2000), Text(" کیر")));

            var defaultList = Source(nameof(DefaultListTests));
            added += Matching(defaultList, "ordinary", Default, "این فیلم آشغال بود");
            added += Matching(defaultList, "must-match", "all", "این فیلم آشغال بود");
            added += Matching(defaultList, "must-match", "slurs-only", "faggot");
            added += Matching(defaultList, "ordinary", "slurs-only", "fuck");
            foreach (var text in new[] { "nigger", "کیر", "kys" })
            {
                added += Matching(defaultList, "must-match", Default, text);
            }

            var readme = Source(nameof(ReadmeExampleTests));
            added += Matching(readme, "must-match", Default, "sh1iiit");
            added += Matching(readme, "must-match", "slurs-and-harassment", "kys");
            added += Matching(readme, "ordinary", "slurs-and-harassment", "کیر");
            added += Matching(readme, "must-match", "custom-readme", "این پیام اسپم است");
            added += Matching(readme, "must-match", "custom-readme", "onlinecasino.example");
            added += Matching(readme, "must-match", Default, "kir and motherfucker", "#");

            var normalizer = Source(nameof(PersianNormalizerTests));
            added += Normalization(readme, "comparison", "كتاب‌هاي  ۱۲ ABC");
            added += Normalization(readme, "standard", "كتاب‌هاي  ۱۲ ABC");
            added += Normalization(normalizer, new JsonArray((JsonNode)"unifyLetters"), "كتاب‌هاي");
            added += Normalization(normalizer, "none", "كتاب‌هاي");
            foreach (var text in new[] { "بازي", "بازی", "كمك", "کمک", "سسسسلام", "BOOOOOK", "  سلام \t\n  دنیا  ", null, "   " })
            {
                added += Normalization(normalizer, "comparison", text);
            }

            added += Normalization(normalizer, "toPersianDigits", "2 ساعت پیش، KR1");
            added += Normalization(readme, "toPersianDigits", "2 ساعت پیش");
            added += Normalization(normalizer, "toAsciiDigits", "۲ ساعت پیش، KR١");
            added += Normalization(readme, "toAsciiDigits", "۱۴۰۴/۰۵/۱۴");

            added += Tokenization(normalizer, "سلام، دنیا! خوبی؟");
            added += Tokenization(normalizer, null);
            added += Tokenization(findMatches, "hello,fuck");
            added += Tokenization(findMatches, "کیر😂");

            JsonNode[] selections =
            [
                "default", "all",
                new JsonArray((JsonNode)"profanity"), new JsonArray((JsonNode)"sexual"), new JsonArray((JsonNode)"insult"),
                new JsonArray((JsonNode)"slur"), new JsonArray((JsonNode)"harassment"), new JsonArray((JsonNode)"mild"),
                new JsonArray((JsonNode)"slur", (JsonNode)"harassment"),
            ];
            foreach (var selection in selections)
            {
                added += Add("category-selection.json", defaultList, new JsonObject { ["kind"] = "category-selection", ["selection"] = selection });
            }

            foreach (var mask in new[] { "*", "#" })
            {
                added += Add("mask-validation.json", censor, new JsonObject { ["kind"] = "mask-validation", ["mask"] = mask });
            }

            return added;
        }

        // -----------------------------------------------------------------------------------------
        // Supplementary suite
        // -----------------------------------------------------------------------------------------

        private (int Included, int Disagreements, int Duplicates) SeedSupplementarySuite()
        {
            var filter = _corpus.FilterFor(Default);
            int included = 0, disagreements = 0, duplicates = 0;

            foreach (var (cat, text, bad, note) in SupplementarySuite)
            {
                // Only record what 1.2.0 already does; a known gap must never become an expectation.
                if (filter.ContainsProfanity(text) != bad)
                {
                    Console.WriteLine($"Suite disagreement skipped: [{cat}] \"{Corpus.ShowInvisible(text)}\" labelled {(bad ? "bad" : "clean")}");
                    disagreements++;
                    continue;
                }

                var file = cat.StartsWith("FA ", StringComparison.Ordinal) ? "matching-persian.json"
                    : cat.StartsWith("Finglish", StringComparison.Ordinal) ? "matching-finglish.json"
                    : cat.StartsWith("EN ", StringComparison.Ordinal) ? "matching-english.json"
                    : throw new InvalidOperationException($"Unknown suite category '{cat}'.");

                if (Add(file, "suite-" + Kebab(cat), MatchingCase(bad ? "must-match" : "ordinary", Default, text, [], note)) == 1)
                {
                    included++;
                }
                else
                {
                    duplicates++;
                }
            }

            return (included, disagreements, duplicates);
        }

        // -----------------------------------------------------------------------------------------
        // Building and adding cases
        // -----------------------------------------------------------------------------------------

        private int Matching(string source, string kind, string configuration, JsonNode? input, params string[] masks) =>
            Add(MatchingFile(kind, configuration, input), source, MatchingCase(kind, configuration, input, masks, note: null));

        private static JsonObject MatchingCase(string kind, string configuration, JsonNode? input, string[] masks, string? note)
        {
            var json = new JsonObject { ["kind"] = kind, ["configuration"] = configuration };
            if (!string.IsNullOrEmpty(note))
            {
                json["note"] = note;
            }

            if (masks.Length > 0)
            {
                json["masks"] = new JsonArray(masks.Select(m => (JsonNode?)m).ToArray());
            }

            json["input"] = input;
            return json;
        }

        private int Normalization(string source, JsonNode steps, string? input) =>
            Add("normalization.json", source, new JsonObject { ["kind"] = "normalization", ["steps"] = steps, ["input"] = input });

        private int Tokenization(string source, string? input) =>
            Add("tokenization.json", source, new JsonObject { ["kind"] = "tokenization", ["input"] = input });

        /// <summary>
        /// Robustness cases, and cases under another configuration, have files of their own. A
        /// must-match case goes with the list its match comes from; an ordinary one by its script.
        /// </summary>
        private string MatchingFile(string kind, string configuration, JsonNode? input)
        {
            if (kind == "robustness")
            {
                return "robustness.json";
            }

            if (configuration != Default)
            {
                return "matching-options.json";
            }

            var text = Corpus.BuildInput(input) ?? string.Empty;
            if (kind == "ordinary")
            {
                return text.Any(c => c >= '؀' && c <= 'ۿ') ? "matching-persian.json" : "matching-english.json";
            }

            var match = _corpus.FilterFor(Default).FindMatch(text)
                        ?? throw new InvalidOperationException($"\"{Corpus.ShowInvisible(text)}\" is seeded as must-match but does not match under default.");

            foreach (var (file, words) in _lists)
            {
                if (words.Any(w => w.Text == match.Word.Text && w.Mode == match.Word.Mode))
                {
                    return file;
                }
            }

            throw new InvalidOperationException($"The entry '{match.Word.Text}' is in none of the bundled lists.");
        }

        /// <summary>Appends a pending case unless its content is already in the corpus. Returns 1 when added.</summary>
        private int Add(string file, string source, JsonObject content)
        {
            var kind = content["kind"]!.GetValue<string>();
            if (!_keys.Add(ContentKey(kind, content)))
            {
                return 0;
            }

            if (!_files.TryGetValue(file, out var cases))
            {
                cases = [];
                _files.Add(file, cases);
            }

            var prefix = $"{file.Substring(0, file.Length - ".json".Length)}-{source}";
            var n = 1;
            while (_ids.Contains($"{prefix}-{n:000}"))
            {
                n++;
            }

            var json = new JsonObject { ["id"] = $"{prefix}-{n:000}" };
            foreach (var property in content.ToList())
            {
                content.Remove(property.Key);
                json[property.Key] = property.Value;
            }

            _ids.Add(json["id"]!.GetValue<string>());
            cases.Add(json);
            _changed.Add(file);
            return 1;
        }

        private static string ContentKey(string kind, JsonObject json) => kind switch
        {
            "ordinary" or "must-match" or "robustness" =>
                $"matching|{Field(json, "configuration")}|{InputKey(json["input"])}|"
                + string.Join("", (json["masks"] as JsonArray ?? []).Select(m => m?.GetValue<string>()).OrderBy(m => m, StringComparer.Ordinal)),
            "normalization" => $"normalization|{Canonical(json["steps"])}|{InputKey(json["input"])}",
            "tokenization" => $"tokenization|{InputKey(json["input"])}",
            "word-list-parsing" => $"word-list-parsing|{InputKey(json["text"])}",
            "category-selection" => $"category-selection|{Canonical(json["selection"])}",
            "mask-validation" => $"mask-validation|{InputKey(json["mask"])}",
            _ => throw new InvalidOperationException($"Unknown kind '{kind}'."),
        };

        private static string? Field(JsonObject json, string name) => (json[name] as JsonValue)?.GetValue<string>();

        /// <summary>A structural key: a string and a build object with the same text stay different inputs.</summary>
        private static string InputKey(JsonNode? input) => input switch
        {
            null => "null",
            JsonValue value => "string:" + value.GetValue<string>(),
            _ => "build:" + Canonical(input),
        };

        private static string Canonical(JsonNode? node) => node switch
        {
            null => "null",
            JsonArray array => "[" + string.Join(",", array.Select(Canonical)) + "]",
            JsonObject obj => "{" + string.Join(",", obj.Select(p => p.Key + ":" + Canonical(p.Value))) + "}",
            JsonValue value when value.GetValueKind() == System.Text.Json.JsonValueKind.String => "\"" + value.GetValue<string>() + "\"",
            _ => node.ToJsonString(),
        };

        private static JsonObject Build(params JsonObject[] parts) => new() { ["build"] = new JsonArray(parts.Cast<JsonNode?>().ToArray()) };

        private static JsonObject Text(string text) => new() { ["text"] = text };

        private static JsonObject Repeat(string text, int times) => new() { ["repeat"] = text, ["times"] = times };

        private static JsonObject Utf16(string hex) => new() { ["utf16"] = hex };

        /// <summary><c>Persian_suffixes_are_matched</c> → <c>persian-suffixes-are-matched</c>; <c>FindMatchesTests</c> → <c>find-matches-tests</c>.</summary>
        private static string Source(string name) => Kebab(name);

        /// <summary>Lowercase ASCII words joined by single hyphens; <c>FA evasion</c> → <c>fa-evasion</c>.</summary>
        private static string Kebab(string name)
        {
            var sb = new StringBuilder();
            for (var i = 0; i < name.Length; i++)
            {
                var c = name[i];
                if (c < 128 && char.IsLetterOrDigit(c))
                {
                    if (char.IsUpper(c) && i > 0 && char.IsLower(name[i - 1]))
                    {
                        sb.Append('-');
                    }

                    sb.Append(char.ToLowerInvariant(c));
                }
                else if (sb.Length > 0 && sb[sb.Length - 1] != '-')
                {
                    sb.Append('-');
                }
            }

            return sb.ToString().Trim('-');
        }
    }
}
