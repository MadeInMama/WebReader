namespace WebReader.Helpers;

public static class GlobalFunctions
{
    public static string FormatSize(ulong size)
    {
        if (size == 0)
            return "0B";

        var digits = size.ToString().Length;

        return digits switch
        {
            > 7 => size / 1024 / 1024 + "MB",
            > 4 => size / 1024 + "KB",
            _ => size + "B"
        };
    }

    public static string FormatSize(long size)
    {
        return FormatSize((ulong)size);
    }
}
