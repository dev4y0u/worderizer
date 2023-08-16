using Microsoft.AspNetCore.Mvc;
using Syncfusion.DocIO;
using Syncfusion.DocIO.DLS;
using Syncfusion.Drawing;
using WebApplication.Worderizer.Models;

namespace WebApplication.Worderizer.Controllers
{
    [ApiController]
    [Route("[controller]/[action]")]
    public class SyncfusionController : ControllerBase
    {
        private readonly ILogger<SyncfusionController> _logger;

        public SyncfusionController(ILogger<SyncfusionController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        public async Task<byte[]> FillWordForm(string wordDocTemplatePath, IEnumerable<FormControlItem> models)
        {
            using var resultStream = new MemoryStream();
            using var doc = new WordDocument();
            using (var wordDocTemplate = new FileStream(wordDocTemplatePath, FileMode.Open))
            {
                if (wordDocTemplate == null)
                    throw new ArgumentNullException(nameof(wordDocTemplate));

                if (models == null)
                    throw new ArgumentNullException(nameof(models));

                doc.Open(wordDocTemplate, FormatType.Docx);
            }

            var controls = await GetFormControls(doc);
            await FillFormControls(controls.ToList(), models.ToList());

            doc.Save(resultStream, FormatType.Docx);
            return resultStream.ToArray();
        }

        private async Task<IEnumerable<CustomFormControl>> GetFormControls(object entity)
        {
            var collection = new List<object>();

            if (entity is WordDocument wordDocument)
                collection = wordDocument.Sections.Cast<object>().ToList();
            else if (entity is WSection wSection)
                collection = wSection.WidgetInnerCollection.Cast<object>()
                    .Concat(wSection.HeadersFooters.Cast<object>())
                    .ToList();
            else if (entity is HeaderFooter headerFooter)
                collection = headerFooter.WidgetInnerCollection.Cast<object>().ToList();
            else if (entity is WParagraph wParagraph)
                collection = wParagraph.ChildEntities.Cast<object>().ToList();
            else if (entity is BlockContentControl blockContentControl)
                collection = blockContentControl.ChildEntities.Cast<object>().ToList();
            else if (entity is WTable wTable)
                collection = wTable.ChildEntities.Cast<object>().ToList();
            else if (entity is WTableRow wTableRow)
                collection = wTableRow.ChildEntities.Cast<object>().ToList();
            else if (entity is WTableCell wTableCell)
                collection = wTableCell.ChildEntities.Cast<object>().ToList();
            else if (entity is WTextBox wTextBox)
                collection = wTextBox.ChildEntities.Cast<object>().ToList();
            else if (entity is ParagraphItemCollection paragraphItemCollection)
                collection = paragraphItemCollection.Cast<object>().ToList();
            else if (entity is EntityCollection entityCollection)
                collection = entityCollection.Cast<object>().ToList();
            else if (entity is IInlineContentControl inlineContentControl)
                collection = inlineContentControl.ParagraphItems.Cast<object>().ToList();

            var result = new List<CustomFormControl>();
            result.AddRange(collection.Where(x => x is IInlineContentControl).Cast<IInlineContentControl>().Select(inlineCtrl => new CustomFormControl
            (
                inlineCtrl,
                inlineCtrl.ContentControlProperties,
                inlineCtrl.ParagraphItems?.Count > 0 ? inlineCtrl.ParagraphItems.FirstItem as WTextRange : null,
                inlineCtrl.ParagraphItems?.Count > 0 ? inlineCtrl.ParagraphItems.FirstItem as WPicture : null
            )));
            result.AddRange(collection.Where(x => x is IBlockContentControl).Cast<IBlockContentControl>().Select(blockCtrl => new CustomFormControl
            (
                blockCtrl,
                blockCtrl.ContentControlProperties,
                blockCtrl.TextBody?.Paragraphs?.Count > 0 ? blockCtrl.TextBody?.Paragraphs[0].ChildEntities.FirstItem as WTextRange : null,
                blockCtrl.TextBody?.Paragraphs?.Count > 0 ? blockCtrl.TextBody?.Paragraphs[0].ChildEntities.FirstItem as WPicture : null
            )));

            foreach (var item in collection)
            {
                result.AddRange(await GetFormControls(item));
            }

            return result;
        }

        private async Task FillFormControls(IList<CustomFormControl> controls, IList<FormControlItem> models)
        {
            foreach (var model in models)
            {
                try
                {
                    foreach (var selectedControl in controls.Where(x => x.ContentControlProperties.Title.ToLowerInvariant() == model.Key.ToLowerInvariant()))
                    { 
                        await FillFormControl(selectedControl, model, models); 
                    }
                }
                catch (Exception e)
                {
                    _logger.LogWarning(e, $"Error filling the form field {model.Key}");
                }
            }
        }

        private async Task FillFormControl(CustomFormControl control, FormControlItem model, IList<FormControlItem> models)
        {
            if (control == null || model == null)
                return;

            var controlType = control.ContentControlProperties.Type;

            switch (model.Type)
            {
                case FormControlType.Checkbox:
                    if (model.Value is bool boolValue)
                        control.ContentControlProperties.IsChecked = boolValue;
                    break;
                case FormControlType.Text:
                    if (control.TextRange != null)
                        control.TextRange.Text = !string.IsNullOrEmpty(model.FormatString) ? string.Format(model.FormatString, model.Value) : model.Value?.ToString() ?? string.Empty;
                    break;
                case FormControlType.Image:
                    if (model.Value is string str)
                        model.Value = Convert.FromBase64String(str);
                    if (model.Value is byte[] bytes && controlType == ContentControlType.Picture && control.Picture != null)
                        FillPicture(control.Picture, bytes);
                    break;
                case FormControlType.Container:
                    if (model.Value is int l)
                        await MultiplyContainer(control, l, models);
                    break;
                default:
                    _logger.LogWarning($"Unexpected {nameof(FormControlType)} {model.Type}");
                    break;
            }
        }

        private static void FillPicture(WPicture control, byte[] data)
        {
            var image = Image.FromStream(new MemoryStream(data));

            var scaleWidth = control.Width < image.Width ? control.Width / image.Width : 1;
            var scaleHeight = control.Height < image.Height ? control.Height / image.Height : 1;
            var scaleFactor = Math.Min(scaleWidth, scaleHeight);

            control.LoadImage(data);
            control.Height = image.Height * scaleFactor;
            control.Width = image.Width * scaleFactor;
        }

        private async Task MultiplyContainer(CustomFormControl control, long repetitions, IList<FormControlItem> models)
        {
            var cloneTarget = (IEntity?)GetParentRecursive<WTableRow>(control.ContentControl) ?? GetParentRecursive<WParagraph>(control.ContentControl);
            if (cloneTarget == null)
                return;

            var parent = GetParentRecursive<ICompositeEntity>(cloneTarget.Owner);
            if (parent == null)
                return;

            var newItems = new List<IEntity>();
            IEnumerable<CustomFormControl> controls;
            for (var i = 1; i < repetitions; i++)
            {
                var cloned = cloneTarget.Clone();
                controls = await GetFormControls(cloned);
                await ProcessClonedControls(controls.ToList(), i, models);
                newItems.Add(cloned);
            }

            controls = await GetFormControls(cloneTarget);
            await ProcessClonedControls(controls.ToList(), 0, models);

            foreach (var item in newItems)
                parent.ChildEntities.Add(item);
        }

        private TParent? GetParentRecursive<TParent>(IEntity baseEntity) where TParent : class, IEntity
        {
            if (baseEntity is TParent parent)
                return parent;
            if (baseEntity is WordDocument || baseEntity == null)
                return null;
            return GetParentRecursive<TParent>(baseEntity.Owner);
        }

        private async Task ProcessClonedControls(IList<CustomFormControl> controls, int idx, IList<FormControlItem> models)
        {
            foreach (var control in controls)
            {
                control.ContentControlProperties.Title = control.ContentControlProperties.Title + idx;
            }

            await FillFormControls(controls, models);
        }
    }
}