namespace WebApplication.Worderizer.Models
{
    public class FormControlItem
    {
        public string Key { get; set; }

        public FormControlType Type { get; set; }

        public object? Value { get; set; }

        public string? FormatString { get; set; }

        public FormControlItem(string key, FormControlType type, object? value = null, string? formatString = null)
        {
            Key = key;
            Type = type;
            Value = value;
            FormatString = formatString;
        }
    }
}
