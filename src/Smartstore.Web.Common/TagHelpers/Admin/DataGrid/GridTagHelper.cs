﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Newtonsoft.Json;
using Smartstore.ComponentModel;
using Smartstore.Web.Rendering;

namespace Smartstore.Web.TagHelpers.Admin
{
    [Flags]
    public enum DataGridBorderStyle
    {
        Borderless = 0,
        VerticalBorders = 1 << 0,
        HorizontalBorders = 1 << 1,
        Grid = VerticalBorders | HorizontalBorders
    }
    
    [HtmlTargetElement("datagrid")]
    [RestrictChildren("columns", "datasource", "paging", "toolbar", "sorting")]
    public class GridTagHelper : SmartTagHelper
    {
        const string BorderAttributeName = "border-style";
        const string StripedAttributeName = "striped";
        const string HoverAttributeName = "hover";
        const string CondensedAttributeName = "condensed";
        const string AllowResizeAttributeName = "allow-resize";
        const string AllowRowSelectionAttributeName = "allow-row-selection";
        const string AllowColumnReorderingAttributeName = "allow-column-reordering";
        const string AllowEditAttributeName = "allow-edit";
        const string HideHeaderAttributeName = "hide-header";
        const string KeyMemberAttributeName = "key-member";
        const string MaxHeightAttributeName = "max-height";
        const string PreserveCommandStateAttributeName = "preserve-command-state";
        const string PreserveGridStateAttributeName = "preserve-grid-state";
        const string OnDataBindingAttributeName = "ondatabinding";
        const string OnDataBoundAttributeName = "ondatabound";
        const string OnRowSelectedAttributeName = "onrowselected";

        public override void Init(TagHelperContext context)
        {
            base.Init(context);
            context.Items[nameof(GridTagHelper)] = this;
        }

        #region Public properties

        /// <summary>
        /// DataGrid table border style. Default: <see cref="DataGridBorderStyle.VerticalBorders"/>.
        /// </summary>
        [HtmlAttributeName(BorderAttributeName)]
        public DataGridBorderStyle BorderStyle { get; set; } = DataGridBorderStyle.VerticalBorders;

        /// <summary>
        /// Adds zebra-striping to any table row within tbody.
        /// </summary>
        [HtmlAttributeName(StripedAttributeName)]
        public bool Striped { get; set; }

        /// <summary>
        /// Enables a hover state on table rows within tbody.
        /// </summary>
        [HtmlAttributeName(HoverAttributeName)]
        public bool Hover { get; set; }

        ///// <summary>
        ///// Makes data table more compact by cutting cell padding in half.
        ///// </summary>
        //[HtmlAttributeName(CondensedAttributeName)]
        //public bool Condensed { get; set; }

        /// <summary>
        /// Allows resizing of single columns. Default: <c>false</c>.
        /// </summary>
        [HtmlAttributeName(AllowResizeAttributeName)]
        public bool AllowResize { get; set; }

        /// <summary>
        /// Allows selection of rows via pinned checkboxes on the left side. Default: <c>false</c>.
        /// </summary>
        [HtmlAttributeName(AllowRowSelectionAttributeName)]
        public bool AllowRowSelection { get; set; }

        /// <summary>
        /// Allows reordering of columns via drag & drop. Default: <c>false</c>.
        /// </summary>
        [HtmlAttributeName(AllowColumnReorderingAttributeName)]
        public bool AllowColumnReordering { get; set; }

        /// <summary>
        /// Allows inline editing of rows via double click. Default: <c>false</c>.
        /// </summary>
        [HtmlAttributeName(AllowEditAttributeName)]
        public bool AllowEdit { get; set; }

        /// <summary>
        /// Whether to hide data table header. Default: <c>false</c>.
        /// </summary>
        [HtmlAttributeName(HideHeaderAttributeName)]
        public bool HideHeader { get; set; }

        /// <summary>
        /// Key member expression. If <c>null</c>, any property named <c>Id</c> will be key member.
        /// </summary>
        [HtmlAttributeName(KeyMemberAttributeName)]
        public ModelExpression KeyMember { get; set; }

        /// <summary>
        /// Maximum height of grid element including toolbar, pager, header etc. Expressed as CSS max-height expression. Default: <c>initial</c>.
        /// </summary>
        [HtmlAttributeName(MaxHeightAttributeName)]
        public string MaxHeight { get; set; }

        /// <summary>
        /// Preserves command state of data grid across requests, but only within current session. 
        /// The state key is varied by current route identifier / URL.
        /// Data is saved on the server side. Default: <c>true</c>.
        /// </summary>
        [HtmlAttributeName(PreserveCommandStateAttributeName)]
        public bool PreserveCommandState { get; set; } = true;

        /// <summary>
        /// Preserves local state of data grid across requests. 
        /// Data is saved on the client side (localStorage). Default: <c>true</c>.
        /// </summary>
        [HtmlAttributeName(PreserveGridStateAttributeName)]
        public bool PreserveGridState { get; set; } = true;

        /// <summary>
        /// Name of Javascript function to call before data binding.
        /// Function parameters: <c>this</c> = Grid component instance, <c>command</c>.
        /// </summary>
        [HtmlAttributeName(OnDataBindingAttributeName)]
        public string OnDataBinding { get; set; }

        /// <summary>
        /// Name of Javascript function to call after data has been bound successfully.
        /// Function parameters: <c>this</c> = Grid component instance, <c>command</c>, <c>rows</c>.
        /// </summary>
        [HtmlAttributeName(OnDataBoundAttributeName)]
        public string OnDataBound { get; set; }

