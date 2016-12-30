﻿#region header
// ========================================================================
// Copyright (c) 2016 - Julien Caillon (julien.caillon@gmail.com)
// This file (YamuiFilteredTypeTreeList.cs) is part of YamuiFramework.
// 
// YamuiFramework is a free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// YamuiFramework is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with YamuiFramework. If not, see <http://www.gnu.org/licenses/>.
// ========================================================================
#endregion
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using YamuiFramework.Fonts;
using YamuiFramework.Themes;

namespace YamuiFramework.Controls.YamuiList {

    /// <summary>
    /// This class is the most complicated, obviously
    /// The difficulty being handling the filter string correctly;
    /// basically, we have two separate modes (see SearchMode)
    /// This is not the conventionnal tree searching but this makes more sense to me
    /// </summary>
    public class YamuiFilteredTypeTreeList : YamuiFilteredTypeList {

        #region private fields

        /// <summary>
        /// Allows to save the expansion state for each node of the tree
        /// </summary>
        private Dictionary<string, bool> _savedState = new Dictionary<string, bool>();

        /// <summary>
        /// Root items passed to the SetItems method
        /// </summary>
        protected List<FilteredTypeTreeListItem> _treeRootItems;

        private SearchModeOption _searchMode;

        /// <summary>
        /// True when in FilterSortWithNoParent mode + filter string not empty
        /// </summary>
        private bool _isSearching;

        private bool _showTreeBranches = true;

        #endregion

        #region public properties

        /// <summary>
        /// Two modes can be active when the filter string isn't empty :
        /// - either we filter the item + we include their parent and still display the list as a tree
        /// - or we filter + sort and display the list as a simple filtered list with types
        /// </summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public SearchModeOption SearchMode {
            get { return _searchMode; }
            set {
                _searchMode = value;
                FilterString = FilterString;
            }
        }

