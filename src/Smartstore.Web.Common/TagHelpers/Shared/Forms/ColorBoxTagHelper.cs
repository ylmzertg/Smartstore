﻿using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Smartstore.Web.TagHelpers.Shared
{
    [OutputElementHint("div")]
    [HtmlTargetElement("colorbox", TagStructure = TagStructure.WithoutEndTag)]
    public class ColorBoxTagHelper : BaseFormTagHelper
    {
        const string DefaultColorAttributeName = "sm-default-color";

        [HtmlAttributeName(DefaultColorAttributeName)]
        public string DefaultColor { get; set; }

        protected override void ProcessCore(TagHelperContext context, TagHelperOutput output)
        {
            // TODO: (mh) (core) This is not how model expressions work. "value" is just the value,
            // but where is the name/id of the control? You can't just pass null to HtmlHelper.TextBox(),
            // you need a name. TBD with MC. Also: expecting an attribute named "value" without declaring a public property
            // for it in this class is really BAD API design!
            string value;
            if (output.Attributes.TryGetAttribute("value", out var valueAttribute))
            {
                value = valueAttribute.Value.ToString();
            }
            else
            {
                value = For != null ? For.Model.ToString() : string.Empty;
            }

            var defaultColor = DefaultColor.EmptyNull();
            var isDefault = value.EqualsNoCase(defaultColor);

            output.TagName = "div";
            output.TagMode = TagMode.StartTagAndEndTag;
            output.AppendCssClass("input-group colorpicker-component sm-colorbox");
            output.Attributes.Add("data-fallback-color", defaultColor);

            output.Content.AppendHtml(HtmlHelper.TextBox(For != null ? For.Name : string.Empty, 
                isDefault ? string.Empty : value, 
                new { @class = "form-control colorval", placeholder = defaultColor }));

            var addon = new TagBuilder("div");
            addon.AddCssClass("input-group-append input-group-addon");
            addon.InnerHtml.AppendHtml(
                $"<div class='input-group-text'><i class='thecolor' style='{(DefaultColor.HasValue() ? "background-color: " + DefaultColor : string.Empty)}'>&nbsp;</i></div>");

            output.Content.AppendHtml(addon);
        }
    }
}