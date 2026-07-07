namespace WebReader.Models;

public enum FileType
{
    Pdf,
    ZipWithImg,
    Fb2
}

public enum ImageType
{
    Jpg,
    Jpeg,
    Png
}

public static class CoverHelper
{
    public static string GetCoverNameByFileType(FileType? type)
    {
        switch (type)
        {
            case FileType.ZipWithImg:
                return "default_manga_cover.jpg";
            case FileType.Pdf:
            case FileType.Fb2:
            case null:
            default:
                return "default_cover.png";
        }
    }
}

public static class TypeHelper
{
    public static readonly IDictionary<FileType, string> FileTypeNameDict = new Dictionary<FileType, string>();
    public static readonly IDictionary<string, FileType> FileTypeNameDictReverse = new Dictionary<string, FileType>();

    public static readonly IDictionary<ImageType, string> ImgTypeNameDict = new Dictionary<ImageType, string>();
    public static readonly IDictionary<string, ImageType> ImgTypeNameDictReverse = new Dictionary<string, ImageType>();

    static TypeHelper()
    {
        FileTypeNameDict.Add(FileType.ZipWithImg, "zip");
        FileTypeNameDict.Add(FileType.Pdf, "pdf");
        FileTypeNameDict.Add(FileType.Fb2, "fb2");

        FileTypeNameDictReverse.Add("zip", FileType.ZipWithImg);
        FileTypeNameDictReverse.Add("pdf", FileType.Pdf);
        FileTypeNameDictReverse.Add("fb2", FileType.Fb2);

        ImgTypeNameDict.Add(ImageType.Jpg, "jpg");
        ImgTypeNameDict.Add(ImageType.Jpeg, "jpeg");
        ImgTypeNameDict.Add(ImageType.Png, "png");

        ImgTypeNameDictReverse.Add("jpg", ImageType.Jpg);
        ImgTypeNameDictReverse.Add("jpeg", ImageType.Jpeg);
        ImgTypeNameDictReverse.Add("png", ImageType.Png);
    }

    public static bool TryGetFileType(this string source, out FileType res)
    {
        return FileTypeNameDictReverse.TryGetValue(Path.GetExtension(source).Remove(0, 1), out res);
    }

    public static bool TryGetImgType(this string source, out ImageType res)
    {
        return ImgTypeNameDictReverse.TryGetValue(Path.GetExtension(source).Remove(0, 1), out res);
    }
}