        /// <summary>
        /// Set this to filter the list with the given text
        /// </summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public override string FilterString {
            get { return _filterString; }
            set {
                _filterString = value.ToLower().Trim();
                if (SetIsSearching(value, SearchMode))
                    // base.FilterString = value; is done in SetIsSearching
                    return;
                base.FilterString = value;
            }
        }

        /// <summary>
        /// If true, will display the lines representing the tree branches
        /// </summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool ShowTreeBranches {
            get { return _showTreeBranches; }
            set { _showTreeBranches = value; }
        }

        #endregion

        #region Enum

        private enum ForceExpansion {
            Idle,
            ForceExpand,
            ForceCollapse
        }

        /// <summary>
        /// Two modes can be active when the filter string isn't empty :
        /// - either we filter the item + we include their parent and still display the list as a tree
        /// - or we filter + sort and display the list as a simple filtered list with types
        /// </summary>
        public enum SearchModeOption {
            FilterSortWithNoParent,
            FilterOnlyAndIncludeParent,
        }

        #endregion

        #region Set

        /// <summary>
        /// Set the items that will be displayed in the list
        /// </summary>
        public override void SetItems(List<ListItem> listItems) {

            var firstItem = listItems.FirstOrDefault();
            if (firstItem != null && !(firstItem is FilteredTypeTreeListItem))
                throw new Exception("listItems shoud contain objects of type FilteredTypeItem");

            _treeRootItems = listItems.Cast<FilteredTypeTreeListItem>().ToList();

            if (_isSearching)
                base.SetItems(GetFullItemsList(_treeRootItems));
            else
                base.SetItems(GetExpandedItemsList(_treeRootItems, ForceExpansion.Idle));
        }

        /// <summary>
        /// Returns the full list of items to be displayed, taking into account expanded items
        /// </summary>
        private List<ListItem> GetExpandedItemsList(List<FilteredTypeTreeListItem> list, ForceExpansion forceExpansion) {

            if (list == null)
                return null;

            var outList = new List<ListItem>();

            foreach (var item in list) {
                outList.Add(item);
                var descriptor = item.PathDescriptor;

                if (item.CanExpand) {

                    // force expand/collapse?
                    if (forceExpansion != ForceExpansion.Idle) {
                        if (_savedState.ContainsKey(descriptor))
                            _savedState[descriptor] = forceExpansion == ForceExpansion.ForceExpand;
                        else
                            _savedState.Add(descriptor, forceExpansion == ForceExpansion.ForceExpand);
                    }

                    // restore the expand state of the item if needed
                    if (_savedState.ContainsKey(descriptor))
                        item.IsExpanded = _savedState[descriptor];

                    if (item.IsExpanded) {
                        var children = GetExpandedItemsList(item.GetItemChildren(), forceExpansion);
                        if (children != null)
                            outList.AddRange(children);
                    }
                }
            }

            return outList;
        }

        /// <summary>
        /// Returns the full list of items in the tree
        /// </summary>
        private List<ListItem> GetFullItemsList(List<FilteredTypeTreeListItem> list) {
            if (list == null)
                return null;
            var outList = new List<ListItem>();
            foreach (var item in list) {
                outList.Add(item);
                if (item.CanExpand) {
                    var children = GetFullItemsList(item.GetItemChildren());
                    if (children != null)
                        outList.AddRange(children);
                }
            }
            return outList;
        }

        #endregion

        #region Expand/Retract

        /// <summary>
        /// Call this method to expand the whole tree
        /// </summary>
        public void ForceAllToExpand() {
            if (!_isSearching)
                base.SetItems(GetExpandedItemsList(_treeRootItems, ForceExpansion.ForceExpand));
        }

        /// <summary>
        /// Call this method to collapse the whole tree
        /// </summary>
        public void ForceAllToCollapse() {
            if (!_isSearching)
                base.SetItems(GetExpandedItemsList(_treeRootItems, ForceExpansion.ForceCollapse));
        }

        /// <summary>
        /// ReCompute the whole list while taking into account expanded items, to be called when you change the 
        /// IsExpanded property of items on the list
        /// </summary>
        public void ApplyExpansionState() {
            if (!_isSearching)
                base.SetItems(GetExpandedItemsList(_treeRootItems, ForceExpansion.Idle));
        }

        /// <summary>
        /// The list automatocally saves the expansion state of a node when it is changed,
        /// if you want the states to be forgotten, call this method
        /// </summary>
        public void ClearSavedExpansionState() {
            _savedState.Clear();
        }

        /// <summary>
        /// Toggle expand/collapse for the an item at the given index
        /// </summary>
        private void ExpandCollapse(int itemIndex, ForceExpansion forceExpansion) {

            var selectedItem = GetItem(itemIndex);
            if (selectedItem == null)
                return;

            // handles a node expansion
            var currentItem = selectedItem as FilteredTypeTreeListItem;
            if (currentItem != null && currentItem.CanExpand) {
                string currentItemPathDescriptor = currentItem.PathDescriptor;

                // switch state
                if (forceExpansion != ForceExpansion.Idle)
                    currentItem.IsExpanded = (forceExpansion == ForceExpansion.ForceExpand);
                else
                    currentItem.IsExpanded = !currentItem.IsExpanded;

                // saves expansion state
                if (_savedState.ContainsKey(currentItemPathDescriptor))
                    _savedState[currentItemPathDescriptor] = currentItem.IsExpanded;
                else
                    _savedState.Add(currentItemPathDescriptor, currentItem.IsExpanded);

                ApplyExpansionState();
            }
        }

        #endregion

        #region Draw list

        /// <summary>
        /// Called by default to paint the row if no OnRowPaint is defined
        /// </summary>
        protected override void RowPaint(ListItem item, YamuiListRow row, PaintEventArgs e) {

            // background
            var backColor = YamuiThemeManager.Current.MenuBg(row.IsSelected, row.IsHovered, !item.IsDisabled);
            e.Graphics.Clear(backColor);

            var curItem = item as FilteredTypeTreeListItem;
            if (curItem != null) {
                var drawRect = row.ClientRectangle;
                drawRect.Height = RowHeight;
                var shiftedDrawRect = drawRect;

                // draw the tree structure
                if (!_isSearching) {

                    var treeWidth = 8;
                    var foreColor = YamuiThemeManager.Current.MenuFg(row.IsSelected, row.IsHovered, !item.IsDisabled);
                    var arrowColor = curItem.IsExpanded ? YamuiThemeManager.Current.AccentColor : foreColor;

                    // draw the branches of the tree
                    if (ShowTreeBranches) {
                        using (var linePen = new Pen(!item.IsDisabled ? YamuiThemeManager.Current.SubTextFore : foreColor, 1.5f) {DashStyle = DashStyle.Solid}) {

                            // Draw the vertical lines for each ancestors
                            var pos = drawRect.X + treeWidth/2;
                            for (int i = 1; i <= curItem.Level; i++) {
                                if (i == curItem.Level && curItem.IsLastItem)
                                    e.Graphics.DrawLine(linePen, pos, drawRect.Y, pos, drawRect.Y + drawRect.Height/2 - 1);
                                else
                                    e.Graphics.DrawLine(linePen, pos, drawRect.Y, pos, drawRect.Y + drawRect.Height);
                                pos += treeWidth;
                            }

                            // Draw the horizontal line that goes to the arrow
                            if (curItem.Level > 0) {
                                pos -= treeWidth;
                                linePen.Color = arrowColor;
                                e.Graphics.DrawLine(linePen, pos, drawRect.Y + drawRect.Height/2 - 1, pos + treeWidth/2, drawRect.Y + drawRect.Height/2 - 1);
                            }
                        }
                    }

                    for (int i = 0; i <= curItem.Level; i++) {
                        shiftedDrawRect.X += treeWidth;
                        shiftedDrawRect.Width -= treeWidth;
                    }

                    // Draw the arrow icon indicating if the node is expanded or not
                    if (curItem.CanExpand) {
                        TextRenderer.DrawText(e.Graphics, curItem.IsExpanded ? "q" : "u", FontManager.GetOtherFont("Wingdings 3", FontStyle.Regular, (float) (shiftedDrawRect.Height*0.40)), new Rectangle(shiftedDrawRect.X - treeWidth, shiftedDrawRect.Y, treeWidth, shiftedDrawRect.Height), arrowColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                    }
                }

                DrawFilteredTypeRow(curItem, shiftedDrawRect, row, e);
            }
        }

        #endregion

        #region ApplyFilter

        /// <summary>
        /// Returns a list of items that meet the FilterPredicate requirement as well as the filter string requirement,
        /// it also sorts the list of items
        /// We override this for trees so that if an item survives the filter, its parent should be displayed as well
        /// </summary>
        protected override List<ListItem> GetFilteredAndSortedList(List<FilteredListItem> listItems) {

            // when searching, the tree must actually behave like a FilteredTypeList
            if (_isSearching)
                return base.GetFilteredAndSortedList(listItems);

            var outList = new List<FilteredTypeTreeListItem>();
            var parentsToInclude = new HashSet<string>();

            var nbItems = listItems.Count;
            for (int i = nbItems - 1; i >= 0; i--) {
                var item = listItems[i] as FilteredTypeTreeListItem;
                if (item == null) continue;
                
                // the item must be included
                if (parentsToInclude.Contains(item.PathDescriptor) || (item.FilterFullyMatch && (FilterPredicate == null || FilterPredicate(item)))) {
                    outList.Add(item);

                    // we register its parent to be included as well
                    var lastIdx = item.PathDescriptor.LastIndexOf(FilteredTypeTreeListItem.TreePathSeparator, StringComparison.CurrentCultureIgnoreCase);
                    if (lastIdx > -1) {
                        var parentPath = item.PathDescriptor.Substring(0, lastIdx);
                        if (!parentsToInclude.Contains(parentPath))
                            parentsToInclude.Add(parentPath);
                    }
                }
            }

            // we have our list completed, but we need to reverse it before delivering it
            outList.Reverse();

            return outList.Cast<ListItem>().ToList();
        }

        /// <summary>
        /// Allows to update the _isSearching value, if it does update, switches the list from
        /// a tree view to a flat list where we applied the classic filter from the filteredtypelist
        /// </summary>
        private bool SetIsSearching(string stringFilter, SearchModeOption searchMode) {
            var newIsSearching = !string.IsNullOrEmpty(stringFilter.ToLower().Trim()) && searchMode == SearchModeOption.FilterSortWithNoParent;
            if (newIsSearching != _isSearching) {
                _isSearching = newIsSearching;

                // we went from searching to not searching or the contrary
                if (_isSearching)
                    base.SetItems(GetFullItemsList(_treeRootItems));
                else
                    base.SetItems(GetExpandedItemsList(_treeRootItems, ForceExpansion.Idle));
                return true;
            }
            return false;
        }

        #endregion

        #region Events pushed from the button rows

        /// <summary>
        /// Click on an item, SelectedItem is usable at this time
        /// </summary>
        protected override void OnItemClick(MouseEventArgs eventArgs) {
            // handles node expansion
            if (!_isSearching)
                ExpandCollapse(SelectedItemIndex, ForceExpansion.Idle);

            base.OnItemClick(eventArgs);
        }

        #endregion

        #region OnKeyDown

        public override bool OnKeyDown(Keys pressedKey) {
            switch (pressedKey) {
                case Keys.Left:
                    if (_isSearching || ModifierKeys.HasFlag(Keys.Control)) {
                        LeftRight(true);
                    } else {
                        var curItem = SelectedItem as FilteredTypeTreeListItem;
                        if (curItem != null) {
                            if (curItem.CanExpand && curItem.IsExpanded)
                                // collapse the current item
                                ExpandCollapse(SelectedItemIndex, ForceExpansion.ForceCollapse);
                            else {
                                // select its parent
                                var lastSep = curItem.PathDescriptor.LastIndexOf(FilteredTypeTreeListItem.TreePathSeparator, StringComparison.CurrentCultureIgnoreCase);
                                if (lastSep == -1)
                                    return true;
                                var parentPath = curItem.PathDescriptor.Substring(0, lastSep);
                                var idx = SelectedItemIndex;
                                FilteredTypeTreeListItem itemAbove;
                                do {
                                    idx--;
                                    itemAbove = GetItem(idx) as FilteredTypeTreeListItem;
                                } while (itemAbove != null && !itemAbove.PathDescriptor.Equals(parentPath));
                                SelectedItemIndex = idx;
                            }
                        }
                    }
                    return true;

                case Keys.Right:
                    if (_isSearching || ModifierKeys.HasFlag(Keys.Control)) {
                        LeftRight(false);
                    } else {
                        // expand the current item
                        ExpandCollapse(SelectedItemIndex, ForceExpansion.ForceExpand);
                    }
                    return true;
            }
            return base.OnKeyDown(pressedKey);
        }

        #endregion

    }

}
