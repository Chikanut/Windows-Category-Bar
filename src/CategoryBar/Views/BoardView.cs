using System.Windows.Shapes;

namespace CategoryBar.Views;

/// <summary>
/// All categories side by side as columns of applications. Shortcuts are dragged between and within
/// columns, columns are dragged by their header. Used by the settings window and the first-run wizard.
/// </summary>
internal sealed class BoardView : Grid
{
    private const double ColumnWidth = 216, RowHeight = 34;
    private readonly Func<List<Category>> source;
    private readonly Func<Action, string?, bool> commit;
    private readonly Action<Category>? addApps;
    private readonly StackPanel columns = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
    private readonly ScrollViewer scroller = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
    private readonly Canvas overlay = new() { IsHitTestVisible = false };
    private readonly Border marker = new Border { CornerRadius = new CornerRadius(1.5), Visibility = Visibility.Collapsed }.Res(Border.BackgroundProperty, "Accent");
    private readonly Dictionary<Category, (ContentControl Column, StackPanel Rows)> map = [];
    private Border? ghost;
    private object? pressed;          // a Shortcut or a Category
    private FrameworkElement? pressedElement;
    private Point pressPoint;
    private bool dragging;
    private (Category Category, int Index)? drop;
    private Category? renaming;

    /// <param name="source">The live list of categories.</param>
    /// <param name="commit">Applies a change (with an optional toast title) and reports success.</param>
    internal BoardView(Func<List<Category>> source, Func<Action, string?, bool> commit, Action<Category>? addApps = null)
    {
        this.source = source; this.commit = commit; this.addApps = addApps;
        scroller.Content = columns;
        Children.Add(scroller);
        overlay.Children.Add(marker);
        Children.Add(overlay);
        Background = Brushes.Transparent;
        MouseMove += (_, e) => OnMove(e);
        MouseLeftButtonUp += (_, _) => OnRelease();
        LostMouseCapture += (_, _) => { if (dragging) EndDrag(false); };
        scroller.PreviewMouseWheel += (_, e) =>
        {
            // The board scrolls sideways; a column under the pointer keeps the wheel when it can scroll itself.
            if (e.OriginalSource is DependencyObject origin && FindColumnScroller(origin) is { ScrollableHeight: > 0 }) return;
            scroller.ScrollToHorizontalOffset(scroller.HorizontalOffset - e.Delta);
            e.Handled = true;
        };
        Refresh();
    }

    private static ScrollViewer? FindColumnScroller(DependencyObject origin)
    {
        for (var current = origin; current is not null; current = current is Visual ? VisualTreeHelper.GetParent(current) : LogicalTreeHelper.GetParent(current))
            if (current is ScrollViewer { Tag: "column" } viewer) return viewer;
        return null;
    }

    internal void Refresh()
    {
        if (dragging) return;
        var offset = scroller.HorizontalOffset;
        columns.Children.Clear(); map.Clear();
        foreach (var category in source()) columns.Children.Add(Column(category));
        columns.Children.Add(NewColumnButton());
        scroller.ScrollToHorizontalOffset(offset);
    }

