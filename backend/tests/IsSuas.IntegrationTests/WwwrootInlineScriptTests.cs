using System.Text.RegularExpressions;

namespace IsSuas.IntegrationTests;

/// <summary>
/// Celé UI je inline JavaScript vo wwwroot/index.html. Prehliadač pri syntaktickej chybe zahodí
/// celý &lt;script&gt;, takže UI prestane reagovať bez akejkoľvek hlášky — test preto kontroluje
/// vyváženosť zátvoriek a uzavretie literálov.
/// </summary>
public class WwwrootInlineScriptTests
{
    [Fact]
    public void IndexHtml_InlineScript_MaVyvazeneZatvorkyALiteraly()
    {
        var path = FindIndexHtml();
        var html = File.ReadAllText(path);
        var scripts = InlineScripts(html);

        Assert.NotEmpty(scripts);
        var problems = scripts.SelectMany(s => Scan(s.Code, s.FirstLine)).ToList();
        Assert.True(problems.Count == 0, $"{path}{Environment.NewLine}{string.Join(Environment.NewLine, problems)}");
    }

    [Fact]
    public void Kontrola_Najde_Prebytocnu_Zatvorku()
    {
        const string code = """
            function render(rows) {
              return '<div>' + rows + '</div>';
            }
            }
            """;

        var problems = Scan(code, 1);

        Assert.Contains("riadok 4", Assert.Single(problems));
    }

    [Fact]
    public void Kontrola_Nehlasi_Chybu_Pri_Regexoch_A_Literaloch()
    {
        const string code = """
            var re = /[{(]"'`/g;
            var text = 'zátvorka { v texte';
            var tpl = `hodnota ${ { a: 1 }.a } }`;
            var div = (10) / 2; // delenie, nie regex
            function f() { return /^\{$/.test(text); }
            """;

        Assert.Empty(Scan(code, 1));
    }

