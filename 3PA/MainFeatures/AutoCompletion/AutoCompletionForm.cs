﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using BrightIdeasSoftware;
using BrightIdeasSoftware.Utilities;
using YamuiFramework.Controls;
using YamuiFramework.Fonts;
using YamuiFramework.Helper;
using YamuiFramework.Themes;
using _3PA.Images;
using _3PA.Interop;
using _3PA.Lib;

namespace _3PA.MainFeatures.AutoCompletion {

    /// <summary>
    /// This class create an autocompletion window
    /// </summary>
    public partial class AutoCompletionForm : Form {

        #region fields
        /// <summary>
        /// The filter to apply to the autocompletion form
        /// </summary>
        public string FilterByText {
            get { return _filterString; }
            set { _filterString = value; ApplyFilter(); }
        }

        public bool UseAlternateBackColor {
            set { fastOLV.UseAlternatingBackColors = value; }
        }

        /// <summary>
        /// Raised when the user presses TAB or ENTER or double click
        /// </summary>
        public event EventHandler<TabCompletedEventArgs> TabCompleted;

        /// <summary>
        /// Set this to the parent form handle, this gives him back the focus when needed
        /// </summary>
        public IntPtr CurrentForegroundWindow;

        private Dictionary<CompletionType, SelectorButton> _activeTypes;
        private string _filterString;
        private int _totalItems;
        private bool _focusAllowed;
        // check the npp window rect, if it has changed from a previous state, close this form (poll every 500ms)
        private Rectangle? _nppRect;
        private Timer _timer1;
        private bool _iGotActivated;
        private int _normalWidth;
        private List<CompletionData> _initialObjectsList;
        private ImageList _imageListOfTypes;
        private bool _allowshowdisplay;
        #endregion

        #region constructor

        /// <summary>
        /// Constructor for the autocompletion form
        /// </summary>
        /// <param name="initialFilter"></param>
        public AutoCompletionForm(string initialFilter) {
            SetStyle(
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint, true);

            InitializeComponent();

            // Style the control
            fastOLV.OwnerDraw = true;
            fastOLV.Font = FontManager.GetLabelFont(LabelFunction.AutoCompletion);
            fastOLV.BackColor = ThemeManager.Current.AutoCompletionNormalBackColor;
            fastOLV.AlternateRowBackColor = ThemeManager.Current.AutoCompletionNormalAlternateBackColor;
            fastOLV.ForeColor = ThemeManager.Current.AutoCompletionNormalForeColor;
            fastOLV.HighlightBackgroundColor = ThemeManager.Current.AutoCompletionFocusBackColor;
            fastOLV.HighlightForegroundColor = ThemeManager.Current.AutoCompletionFocusForeColor;
            fastOLV.UnfocusedHighlightBackgroundColor = fastOLV.HighlightBackgroundColor;
            fastOLV.UnfocusedHighlightForegroundColor = fastOLV.HighlightForegroundColor;

            // Decorate and configure hot item
            fastOLV.UseHotItem = true;
            fastOLV.HotItemStyle = new HotItemStyle();
            fastOLV.HotItemStyle.BackColor = ThemeManager.Current.AutoCompletionHoverBackColor;
            fastOLV.HotItemStyle.ForeColor = ThemeManager.Current.AutoCompletionHoverForeColor;

            // set the image list to use for the keywords
            _imageListOfTypes = new ImageList {
                TransparentColor = Color.Transparent,
                ColorDepth = ColorDepth.Depth32Bit,
                ImageSize = new Size(20, 20)
            };
            ImagelistAdd.AddFromImage(ImageResources.Keyword, _imageListOfTypes);
            ImagelistAdd.AddFromImage(ImageResources.Table, _imageListOfTypes);
            ImagelistAdd.AddFromImage(ImageResources.TempTable, _imageListOfTypes);
            ImagelistAdd.AddFromImage(ImageResources.Field, _imageListOfTypes);
            ImagelistAdd.AddFromImage(ImageResources.FieldPk, _imageListOfTypes);
            ImagelistAdd.AddFromImage(ImageResources.Snippet, _imageListOfTypes);
            ImagelistAdd.AddFromImage(ImageResources.Function, _imageListOfTypes);
            ImagelistAdd.AddFromImage(ImageResources.Procedure, _imageListOfTypes);
            ImagelistAdd.AddFromImage(ImageResources.UserVariablePrimitive, _imageListOfTypes);
            ImagelistAdd.AddFromImage(ImageResources.UserVariableOther, _imageListOfTypes);
            ImagelistAdd.AddFromImage(ImageResources.Preprocessed, _imageListOfTypes);
            fastOLV.SmallImageList = _imageListOfTypes;
            Keyword.ImageGetter += rowObject => {
                var x = (CompletionData) rowObject;
                return (int) x.Type;
            };

            // overlay of empty list :
            fastOLV.EmptyListMsg = "No suggestions!";
            TextOverlay textOverlay = fastOLV.EmptyListMsgOverlay as TextOverlay;
            if (textOverlay != null) {
                textOverlay.TextColor = ThemeManager.Current.AutoCompletionNormalForeColor;
                textOverlay.BackColor = ThemeManager.Current.AutoCompletionNormalAlternateBackColor;
                textOverlay.BorderColor = ThemeManager.Current.AutoCompletionNormalForeColor;
                textOverlay.BorderWidth = 4.0f;
                textOverlay.Font = FontManager.GetFont(FontStyle.Bold, 30f);
                textOverlay.Rotation = -5;
            }

            // decorate rows
            fastOLV.UseCellFormatEvents = true;
            fastOLV.FormatCell += FastOlvOnFormatCell;

            // we prevent further sorting
            fastOLV.BeforeSorting += FastOlvOnBeforeSorting;
            fastOLV.KeyDown += FastOlvOnKeyDown;

            fastOLV.UseTabAsInput = true;
            _filterString = initialFilter;

            // timer to check if the npp window changed
            _timer1 = new Timer();
            _timer1.Enabled = true;
            _timer1.Interval = 500;
            _timer1.Tick += timer1_Tick;

            // handles mouse leave/mouse enter
            MouseLeave += CustomOnMouseLeave;
            fastOLV.MouseLeave += CustomOnMouseLeave;
            fastOLV.DoubleClick += FastOlvOnDoubleClick;

            // register to Npp
            FormIntegration.RegisterToNpp(Handle);

            Visible = false;
            Opacity = 0d;
            Tag = false;
            Closing += OnClosing;
        }