    private FrameworkElement NewColumnButton()
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var plus = Ui.Icon(Icons.Plus, "Text.Muted", 14); plus.Margin = new Thickness(0, 0, 8, 0);
        content.Children.Add(plus); content.Children.Add(Ui.Text(L.T("Нова категорія"), 13, "Text.Muted"));
        var outline = new Rectangle { StrokeDashArray = [4, 3], StrokeThickness = 1, RadiusX = 8, RadiusY = 8, Opacity = .6 }.Res(Shape.StrokeProperty, "Text.Faint");
        var button = new Grid { Width = 170, Height = 44, VerticalAlignment = VerticalAlignment.Top, Background = Brushes.Transparent, Cursor = Cursors.Hand, Children = { outline, content } };
        button.MouseLeftButtonUp += (_, _) =>
        {
            var category = new Category { Name = Ui.FreshCategoryName(source()) };
            if (!commit(() => source().Add(category), null)) return;
            renaming = category;
            Refresh();
            scroller.ScrollToRightEnd();
        };
        return button;
    }

    private ContentControl Column(Category category)
    {
        var rows = new StackPanel { MinHeight = 40 };
        foreach (var shortcut in category.Shortcuts) rows.Children.Add(Row(category, shortcut));
        if (category.Shortcuts.Count == 0)
        {
            var hint = Ui.Text(L.T("Перетягніть сюди застосунки"), 12, "Text.Faint", wrap: true);
            hint.TextAlignment = TextAlignment.Center; hint.Margin = new Thickness(10, 14, 10, 14); hint.IsHitTestVisible = false;
            rows.Children.Add(hint);
        }

        var header = new Grid { Height = 36, Background = Brushes.Transparent, Cursor = Cursors.SizeAll, Margin = new Thickness(4, 2, 2, 2) };
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        if (renaming == category) header.Children.Add(RenameBox(category));
        else
        {
            var name = Ui.Text(category.Name, 13.5, "Text", bold: true); name.Margin = new Thickness(6, 0, 0, 0);
            header.Children.Add(name);
        }
        var count = Ui.Text(category.Shortcuts.Count.ToString(), 11.5, "Text.Faint"); count.Margin = new Thickness(6, 0, 4, 0);
        header.Children.Add(count.At(1));
        var more = Ui.IconButton(Icons.More, L.T("Дії з категорією"), () => { });
        more.Click += (_, _) => OpenColumnMenu(category, more);
        header.Children.Add(more.At(2));
        header.MouseLeftButtonDown += (_, e) =>
        {
            if (e.OriginalSource is DependencyObject origin && IsInside<Button>(origin) || renaming is not null) return;
            if (e.ClickCount == 2) { renaming = category; Refresh(); return; }
            Press(category, header, e);
        };

        var list = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = rows, Tag = "column" };
        var layout = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        layout.Children.Add(header);
        if (addApps is not null)
        {
            var add = new Border { Height = 32, Background = Brushes.Transparent, Margin = new Thickness(2, 2, 2, 0), Cursor = Cursors.Hand }.Res(Border.CornerRadiusProperty, "Radius.Small");
            var content = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 0, 0, 0) };
            var plus = Ui.Icon(Icons.Plus, "Text.Faint", 13); plus.Margin = new Thickness(0, 0, 9, 0);
            content.Children.Add(plus); content.Children.Add(Ui.Text(L.T("Додати"), 12.5, "Text.Faint"));
            add.Child = content;
            add.MouseEnter += (_, _) => add.SetResourceReference(Border.BackgroundProperty, "Hover");
            add.MouseLeave += (_, _) => add.Background = Brushes.Transparent;
            add.MouseLeftButtonUp += (_, _) => addApps(category);
            DockPanel.SetDock(add, Dock.Bottom);
            layout.Children.Add(add);
        }
        layout.Children.Add(list);
        var column = new ContentControl { Width = ColumnWidth, Margin = new Thickness(0, 0, 10, 0), Content = layout, Tag = category }.Res(StyleProperty, "Theme.BoardColumn");
        map[category] = (column, rows);
        return column;
    }

    private static bool IsInside<T>(DependencyObject origin) where T : DependencyObject
    {
        for (var current = origin; current is not null; current = current is Visual ? VisualTreeHelper.GetParent(current) : LogicalTreeHelper.GetParent(current))
            if (current is T) return true;
        return false;
    }

    private ContentControl Row(Category category, Shortcut shortcut)
    {
        var line = new Grid();
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        line.ColumnDefinitions.Add(new ColumnDefinition());
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        line.Children.Add(Ui.ShortcutIcon(shortcut, 20));
        var name = Ui.Text(shortcut.Name, 12.5); name.Margin = new Thickness(9, 0, 0, 0);
        line.Children.Add(name.At(1));
        var remove = Ui.IconButton(Icons.Close, L.T("Прибрати з категорії"), () => commit(() => category.Shortcuts.Remove(shortcut), L.F("Прибрано «{0}»", shortcut.Name)), "Text.Faint");
        remove.Width = remove.Height = remove.MinHeight = 22; remove.Visibility = Visibility.Hidden; remove.Margin = new Thickness(4, 0, -4, 0);
        line.Children.Add(remove.At(2));
        var row = new ContentControl { Height = RowHeight, Content = line, Tag = shortcut, ToolTip = shortcut.Name }.Res(StyleProperty, "Theme.MenuItem");
        ToolTipService.SetInitialShowDelay(row, 900);
        row.MouseEnter += (_, _) => { if (dragging) return; remove.Visibility = Visibility.Visible; };
        row.MouseLeave += (_, _) => { remove.Visibility = Visibility.Hidden; };
        row.MouseLeftButtonDown += (_, e) => { if (e.OriginalSource is DependencyObject origin && IsInside<Button>(origin)) return; Press(shortcut, row, e); };
        row.MouseRightButtonUp += (_, e) => { e.Handled = true; OpenRowMenu(category, shortcut, row); };
        return row;
    }

    private TextBox RenameBox(Category category)
    {
        var box = new TextBox { Text = category.Name, MaxLength = 100, MinHeight = 26, Height = 26, Margin = new Thickness(0, 0, 4, 0), Cursor = Cursors.IBeam };
        var finished = false;
        void Finish(bool save)
        {
            if (finished) return;
            finished = true; renaming = null;
            var name = box.Text.Trim();
            if (!save || name.Length == 0 || name == category.Name || !commit(() => category.Name = name, null)) Refresh();
        }
        box.Loaded += (_, _) => { box.Focus(); box.SelectAll(); };
        box.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { e.Handled = true; Finish(true); }
            else if (e.Key == Key.Escape) { e.Handled = true; Finish(false); }
        };
        box.LostKeyboardFocus += (_, _) => Finish(true);
        return box;
    }

    private void OpenColumnMenu(Category category, UIElement target)
    {
        var list = source();
        var index = list.IndexOf(category);
        var menu = new ContextMenu { PlacementTarget = target, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        menu.Items.Add(Ui.Item(L.T("Перейменувати"), () => { renaming = category; Refresh(); }, Icons.Pencil));
        if (addApps is not null) menu.Items.Add(Ui.Item(L.T("Додати застосунки…"), () => addApps(category), Icons.Plus));
        menu.Items.Add(Ui.Item(L.T("Перемістити ліворуч"), () => commit(() => ConfigurationEditor.Move(list, category, index - 1), null), Icons.ChevronLeft, enabled: index > 0));
        menu.Items.Add(Ui.Item(L.T("Перемістити праворуч"), () => commit(() => ConfigurationEditor.Move(list, category, index + 1), null), Icons.ChevronRight, enabled: index < list.Count - 1));
        menu.Items.Add(new Separator());
        menu.Items.Add(Ui.Item(L.T("Видалити категорію"), () => DeleteRequested?.Invoke(category), Icons.Trash, danger: true));
        menu.IsOpen = true;
    }

    /// <summary>Lets the host decide how a category is deleted (confirmation, moving its shortcuts).</summary>
    internal Action<Category>? DeleteRequested { get; set; }

    private void OpenRowMenu(Category category, Shortcut shortcut, UIElement target)
    {
        var menu = new ContextMenu { PlacementTarget = target };
        var move = new MenuItem { Header = L.T("Перемістити до"), Icon = Ui.Icon(Icons.Move) };
        foreach (var other in source().Where(c => c != category))
            move.Items.Add(Ui.Item(other.Name, () => commit(() => { category.Shortcuts.Remove(shortcut); other.Shortcuts.Add(shortcut); }, null)));
        move.IsEnabled = move.Items.Count > 0;
        menu.Items.Add(move);
        menu.Items.Add(Ui.Item(L.T("Прибрати з категорії"), () => commit(() => category.Shortcuts.Remove(shortcut), L.F("Прибрано «{0}»", shortcut.Name)), Icons.Trash, danger: true));
        menu.IsOpen = true;
    }

    // ---- Dragging --------------------------------------------------------------------------------

    private void Press(object item, FrameworkElement element, MouseButtonEventArgs e)
    {
        pressed = item; pressedElement = element; pressPoint = e.GetPosition(this);
        CaptureMouse();
    }

    private void OnMove(MouseEventArgs e)
    {
        if (pressed is null || e.LeftButton != MouseButtonState.Pressed) return;
        var point = e.GetPosition(this);
        if (!dragging)
        {
            if (Math.Abs(point.X - pressPoint.X) + Math.Abs(point.Y - pressPoint.Y) < 6) return;
            dragging = true;
            var label = pressed is Shortcut s ? s.Name : ((Category)pressed).Name;
            var content = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 0, 12, 0) };
            if (pressed is Shortcut shortcut) content.Children.Add(Ui.ShortcutIcon(shortcut, 20));
            var text = Ui.Text(label, 12.5, "Text", bold: pressed is Category); text.Margin = new Thickness(9, 0, 0, 0); text.MaxWidth = 170;
            content.Children.Add(text);
            ghost = new Border { Height = RowHeight, BorderThickness = new Thickness(1), Child = content, Opacity = .95 }
                .Res(Border.BackgroundProperty, "Popup.Bg").Res(Border.BorderBrushProperty, "Accent").Res(Border.CornerRadiusProperty, "Radius.Small").Res(EffectProperty, "Shadow");
            overlay.Children.Add(ghost);
            if (pressedElement is not null) pressedElement.Opacity = .35;
        }
        Canvas.SetLeft(ghost!, point.X + 10); Canvas.SetTop(ghost!, point.Y - RowHeight / 2);

        // Scroll sideways when the pointer nears an edge so far-away columns stay reachable.
        if (point.X < 36) scroller.ScrollToHorizontalOffset(scroller.HorizontalOffset - 14);
        else if (point.X > ActualWidth - 36) scroller.ScrollToHorizontalOffset(scroller.HorizontalOffset + 14);

        drop = null;
        marker.Visibility = Visibility.Collapsed;
        var list = source();
        if (pressed is Category)
        {
            // Insert before the first column whose middle is to the right of the pointer.
            var index = list.Count;
            for (var i = 0; i < list.Count; i++)
            {
                var column = map[list[i]].Column;
                if (point.X < column.TranslatePoint(new Point(column.ActualWidth / 2, 0), this).X) { index = i; break; }
            }
            drop = ((Category)pressed, index);
            var edge = index < list.Count ? map[list[index]].Column.TranslatePoint(new Point(-6, 0), this).X
                : map[list[^1]].Column.TranslatePoint(new Point(ColumnWidth + 4, 0), this).X;
            ShowMarker(edge, 4, 3, Math.Max(40, ActualHeight - 24));
            return;
        }
        foreach (var category in list)
        {
            var (column, rows) = map[category];
            var left = column.TranslatePoint(new Point(0, 0), this).X;
            if (point.X < left - 5 || point.X >= left + ColumnWidth + 5) continue;
            var count = category.Shortcuts.Count;
            var y = e.GetPosition(rows).Y;
            var index = Math.Clamp((int)Math.Round(y / RowHeight), 0, count);
            drop = (category, index);
            var top = rows.TranslatePoint(new Point(0, index * RowHeight), this);
            if (top.Y > 30 && top.Y < ActualHeight - 4) ShowMarker(top.X + 4, top.Y - 1.5, ColumnWidth - 18, 3);
            break;
        }
    }

    private void ShowMarker(double x, double y, double width, double height)
    {
        marker.Width = width; marker.Height = height;
        Canvas.SetLeft(marker, x); Canvas.SetTop(marker, y);
        marker.Visibility = Visibility.Visible;
    }

    private void OnRelease()
    {
        if (pressed is null) return;
        if (dragging) EndDrag(true);
        else { pressed = null; pressedElement = null; ReleaseMouseCapture(); }
    }

    private void EndDrag(bool apply)
    {
        var item = pressed; var target = drop;
        dragging = false; pressed = null; drop = null;
        if (pressedElement is not null) pressedElement.Opacity = 1;
        pressedElement = null;
        if (ghost is not null) overlay.Children.Remove(ghost);
        ghost = null; marker.Visibility = Visibility.Collapsed;
        if (IsMouseCaptured) ReleaseMouseCapture();
        if (!apply || target is null) return;
        var list = source();
        if (item is Category category)
        {
            var from = list.IndexOf(category);
            var to = target.Value.Index > from ? target.Value.Index - 1 : target.Value.Index;
            if (from >= 0 && to != from) commit(() => ConfigurationEditor.Move(list, category, to), null);
        }
        else if (item is Shortcut shortcut && list.FirstOrDefault(c => c.Shortcuts.Contains(shortcut)) is { } origin)
        {
            var (destination, index) = target.Value;
            var from = origin.Shortcuts.IndexOf(shortcut);
            if (origin == destination && (index == from || index == from + 1)) return;
            commit(() => ConfigurationEditor.MoveShortcut(origin, destination, shortcut, index),
                origin == destination ? null : L.F("Переміщено «{0}» до «{1}»", shortcut.Name, destination.Name));
        }
    }
}
