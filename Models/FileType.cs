namespace WebReader.Models;

public enum FileType
{
    Pdf,
    ZipWithImg,
    Fb2
}

public static class FileTypeHelper
{
    public static readonly IDictionary<FileType, string> FileTypeNameDict = new Dictionary<FileType, string>();
    public static readonly IDictionary<string, FileType> FileTypeNameDictReverse = new Dictionary<string, FileType>();

    static FileTypeHelper()
    {
        FileTypeNameDict.Add(FileType.ZipWithImg, "zip");
        FileTypeNameDict.Add(FileType.Pdf, "pdf");
        FileTypeNameDict.Add(FileType.Fb2, "fb2");

        FileTypeNameDictReverse.Add("zip", FileType.ZipWithImg);
        FileTypeNameDictReverse.Add("pdf", FileType.Pdf);
        FileTypeNameDictReverse.Add("fb2", FileType.Fb2);
    }

    public static bool TryGetFileType(this string source, out FileType res)
    {
        return FileTypeNameDictReverse.TryGetValue(Path.GetExtension(source).Remove(0, 1), out res);
    }
}