        /// <summary>
        /// hides the form
        /// </summary>
        public void Cloack() {
            Visible = false;
            GiveFocusBack();
        }

        /// <summary>
        /// show the form
        /// </summary>
        public void UnCloack() {
            _allowshowdisplay = true;
            Opacity = Config.Instance.AutoCompleteOpacityUnfocused;
            Visible = true;
            GiveFocusBack();
        }

        /// <summary>
        /// instead of closing, cload this form (invisible)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="cancelEventArgs"></param>
        private void OnClosing(object sender, CancelEventArgs cancelEventArgs) {
            if ((bool)Tag) return;
            cancelEventArgs.Cancel = true;
            Cloack();
        }

        /// <summary>
        /// Event on format cell
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void FastOlvOnFormatCell(object sender, FormatCellEventArgs args) {
            var type = ((CompletionData)args.Model).Flag;
            if (type != CompletionFlag.None) {
                TextDecoration decoration = new TextDecoration(Enum.GetName(typeof(CompletionFlag), type), 100);
                decoration.Alignment = ContentAlignment.MiddleRight;
                decoration.Offset = new Size(-5, 0);
                decoration.Font = FontManager.GetFont(FontStyle.Bold, 11);
                decoration.TextColor = ThemeManager.Current.AutoCompletionNormalSubTypeForeColor;
                decoration.CornerRounding = 1f;
                decoration.Rotation = 0;
                decoration.BorderWidth = 1;
                decoration.BorderColor = ThemeManager.Current.AutoCompletionNormalSubTypeForeColor;
                args.SubItem.Decoration = decoration; //NB. Sets Decoration
            }
        }
        #endregion

