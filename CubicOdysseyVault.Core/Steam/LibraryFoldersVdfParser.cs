using System.Text;

namespace CubicOdysseyVault.Core.Steam;

internal static class LibraryFoldersVdfParser
{
    public static IReadOnlyList<string> ParseLibraryPaths(string vdfText)
    {
        if (string.IsNullOrWhiteSpace(vdfText)) return Array.Empty<string>();

        using var tokens = Tokenize(vdfText).GetEnumerator();
        if (!tokens.MoveNext()) return Array.Empty<string>();

        if (tokens.Current.Kind != TokenKind.String ||
            !string.Equals(tokens.Current.Value, "libraryfolders", StringComparison.OrdinalIgnoreCase))
        {
            return Array.Empty<string>();
        }

        if (!tokens.MoveNext() || tokens.Current.Kind != TokenKind.OpenBrace)
            throw new FormatException("Expected '{' after 'libraryfolders' key.");

        var paths = new List<string>();
        ParseLibraryFoldersObject(tokens, paths);
        return paths;
    }

    public static IReadOnlyList<string> ParseLibraryPathsFromFile(string path) =>
        ParseLibraryPaths(File.ReadAllText(path));

    private static void ParseLibraryFoldersObject(IEnumerator<Token> tokens, List<string> paths)
    {
        while (tokens.MoveNext())
        {
            switch (tokens.Current.Kind)
            {
                case TokenKind.CloseBrace:
                    return;
                case TokenKind.String:
                    break;
                default:
                    throw new FormatException($"Expected string key at line {tokens.Current.Line}.");
            }

            if (!tokens.MoveNext())
                throw new FormatException("Unexpected end of input after key.");

            if (tokens.Current.Kind == TokenKind.OpenBrace)
            {
                var path = ExtractPathFromLibraryBlock(tokens);
                if (!string.IsNullOrWhiteSpace(path))
                    paths.Add(path);
            }
            else if (tokens.Current.Kind != TokenKind.String)
            {
                throw new FormatException($"Expected '{{' or string value at line {tokens.Current.Line}.");
            }
        }

        throw new FormatException("Unexpected end of input — missing closing brace for 'libraryfolders'.");
    }

    private static string? ExtractPathFromLibraryBlock(IEnumerator<Token> tokens)
    {
        string? path = null;
        int depth = 1;

        while (tokens.MoveNext())
        {
            if (tokens.Current.Kind == TokenKind.CloseBrace)
            {
                depth--;
                if (depth == 0) return path;
                continue;
            }

            if (tokens.Current.Kind == TokenKind.OpenBrace)
            {
                depth++;
                continue;
            }

            if (tokens.Current.Kind != TokenKind.String) continue;

            string key = tokens.Current.Value;
            if (!tokens.MoveNext())
                throw new FormatException("Unexpected end of input after key.");

            if (tokens.Current.Kind == TokenKind.OpenBrace)
            {
                depth++;
                continue;
            }

            if (tokens.Current.Kind != TokenKind.String)
                throw new FormatException($"Expected string value after key '{key}' at line {tokens.Current.Line}.");

            if (depth == 1 && string.Equals(key, "path", StringComparison.OrdinalIgnoreCase))
                path = tokens.Current.Value;
        }

        throw new FormatException("Unexpected end of input — missing closing brace.");
    }

    private enum TokenKind { String, OpenBrace, CloseBrace }
    private readonly record struct Token(TokenKind Kind, string Value, int Line);

    private static IEnumerable<Token> Tokenize(string src)
    {
        int line = 1;
        int i = 0;
        while (i < src.Length)
        {
            char c = src[i];

            if (c == '\n') { line++; i++; continue; }
            if (char.IsWhiteSpace(c)) { i++; continue; }

            if (c == '/' && i + 1 < src.Length && src[i + 1] == '/')
            {
                while (i < src.Length && src[i] != '\n') i++;
                continue;
            }

            if (c == '{') { yield return new Token(TokenKind.OpenBrace, "{", line); i++; continue; }
            if (c == '}') { yield return new Token(TokenKind.CloseBrace, "}", line); i++; continue; }

            if (c == '"')
            {
                int startLine = line;
                int j = i + 1;
                var sb = new StringBuilder();
                while (j < src.Length)
                {
                    char d = src[j];
                    if (d == '\\' && j + 1 < src.Length)
                    {
                        char next = src[j + 1];
                        sb.Append(next switch
                        {
                            '\\' => '\\',
                            '"' => '"',
                            'n' => '\n',
                            't' => '\t',
                            _ => next,
                        });
                        j += 2;
                        continue;
                    }
                    if (d == '"') break;
                    if (d == '\n') line++;
                    sb.Append(d);
                    j++;
                }
                if (j >= src.Length)
                    throw new FormatException($"Unterminated string starting at line {startLine}.");
                yield return new Token(TokenKind.String, sb.ToString(), startLine);
                i = j + 1;
                continue;
            }

            throw new FormatException($"Unexpected character '{c}' at line {line}.");
        }
    }
}
