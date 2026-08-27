namespace ResoDrive.Core.Validation;

public static class RcloneArgumentTextCodec
{
    public static string Format(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var lines = new List<string>(arguments.Count);
        for (var index = 0; index < arguments.Count; index++)
        {
            var option = arguments[index];
            if (option.StartsWith("--", StringComparison.Ordinal) &&
                !option.Contains('=') &&
                index + 1 < arguments.Count &&
                !arguments[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                lines.Add($"{option}={arguments[++index]}");
            }
            else
            {
                lines.Add(option);
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    public static string[] Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Split(
            ['\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