        /// <summary>
        /// set the items of the object view list, correct the width and height of the form and the list
        /// create a button for each type of completion present in the list of items
        /// </summary>
        /// <param name="objectsList"></param>
        public void SetItems(List<CompletionData> objectsList) {
            // we do the sorting
            objectsList.Sort(new CompletionDataSortingClass());
            _initialObjectsList = objectsList;

            // set the default height / width
            fastOLV.Height = 21 * Config.Instance.AutoCompleteShowListOfXSuggestions;
            Height = fastOLV.Height + 32;
            Width = 280;

            // delete any existing buttons
            if (_activeTypes != null) {
                foreach (var selectorButton in _activeTypes) {
                    selectorButton.Value.ButtonPressed -= HandleTypeClick;
                    if (Controls.Contains(selectorButton.Value))
                        Controls.Remove(selectorButton.Value);
                    selectorButton.Value.Dispose();
                }
            }

            // get distinct types, create a button for each
            int xPos = 4;
            _activeTypes = new Dictionary<CompletionType, SelectorButton>();
            foreach (var type in objectsList.Select(x => x.Type).Distinct()) {
                var but = new SelectorButton();
                but.BackGrndImage = _imageListOfTypes.Images[(int)type];
                but.Activated = true;
                but.Size = new Size(24, 24);
                but.TabStop = false;
                but.Location = new Point(xPos, Height - 28);
                but.Type = type;
                but.ButtonPressed += HandleTypeClick;
                _activeTypes.Add(type, but);
                Controls.Add(but);
                xPos += but.Width;
            }
            xPos += 65;

            // correct width
            Width = Math.Max(Width, xPos);
            _normalWidth = Width - 2;
            Keyword.Width = _normalWidth - 17;

            // label for the number of items
            _totalItems = objectsList.Count;
            nbitems.Text = _totalItems + " items";

            fastOLV.SetObjects(objectsList);
        }

        /// <summary>
        /// use this to programmatically uncheck any type that is not in the given list
        /// </summary>
        /// <param name="allowedType"></param>
        public void SetActiveType(List<CompletionType> allowedType) {
            if (_activeTypes == null) return;
            foreach (var selectorButton in _activeTypes) {
                if (allowedType.IndexOf(selectorButton.Value.Type) < 0) {
                    selectorButton.Value.Activated = false;
                    selectorButton.Value.Invalidate();
                }
            }
        }

        /// <summary>
        /// reset all the button Types to activated
        /// </summary>
        public void ResetActiveType() {
            if (_activeTypes == null) return;
            foreach (var selectorButton in _activeTypes) {
                selectorButton.Value.Activated = true;
                selectorButton.Value.Invalidate();
            }
        }

        /// <summary>
        /// allows to programmatically select the first item of the list
        /// </summary>
        public void SelectFirstItem() {
            try {
                fastOLV.SelectedIndex = 0;
            } catch (Exception e) {
                // ignored
            }
        }

        /// <summary>
        /// Position the window in a smart way according to the Point in input
        /// </summary>
        /// <param name="position"></param>
        /// <param name="lineHeight"></param>
        public void SetPosition(Point position, int lineHeight) {
            // position the window smartly
            if (position.X > Screen.PrimaryScreen.WorkingArea.X + 2 * Screen.PrimaryScreen.WorkingArea.Width / 3)
                position.X = position.X - Width;
            if (position.Y > Screen.PrimaryScreen.WorkingArea.Y + 3 * Screen.PrimaryScreen.WorkingArea.Height / 5)
                position.Y = position.Y - Height - lineHeight;
            Location = position;
        }

        /// <summary>
        /// handles double click
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="eventArgs"></param>
        private void FastOlvOnDoubleClick(object sender, EventArgs eventArgs) {
            OnTabCompleted(new TabCompletedEventArgs(((CompletionData)fastOLV.SelectedItem.RowObject)));
        }

        /// <summary>
        /// Handles keydown event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="keyEventArgs"></param>
        private void FastOlvOnKeyDown(object sender, KeyEventArgs keyEventArgs) {
            keyEventArgs.Handled = OnKeyDown(keyEventArgs.KeyCode);
        }

        /// <summary>
        /// cancel any sort of.. sorting
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="beforeSortingEventArgs"></param>
        private void FastOlvOnBeforeSorting(object sender, BeforeSortingEventArgs beforeSortingEventArgs) {
            beforeSortingEventArgs.Canceled = true;
        }


        /// <summary>
        /// This ensures the form is never visible at start
        /// </summary>
        /// <param name="value"></param>
        protected override void SetVisibleCore(bool value) {
            base.SetVisibleCore(_allowshowdisplay ? value : _allowshowdisplay);
        }


