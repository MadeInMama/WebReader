namespace WebReader.Models.Ui;

public class MultiSelectOption
{
    public string Value { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string? Group { get; set; }
    public bool Selected { get; set; }
}