        /// <summary>
        /// Name of Javascript function to call after a rows has been (un)selected.
        /// Function parameters: <c>this</c> = Grid component instance, <c>selectedRows</c>, <c>row</c>, <c>selected</c>.
        /// </summary>
        [HtmlAttributeName(OnRowSelectedAttributeName)]
        public string OnRowSelected { get; set; }

        #endregion

        #region Internal properties

        [HtmlAttributeNotBound]
        internal GridDataSourceTagHelper DataSource { get; set; }

        [HtmlAttributeNotBound]
        internal GridPagingTagHelper Paging { get; set; }

        [HtmlAttributeNotBound]
        internal GridSortingTagHelper Sorting { get; set; }

        [HtmlAttributeNotBound]
        internal GridFilteringTagHelper Filtering { get; set; }

        [HtmlAttributeNotBound]
        internal GridToolbarTagHelper Toolbar { get; set; }

        [HtmlAttributeNotBound]
        internal List<GridColumnTagHelper> Columns { get; set; }

        [HtmlAttributeNotBound]
        internal GridDetailViewTagHelper DetailView { get; set; }

        [HtmlAttributeNotBound]
        internal string KeyMemberName 
        {
            get => KeyMember?.Metadata?.Name ?? "Id";
        }

        #endregion

        protected override async Task ProcessCoreAsync(TagHelperContext context, TagHelperOutput output)
        {
            await output.LoadAndSetChildContentAsync();

            if (Columns == null || Columns.Count == 0)
            {
                throw new InvalidOperationException("At least one column must be specified in order for the grid to render correctly.");
            }

            output.TagName = "div";
            output.AppendCssClass("datagrid-root");

            var component = new TagBuilder("sm-data-grid");
            component.Attributes[":options"] = "options";
            component.Attributes[":data-source"] = "dataSource";
            component.Attributes[":columns"] = "columns";
            component.Attributes[":paging"] = "paging";
            component.Attributes[":sorting"] = "sorting";

            // Generate detail-view slot
            if (DetailView?.Template?.IsEmptyOrWhiteSpace == false)
            {
                var slot = new TagBuilder("template");
                slot.Attributes["v-slot:detailview"] = "item";
                slot.InnerHtml.AppendHtml(DetailView.Template);
                component.InnerHtml.AppendHtml(slot);
            }

            // Generate toolbar slot
            if (Toolbar?.Template?.IsEmptyOrWhiteSpace == false)
            {
                var toolbar = new TagBuilder("div");
                toolbar.Attributes["class"] = "dg-toolbar d-flex flex-nowrap";
                toolbar.InnerHtml.AppendHtml(Toolbar.Template);

                var slot = new TagBuilder("template");
                slot.Attributes["v-slot:toolbar"] = "grid";
                slot.InnerHtml.AppendHtml(toolbar);
                component.InnerHtml.AppendHtml(slot);
            }

            // Generate template slots
            foreach (var column in Columns)
            {
                if (column.DisplayTemplate?.IsEmptyOrWhiteSpace == false)
                {
                    var displaySlot = new TagBuilder("template");
                    displaySlot.Attributes["v-slot:display-" + column.NormalizedMemberName] = "item";
                    displaySlot.InnerHtml.AppendHtml(column.DisplayTemplate);
                    component.InnerHtml.AppendHtml(displaySlot);
                }

                if (AllowEdit && !column.ReadOnly && (column.EditTemplate == null || column.EditTemplate.IsEmptyOrWhiteSpace))
                {
                    var editorSlot = new TagBuilder("template");
                    editorSlot.Attributes["v-slot:edit-" + column.NormalizedMemberName] = "item";

                    if (column.EditTemplate?.IsEmptyOrWhiteSpace == false)
                    {
                        editorSlot.InnerHtml.AppendHtml(column.EditTemplate);
                    }
                    else
                    {
                        // No custom edit template specified
                        editorSlot.InnerHtml.AppendHtml(HtmlHelper.EditorFor(column.For));
                        //editorSlot.InnerHtml.AppendHtml(HtmlHelper.ValidationMessageFor(column.For));
                    }
                    
                    component.InnerHtml.AppendHtml(editorSlot);
                }
            }

            output.Content.AppendHtml(component);

            output.PostElement.AppendHtmlLine(@$"<script>$(function() {{ window['{Id}'] = new Vue({GenerateVueJson()}); }})</script>");
        }

        private string GenerateVueJson()
        {
            var dict = new Dictionary<string, object>
            {
                { "el", "#" + Id }
            };

            var data = new
            {
                options = new
                {
                    vborders = BorderStyle.HasFlag(DataGridBorderStyle.VerticalBorders),
                    hborders = BorderStyle.HasFlag(DataGridBorderStyle.HorizontalBorders),
                    striped = Striped,
                    hover = Hover,
                    keyMemberName = KeyMemberName,
                    allowResize = AllowResize,
                    allowRowSelection = AllowRowSelection,
                    allowColumnReordering = AllowColumnReordering,
                    allowEdit = AllowEdit,
                    hideHeader = HideHeader,
                    maxHeight = MaxHeight,
                    stateKey = Id,
                    preserveState = PreserveGridState,
                    onDataBinding = OnDataBinding,
                    onDataBound = OnDataBound,
                    onRowSelected = OnRowSelected
                },
                dataSource = DataSource?.ToPlainObject(),
                columns = Columns.Select(c => c.ToPlainObject()).ToList(),
                paging = Paging?.ToPlainObject() ?? new { },
                sorting = Sorting?.ToPlainObject() ?? new { },
                filtering = Filtering?.ToPlainObject() ?? new { },
            };

            dict["data"] = data;

            var json = JsonConvert.SerializeObject(dict, new JsonSerializerSettings
            {
                ContractResolver = SmartContractResolver.Instance,
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.None
            });

            return json;
        }
    }
}
