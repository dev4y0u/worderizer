using Syncfusion.DocIO.DLS;

namespace WebApplication.Worderizer.Models
{
    public class CustomFormControl
    {
        public IEntity ContentControl { get; set; }

        public ContentControlProperties ContentControlProperties { get; set; }

        public WTextRange? TextRange { get; set; }

        public WPicture? Picture { get; set; }

        public CustomFormControl(IEntity contentControl, ContentControlProperties contentControlProperties, WTextRange? textRange = null, WPicture? picture = null)
        {
            ContentControl = contentControl;
            ContentControlProperties = contentControlProperties;
            TextRange = textRange;
            Picture = picture;
        }
    }
}
