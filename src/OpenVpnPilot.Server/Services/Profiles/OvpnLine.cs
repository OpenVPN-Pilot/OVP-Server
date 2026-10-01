using System.Text;

namespace OpenVpnPilot.Server.Services.Profiles;

// One line of a configuration and where it sits in the text, so a line can be rewritten without
// touching the bytes around it.
public readonly record struct OvpnLine(int Start, string Text)
{
    // Ends a line at \r\n, \n or a lone \r, as StringReader.ReadLine does in the client's parser.
    public static List<OvpnLine> Split(string text)
    {
        List<OvpnLine> lines = [];
        int start = 0;
        for (int index = 0; index < text.Length; index++)
        {
            if (text[index] is not ('\r' or '\n'))
            {
                continue;
            }

            lines.Add(new OvpnLine(start, text[start..index]));
            if (text[index] == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
            {
                index++;
            }

            start = index + 1;
        }

        if (start < text.Length)
        {
            lines.Add(new OvpnLine(start, text[start..]));
        }

        return lines;
    }

    // Splits a directive into words. OpenVPN allows single and double quoted arguments so that paths may
    // contain spaces; the quotes themselves are not part of the word.
    public static List<string> Tokenize(string line)
    {
        List<string> tokens = [];
        StringBuilder current = new();
        char quote = '\0';
        bool hasToken = false;

        foreach (char character in line)
        {
            if (quote != '\0')
            {
                if (character == quote)
                {
                    quote = '\0';
                }
                else
                {
                    current.Append(character);
                }
            }
            else if (character is '"' or '\'')
            {
                quote = character;
                hasToken = true;
            }
            else if (char.IsWhiteSpace(character))
            {
                if (hasToken)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                    hasToken = false;
                }
            }
            else
            {
                current.Append(character);
                hasToken = true;
            }
        }

        if (hasToken)
        {
            tokens.Add(current.ToString());
        }

        return tokens;
    }

    // Puts the replacement in place of each line's content, keeping its indentation and line ending.
    public static string Replace(string text, IReadOnlyList<OvpnLine> lines, string replacement)
    {
        if (lines.Count == 0)
        {
            return text;
        }

        StringBuilder result = new(text.Length);
        int copied = 0;
        foreach (OvpnLine line in lines)
        {
            int indent = line.Text.Length - line.Text.TrimStart().Length;
            result.Append(text, copied, line.Start + indent - copied).Append(replacement);
            copied = line.Start + line.Text.Length;
        }

        return result.Append(text, copied, text.Length - copied).ToString();
    }
}
