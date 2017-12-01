using Sitecore.Configuration;
using Sitecore.Data.Fields;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Sitecore.Pipelines.Save;
using Sitecore.Web;
using System;

namespace Sitecore.Support.Pipelines.Save
{
  /// <summary>
  /// Processor for saving data.
  /// </summary>
  public class Save
  {
    /// <summary>
    /// Runs the processor.
    /// </summary>
    /// <param name="args">
    /// The arguments.
    /// </param>
    public void Process(SaveArgs args)
    {
      SaveArgs.SaveItem[] items = args.Items;
      for (int i = 0; i < items.Length; i++)
      {
        SaveArgs.SaveItem saveItem = items[i];
        Item item = Context.ContentDatabase.Items[saveItem.ID, saveItem.Language, saveItem.Version];
        if (item != null)
        {
          if (item.Locking.IsLocked() && !item.Locking.HasLock() && !Context.User.IsAdministrator && !args.PolicyBasedLocking)
          {
            args.Error = "Could not modify locked item \"" + item.Name + "\"";
            args.AbortPipeline();
            return;
          }
          item.Editing.BeginEdit();
          SaveArgs.SaveField[] fields = saveItem.Fields;
          for (int j = 0; j < fields.Length; j++)
          {
            SaveArgs.SaveField saveField = fields[j];
            Field field = item.Fields[saveField.ID];
            if (field != null && (!field.IsBlobField || (!(field.TypeKey == "attachment") && !(saveField.Value == Translate.Text("[Blob Value]")))))
            {
              saveField.OriginalValue = field.Value;
              if (!(saveField.OriginalValue == saveField.Value))
              {
                if (!string.IsNullOrEmpty(saveField.Value))
                {
                  if (field.TypeKey == "rich text")
                  {
                    saveField.Value = saveField.Value.Replace("‘", "&lsquo;").Replace("’", "&rsquo;")
                      .Replace("’", "&rsquo;").Replace("“", "&ldquo;").Replace("”", "&rdquo;").Replace("„", "&bdquo;");
                    if (Settings.HtmlEditor.RemoveScripts)
                    {
                      saveField.Value = WebUtil.RemoveAllScripts(saveField.Value);
                    }
                  }
                  if (Save.NeedsHtmlTagEncode(saveField))
                  {
                    saveField.Value = saveField.Value.Replace("<", "&lt;").Replace(">", "&gt;");
                  }
                }
                field.Value = saveField.Value;
              }
            }
          }
          item.Editing.EndEdit();
          Log.Audit(this, "Save item: {0}", new string[]
          {
                        AuditFormatter.FormatItem(item)
          });
          args.SavedItems.Add(item);
        }
      }
      if (!Context.IsUnitTesting)
      {
        Context.ClientPage.Modified = false;
      }
      if (args.SaveAnimation)
      {
        Context.ClientPage.ClientResponse.Eval("var d = new scSaveAnimation('ContentEditor')");
        return;
      }
    }

    /// <summary>
    /// Defines if the html tags in the field value should be encoded.
    /// </summary>
    /// <param name="field">The save field</param>
    /// <returns><c>true</c> if the value should be encoded and <c>false</c> otherwise.</returns>
    private static bool NeedsHtmlTagEncode(SaveArgs.SaveField field)
    {
      return field.ID == FieldIDs.DisplayName;
    }
  }
}