        #region events
        /// <summary>
        /// handles click on a type
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void HandleTypeClick(object sender, EventArgs args) {
            CompletionType clickedType = ((SelectorButton) sender).Type;
            if (_activeTypes[clickedType].Activated) {
                // if everything is active, what we want to do is make everything but this one inactive
                if (_activeTypes.Count(b => !b.Value.Activated) == 0) {
                    foreach (CompletionType key in _activeTypes.Keys.ToList()) {
                        _activeTypes[key].Activated = false;
                        _activeTypes[key].Invalidate();
                    }
                    _activeTypes[clickedType].Activated = true;
                } else if (_activeTypes.Count(b => b.Value.Activated) == 1) {
                    foreach (CompletionType key in _activeTypes.Keys.ToList()) {
                        _activeTypes[key].Activated = true;
                        _activeTypes[key].Invalidate();
                    }
                } else
                    _activeTypes[clickedType].Activated = !_activeTypes[clickedType].Activated;
            } else
                _activeTypes[clickedType].Activated = !_activeTypes[clickedType].Activated;
            _activeTypes[clickedType].Invalidate();
            ApplyFilter();
            // give focus back
            GiveFocusBack();
        }

        /// <summary>
        /// Gives focus back to the owner window
        /// </summary>
        private void GiveFocusBack() {
            WinApi.SetForegroundWindow(CurrentForegroundWindow);
            _iGotActivated = !_iGotActivated;
            Opacity = Config.Instance.AutoCompleteOpacityUnfocused;
        }

        protected void CustomOnMouseLeave(object sender, EventArgs e) {
            if (_iGotActivated) GiveFocusBack();
        }

        protected override void OnActivated(EventArgs e) {
            // Activate the window that previously had focus
            if (!_focusAllowed)
                WinApi.SetForegroundWindow(CurrentForegroundWindow);
            else {
                _iGotActivated = true;
                Opacity = 1;
            }
            base.OnActivated(e);
        }

        protected override void OnLoad(EventArgs e) {
            base.OnLoad(e);
            fastOLV.SelectedIndex = 0;
            //if (!string.IsNullOrEmpty(_filterString)) ApplyFilter();
        }

        protected override void OnShown(EventArgs e) {
            _focusAllowed = true;
            base.OnShown(e);
        }

        protected virtual void OnTabCompleted(TabCompletedEventArgs e) {
            var handler = TabCompleted;
            if (handler != null) handler(this, e);
        }

        private void timer1_Tick(object sender, EventArgs e) {
            try {
                var rect = Npp.GetWindowRect();
                if (_nppRect.HasValue && _nppRect.Value != rect)
                    Close();
                _nppRect = rect;
            } catch (Exception) {
                // ignored
            }
        }
        #endregion

        #region "on key events"

        public bool OnKeyDown(Keys key) {
            bool handled = true;
            // down and up change the selection
            if (key == Keys.Up) {
                if (fastOLV.SelectedIndex > 0)
                    fastOLV.SelectedIndex--;
                else
                    fastOLV.SelectedIndex = (_totalItems - 1);
                fastOLV.EnsureVisible(fastOLV.SelectedIndex);
            } else if (key == Keys.Down) {
                if (fastOLV.SelectedIndex < (_totalItems - 1))
                    fastOLV.SelectedIndex++;
                else
                    fastOLV.SelectedIndex = 0;
                fastOLV.EnsureVisible(fastOLV.SelectedIndex);

                // escape close
            } else if (key == Keys.Escape) {
                Close();

                // enter and tab accept the current selection
            } else if ((key == Keys.Enter && Config.Instance.AutoCompleteUseEnterToAccept) || (key == Keys.Tab && Config.Instance.AutoCompleteUseTabToAccept)) {
                OnTabCompleted(new TabCompletedEventArgs(((CompletionData)fastOLV.SelectedItem.RowObject)));

                // else, any other key needs to be analysed by Npp
            } else {
                handled = false;
            }
            return handled;
        }

        #endregion

        #region Paint Methods

        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e) {
            var backColor = ThemeManager.Current.FormColorBackColor;
            var borderColor = ThemeManager.AccentColor;
            var borderWidth = 1;

            e.Graphics.Clear(backColor);

            // draw the border with Style color
            var rect = new Rectangle(new Point(0, 0), new Size(Width - borderWidth, Height - borderWidth));
            var pen = new Pen(borderColor, borderWidth);
            e.Graphics.DrawRectangle(pen, rect);
        }

        #endregion