    private static string FindIndexHtml()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "backend", "src", "IsSuas.Api", "wwwroot", "index.html");
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException("Nenašiel som backend/src/IsSuas.Api/wwwroot/index.html.");
    }

    private static List<(int FirstLine, string Code)> InlineScripts(string html)
    {
        var scripts = new List<(int, string)>();
        foreach (Match match in Regex.Matches(html, @"<script(?<attrs>[^>]*)>(?<code>.*?)</script>", RegexOptions.Singleline))
        {
            if (match.Groups["attrs"].Value.Contains("src=", StringComparison.OrdinalIgnoreCase))
                continue;
            var code = match.Groups["code"];
            scripts.Add((html.AsSpan(0, code.Index).Count('\n') + 1, code.Value));
        }

        return scripts;
    }

    /// <summary>Znaky, po ktorých '/' začína regulárny výraz a nie delenie.</summary>
    private const string BeforeRegex = "(,=:[!&|?{};+-*%~^<>";

    private static readonly HashSet<string> KeywordsBeforeRegex = new(StringComparer.Ordinal)
    {
        "return", "typeof", "instanceof", "in", "of", "new", "delete", "void", "case", "do", "else", "yield", "await"
    };

    private static List<string> Scan(string code, int firstLine)
    {
        var problems = new List<string>();
        var open = new Stack<(char Bracket, int Line)>();
        var line = firstLine;
        var previous = string.Empty;
        var i = 0;

        while (i < code.Length)
        {
            var ch = code[i];

            if (ch == '\n')
            {
                line++;
                i++;
                continue;
            }

            if (char.IsWhiteSpace(ch))
            {
                i++;
                continue;
            }

            if (ch == '/' && i + 1 < code.Length && code[i + 1] == '/')
            {
                var end = code.IndexOf('\n', i);
                i = end < 0 ? code.Length : end;
                continue;
            }

            if (ch == '/' && i + 1 < code.Length && code[i + 1] == '*')
            {
                var end = code.IndexOf("*/", i + 2, StringComparison.Ordinal);
                if (end < 0)
                {
                    problems.Add($"riadok {line}: neuzavretý blokový komentár /*");
                    break;
                }

                line += code.AsSpan(i, end - i).Count('\n');
                i = end + 2;
                continue;
            }

            if (ch is '\'' or '"')
            {
                i = ReadQuoted(code, i, line, problems);
                previous = "literal";
                continue;
            }

            if (ch == '`')
            {
                i = ReadTemplate(code, i, ref line, problems);
                previous = "literal";
                continue;
            }

            if (ch == '/' && IsRegexStart(previous))
            {
                i = ReadRegex(code, i, line, problems);
                previous = "literal";
                continue;
            }

            if (ch is '{' or '(' or '[')
            {
                open.Push((ch, line));
            }
            else if (ch is '}' or ')' or ']')
            {
                var expected = ch switch { '}' => '{', ')' => '(', _ => '[' };
                if (open.Count == 0)
                {
                    problems.Add($"riadok {line}: prebytočná zátvorka '{ch}'");
                }
                else if (open.Peek().Bracket != expected)
                {
                    var (bracket, openedLine) = open.Pop();
                    problems.Add($"riadok {line}: '{ch}' nezodpovedá zátvorke '{bracket}' otvorenej na riadku {openedLine}");
                }
                else
                {
                    open.Pop();
                }
            }

            if (char.IsLetter(ch) || ch is '_' or '$')
            {
                var start = i;
                while (i < code.Length && (char.IsLetterOrDigit(code[i]) || code[i] is '_' or '$'))
                    i++;
                previous = code[start..i];
                continue;
            }

            previous = ch.ToString();
            i++;
        }

        foreach (var (bracket, openedLine) in open.OrderBy(x => x.Line))
            problems.Add($"riadok {openedLine}: neuzavretá zátvorka '{bracket}'");

        return problems;
    }

    private static bool IsRegexStart(string previous) =>
        previous.Length == 0
        || (previous.Length == 1 && BeforeRegex.Contains(previous[0]))
        || KeywordsBeforeRegex.Contains(previous);

    private static int ReadQuoted(string code, int i, int line, List<string> problems)
    {
        var quote = code[i];
        i++;
        while (i < code.Length)
        {
            var ch = code[i];
            if (ch == '\\')
            {
                i += 2;
                continue;
            }

            if (ch == '\n')
            {
                problems.Add($"riadok {line}: neuzavretý literál {quote}");
                return i;
            }

            if (ch == quote)
                return i + 1;
            i++;
        }

        problems.Add($"riadok {line}: neuzavretý literál {quote}");
        return i;
    }

    private static int ReadTemplate(string code, int i, ref int line, List<string> problems)
    {
        var startLine = line;
        i++;
        while (i < code.Length)
        {
            var ch = code[i];
            if (ch == '\\')
            {
                i += 2;
                continue;
            }

            if (ch == '\n')
            {
                line++;
                i++;
                continue;
            }

            if (ch == '`')
                return i + 1;

            if (ch == '$' && i + 1 < code.Length && code[i + 1] == '{')
            {
                var depth = 1;
                i += 2;
                while (i < code.Length && depth > 0)
                {
                    if (code[i] == '{') depth++;
                    else if (code[i] == '}') depth--;
                    else if (code[i] == '\n') line++;
                    i++;
                }

                continue;
            }

            i++;
        }

        problems.Add($"riadok {startLine}: neuzavretý template literál `");
        return i;
    }

    private static int ReadRegex(string code, int i, int line, List<string> problems)
    {
        i++;
        var inClass = false;
        while (i < code.Length)
        {
            var ch = code[i];
            if (ch == '\\')
            {
                i += 2;
                continue;
            }

            if (ch == '\n')
            {
                problems.Add($"riadok {line}: neuzavretý regex literál /");
                return i;
            }

            if (ch == '[') inClass = true;
            else if (ch == ']') inClass = false;
            else if (ch == '/' && !inClass) return i + 1;
            i++;
        }

        problems.Add($"riadok {line}: neuzavretý regex literál /");
        return i;
    }
}