        #region private methods
        /// <summary>
        /// this methods sorts the items to put the best match on top and then filter it with modelFilter
        /// </summary>
        private void ApplyFilter() {
            fastOLV.SetObjects(_initialObjectsList.OrderBy(
                x => {
                    if (!x.DisplayText.StartsWith(_filterString, StringComparison.OrdinalIgnoreCase))
                        return 2;
                    if (x.DisplayText.Equals(_filterString, StringComparison.OrdinalIgnoreCase)) {
                        return 0;
                    }   
                    return 1;
            }).ToList());

            fastOLV.ModelFilter = new ModelFilter((o => ((CompletionData) o).DisplayText.Contains(_filterString, StringComparison.InvariantCultureIgnoreCase) && _activeTypes[((CompletionData) o).Type].Activated));
            fastOLV.DefaultRenderer = new CustomHighlightTextRenderer(fastOLV, _filterString);

            // update total items
            _totalItems = ((ArrayList) fastOLV.FilteredObjects).Count;
            nbitems.Text = _totalItems + " items";

            // if the selected row is > to number of items, then there will be a unselect
            try {
                Keyword.Width = _normalWidth - ((_totalItems <= Config.Instance.AutoCompleteShowListOfXSuggestions) ? 0 : 17);
                if (fastOLV.SelectedIndex == - 1) fastOLV.SelectedIndex = 0;
                fastOLV.EnsureVisible(fastOLV.SelectedIndex);
            } catch (Exception) {
                // ignored
            }
        }
        #endregion
    }

    #region sorting

    /// <summary>
    /// Class used in objectlist.Sort method
    /// </summary>
    public class CompletionDataSortingClass : IComparer<CompletionData> {
        public int Compare(CompletionData x, CompletionData y) {
            int compare = x.Type.CompareTo(y.Type);
            if (compare == 0) {
                return x.Ranking.CompareTo(y.Ranking);
            }
            return compare;
        }
    }
    #endregion

    #region SelectorButtons
    public class SelectorButton : YamuiButton {

        #region Fields
        public Image BackGrndImage { get; set; }

        public bool Activated { get; set; }

        public CompletionType Type { get; set; }
        #endregion

        #region Paint Methods
        protected override void OnPaint(PaintEventArgs e) {
            try {
                Color backColor = ThemeManager.ButtonColors.BackGround(BackColor, false, IsFocused, IsHovered, IsPressed, true);
                Color borderColor = ThemeManager.ButtonColors.BorderColor(IsFocused, IsHovered, IsPressed, true);
                var img = BackGrndImage;

                // draw background
                using (SolidBrush b = new SolidBrush(backColor)) {
                    e.Graphics.FillRectangle(b, ClientRectangle);
                }

                // draw main image, in greyscale if not activated
                if (!Activated)
                    img = Utils.MakeGrayscale3(new Bitmap(img, new Size(BackGrndImage.Width, BackGrndImage.Height)));
                var recImg = new Rectangle(new Point((ClientRectangle.Width - img.Width)/2, (ClientRectangle.Height - img.Height)/2), new Size(img.Width, img.Height));
                e.Graphics.DrawImage(img, recImg);

                // border
                recImg = ClientRectangle;
                recImg.Inflate(-2, -2);
                if (borderColor != Color.Transparent) {
                    using (Pen b = new Pen(borderColor, 2f)) {
                        e.Graphics.DrawRectangle(b, recImg);
                    }
                }
            } catch {
                // ignored
            }
        }

        #endregion
    }

    #endregion

    #region TabCompletedEventArgs

    public sealed class TabCompletedEventArgs : EventArgs {
        /// <summary>
        /// the link href that was clicked
        /// </summary>
        public CompletionData CompletionItem;

        public TabCompletedEventArgs(CompletionData completionItem) {
            CompletionItem = completionItem;
        }
    }

    #endregion

    #region CustomHighlightRenderer

    /// <summary>
    /// This renderer highlights substrings that match a given text filter. 
    /// </summary>
    public class CustomHighlightTextRenderer : BaseRenderer {
        #region Life and death

        /// <summary>
        /// Create a HighlightTextRenderer
        /// </summary>
        public CustomHighlightTextRenderer() {
            FillBrush = new SolidBrush(ThemeManager.Current.AutoCompletionHighlightBack);
            FramePen = new Pen(ThemeManager.Current.AutoCompletionHighlightBorder);
        }

        /// <summary>
        /// Create a HighlightTextRenderer
        /// </summary>
        public CustomHighlightTextRenderer(ObjectListView fastOvl, string filterStr)
            : this() {
            Filter = new TextMatchFilter(fastOvl, filterStr, StringComparison.OrdinalIgnoreCase);
        }
        #endregion

        #region Configuration properties

        /// <summary>
        /// Gets or set how rounded will be the corners of the text match frame
        /// </summary>
        [Category("Appearance"),
         DefaultValue(3.0f),
         Description("How rounded will be the corners of the text match frame?")]
        public float CornerRoundness {
            get { return _cornerRoundness; }
            set { _cornerRoundness = value; }
        }

        private float _cornerRoundness = 4.0f;

        /// <summary>
        /// Gets or set the brush will be used to paint behind the matched substrings.
        /// Set this to null to not fill the frame.
        /// </summary>
        [Browsable(false),
         DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Brush FillBrush {
            get { return _fillBrush; }
            set { _fillBrush = value; }
        }

        private Brush _fillBrush;

        /// <summary>
        /// Gets or sets the filter that is filtering the ObjectListView and for
        /// which this renderer should highlight text
        /// </summary>
        [Browsable(false),
         DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public TextMatchFilter Filter {
            get { return _filter; }
            set { _filter = value; }
        }

        private TextMatchFilter _filter;

        /// <summary>
        /// Gets or set the pen will be used to frame the matched substrings.
        /// Set this to null to not draw a frame.
        /// </summary>
        [Browsable(false),
         DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Pen FramePen {
            get { return _framePen; }
            set { _framePen = value; }
        }

        private Pen _framePen;

        /// <summary>
        /// Gets or sets whether the frame around a text match will have rounded corners
        /// </summary>
        [Category("Appearance"),
         DefaultValue(true),
         Description("Will the frame around a text match will have rounded corners?")]
        public bool UseRoundedRectangle {
            get { return _useRoundedRectangle; }
            set { _useRoundedRectangle = value; }
        }

        private bool _useRoundedRectangle = true;

        #endregion

        #region IRenderer interface overrides

        /// <summary>
        /// Handle a HitTest request after all state information has been initialized
        /// </summary>
        /// <param name="g"></param>
        /// <param name="cellBounds"></param>
        /// <param name="item"></param>
        /// <param name="subItemIndex"></param>
        /// <param name="preferredSize"> </param>
        /// <returns></returns>
        protected override Rectangle HandleGetEditRectangle(Graphics g, Rectangle cellBounds, OLVListItem item, int subItemIndex, Size preferredSize) {
            return StandardGetEditRectangle(g, cellBounds, preferredSize);
        }

        #endregion

        #region Rendering

        // This class has two implement two highlighting schemes: one for GDI, another for GDI+.
        // Naturally, GDI+ makes the task easier, but we have to provide something for GDI
        // since that it is what is normally used.

        /// <summary>
        /// Draw text using GDI
        /// </summary>
        /// <param name="g"></param>
        /// <param name="r"></param>
        /// <param name="txt"></param>
        protected override void DrawTextGdi(Graphics g, Rectangle r, string txt) {
            if (ShouldDrawHighlighting)
                DrawGdiTextHighlighting(g, r, txt);

            base.DrawTextGdi(g, r, txt);
        }

        /// <summary>
        /// Draw the highlighted text using GDI
        /// </summary>
        /// <param name="g"></param>
        /// <param name="r"></param>
        /// <param name="txt"></param>
        protected virtual void DrawGdiTextHighlighting(Graphics g, Rectangle r, string txt) {
            TextFormatFlags flags = TextFormatFlags.NoPrefix |
                                    TextFormatFlags.VerticalCenter | TextFormatFlags.PreserveGraphicsTranslateTransform;

            // TextRenderer puts horizontal padding around the strings, so we need to take
            // that into account when measuring strings
            int paddingAdjustment = 6;

            // Cache the font
            Font f = Font;

            foreach (CharacterRange range in Filter.FindAllMatchedRanges(txt)) {
                // Measure the text that comes before our substring
                Size precedingTextSize = Size.Empty;
                if (range.First > 0) {
                    string precedingText = txt.Substring(0, range.First);
                    precedingTextSize = TextRenderer.MeasureText(g, precedingText, f, r.Size, flags);
                    precedingTextSize.Width -= paddingAdjustment;
                }

                // Measure the length of our substring (may be different each time due to case differences)
                string highlightText = txt.Substring(range.First, range.Length);
                Size textToHighlightSize = TextRenderer.MeasureText(g, highlightText, f, r.Size, flags);
                textToHighlightSize.Width -= paddingAdjustment;

                float textToHighlightLeft = r.X + precedingTextSize.Width + 1;
                float textToHighlightTop = AlignVertically(r, textToHighlightSize.Height);

                // Draw a filled frame around our substring
                DrawSubstringFrame(g, textToHighlightLeft, textToHighlightTop, textToHighlightSize.Width, textToHighlightSize.Height);
            }
        }

        /// <summary>
        /// Draw an indication around the given frame that shows a text match
        /// </summary>
        /// <param name="g"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        protected virtual void DrawSubstringFrame(Graphics g, float x, float y, float width, float height) {
            if (UseRoundedRectangle) {
                using (GraphicsPath path = GetRoundedRect(x, y, width, height, 3.0f)) {
                    if (FillBrush != null)
                        g.FillPath(FillBrush, path);
                    if (FramePen != null)
                        g.DrawPath(FramePen, path);
                }
            } else {
                if (FillBrush != null)
                    g.FillRectangle(FillBrush, x, y, width, height);
                if (FramePen != null)
                    g.DrawRectangle(FramePen, x, y, width, height);
            }
        }

        /// <summary>
        /// Draw the text using GDI+
        /// </summary>
        /// <param name="g"></param>
        /// <param name="r"></param>
        /// <param name="txt"></param>
        protected override void DrawTextGdiPlus(Graphics g, Rectangle r, string txt) {
            if (ShouldDrawHighlighting)
                DrawGdiPlusTextHighlighting(g, r, txt);

            base.DrawTextGdiPlus(g, r, txt);
        }

        /// <summary>
        /// Draw the highlighted text using GDI+
        /// </summary>
        /// <param name="g"></param>
        /// <param name="r"></param>
        /// <param name="txt"></param>
        protected virtual void DrawGdiPlusTextHighlighting(Graphics g, Rectangle r, string txt) {
            // Find the substrings we want to highlight
            List<CharacterRange> ranges = new List<CharacterRange>(Filter.FindAllMatchedRanges(txt));

            if (ranges.Count == 0)
                return;

            using (StringFormat fmt = StringFormatForGdiPlus) {
                RectangleF rf = r;
                fmt.SetMeasurableCharacterRanges(ranges.ToArray());
                Region[] stringRegions = g.MeasureCharacterRanges(txt, Font, rf, fmt);

                foreach (Region region in stringRegions) {
                    RectangleF bounds = region.GetBounds(g);
                    DrawSubstringFrame(g, bounds.X - 1, bounds.Y - 1, bounds.Width + 2, bounds.Height);
                }
            }
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Gets whether the renderer should actually draw highlighting
        /// </summary>
        protected bool ShouldDrawHighlighting {
            get { return Column == null || (Column.Searchable && Filter != null && Filter.HasComponents); }
        }

        /// <summary>
        /// Return a GraphicPath that is a round cornered rectangle
        /// </summary>
        /// <returns>A round cornered rectagle path</returns>
        /// <remarks>If I could rely on people using C# 3.0+, this should be
        /// an extension method of GraphicsPath.</remarks>        
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="diameter"></param>
        protected GraphicsPath GetRoundedRect(float x, float y, float width, float height, float diameter) {
            return GetRoundedRect(new RectangleF(x, y, width, height), diameter);
        }

        /// <summary>
        /// Return a GraphicPath that is a round cornered rectangle
        /// </summary>
        /// <param name="rect">The rectangle</param>
        /// <param name="diameter">The diameter of the corners</param>
        /// <returns>A round cornered rectagle path</returns>
        /// <remarks>If I could rely on people using C# 3.0+, this should be
        /// an extension method of GraphicsPath.</remarks>
        protected GraphicsPath GetRoundedRect(RectangleF rect, float diameter) {
            GraphicsPath path = new GraphicsPath();

            if (diameter > 0) {
                RectangleF arc = new RectangleF(rect.X, rect.Y, diameter, diameter);
                path.AddArc(arc, 180, 90);
                arc.X = rect.Right - diameter;
                path.AddArc(arc, 270, 90);
                arc.Y = rect.Bottom - diameter;
                path.AddArc(arc, 0, 90);
                arc.X = rect.Left;
                path.AddArc(arc, 90, 90);
                path.CloseFigure();
            } else {
                path.AddRectangle(rect);
            }

            return path;
        }

        #endregion
    }

    #endregion

}