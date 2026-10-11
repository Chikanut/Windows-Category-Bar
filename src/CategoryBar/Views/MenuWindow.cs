using System.Windows.Shapes;
using CategoryBar.Interop;
using CategoryBar.Services;

namespace CategoryBar.Views;

/// <summary>
/// The column of applications above a category tab. One instance per bar is reused for every category,
/// which keeps opening instant and lets a dragged shortcut travel from one category to another.
/// </summary>
internal sealed class MenuWindow : OverlayWindow
{
    private const double ShadowMargin = 24, Bridge = 8;
    private static double MenuWidth => Theme.StyleSheet.Number("menu", "width", 264);
    private static double RowHeight => Theme.StyleSheet.Number("menu-item", "height", 36);
    private static double RowStride => RowHeight + Theme.StyleSheet.Spacing("menu-item", "margin", 0)[0] + Theme.StyleSheet.Spacing("menu-item", "margin", 0)[2] + Theme.StyleSheet.Number("menu", "gap", 0);
    private readonly Session session;
    private readonly BarWindow bar;
    private readonly Grid frame = new() { Margin = new Thickness(ShadowMargin, ShadowMargin, ShadowMargin, Bridge), Width = MenuWidth };
    private readonly StackPanel body = new();
    private readonly ScrollViewer scroller = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    private readonly Border listHost = new();
    private readonly TextBox search = new() { Tag = L.T("Пошук"), Margin = new Thickness(4, 0, 4, 6), Padding = new Thickness(28, 0, 8, 0) };
    private readonly Grid searchHost = new();
    private Panel rows = new StackPanel();
    private IReadOnlyList<Category>? overflow;
    private PixelRect anchor, monitor;
    private bool editing, dragging;
    private int busy, selected = -1;
    private Shortcut? pressed, dragItem, renaming;
    private Native.Point pressPoint;
    private Category? dragOrigin;
    private int dragOriginIndex;

    internal Category? Category { get; private set; }
    private bool closing;
    internal bool IsOpen => IsVisible && !closing;
    internal bool ShowsOverflow => IsOpen && overflow is not null;
    /// <summary>While pinned the menu ignores the pointer leaving it.</summary>
    internal bool Pinned => editing || dragging || busy > 0 || renaming is not null || search.IsKeyboardFocused;
    protected override bool NeedsKeyboard => editing || renaming is not null || search.IsKeyboardFocused;
    internal bool Dragging => dragging;

    internal MenuWindow(Session session, BarWindow bar)
    {
        this.session = session; this.bar = bar;
        var root = new Grid();
        // An almost transparent strip keeps the pointer "inside" while it crosses from the tab to the menu.
        root.Children.Add(new Rectangle { Height = Bridge + 2, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(ShadowMargin, 0, ShadowMargin, 0), Fill = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)) });
        frame.Res(WidthProperty, "menu.Width");
        frame.Children.Add(new Border().Res(Border.BackgroundProperty, "Menu.Bg").Res(Border.CornerRadiusProperty, "menu.Radius").Res(EffectProperty, "menu.Shadow"));
        frame.Children.Add(new ContentControl { Content = body }.Res(StyleProperty, "Theme.Menu")
            .Res(Control.BackgroundProperty, "Menu.Bg").Res(Control.BorderBrushProperty, "menu.BorderBrush").Res(Control.BorderThicknessProperty, "menu.Border").Res(Control.PaddingProperty, "menu.Padding"));
        root.Children.Add(frame);
        SetRoot(root);

        searchHost.Children.Add(search);
        var glass = Ui.Icon(Icons.Search, "Text.Faint", 13); glass.HorizontalAlignment = HorizontalAlignment.Left; glass.Margin = new Thickness(14, 0, 0, 6);
        searchHost.Children.Add(glass);
        search.TextChanged += (_, _) => { selected = -1; RenderRows(); Reposition(); };
        search.PreviewMouseLeftButtonDown += (_, _) => { if (!IsActive || !search.IsKeyboardFocused) FocusInput(search, selectAll: false); };

        frame.AllowDrop = true;
        frame.DragOver += (_, e) => { e.Effects = Category is not null && DroppedPaths(e.Data).Count > 0 ? DragDropEffects.Link : DragDropEffects.None; e.Handled = true; };
        frame.Drop += (_, e) => { if (Category is not null) session.AddPaths(Category, DroppedPaths(e.Data)); };
        frame.MouseMove += (_, _) => { if (dragging) DragStep(); };
        frame.MouseLeftButtonUp += (_, _) => { if (dragging) EndDrag(true); };
        frame.LostMouseCapture += (_, _) => { if (dragging) EndDrag(false); };

        PreviewKeyDown += OnKey;
        Deactivated += (_, _) => { if (busy == 0 && !dragging && !ReleasingFocus) bar.CloseMenu(); };
        session.Status.Changed += OnStatusChanged;
        Closed += (_, _) => session.Status.Changed -= OnStatusChanged;
    }

    internal static List<string> DroppedPaths(IDataObject data)
    {
        if (data.GetData(DataFormats.FileDrop) is string[] files) return [.. files];
        if (data.GetData(DataFormats.UnicodeText) is string text && LaunchTarget.IsLink(text.Trim())) return [text.Trim()];
        return [];
    }

    // ---- Opening, closing, placement -------------------------------------------------------------

    internal void ShowFor(Category category, PixelRect anchor, PixelRect monitor, bool edit = false)
    {
        if (Category != category || overflow is not null) { search.Text = ""; selected = -1; if (!dragging) editing = false; }
        overflow = null; Category = category; this.anchor = anchor; this.monitor = monitor;
        if (edit) { editing = true; }
        Present();
        if (edit) TakeFocus();
    }

    internal void ShowOverflow(IReadOnlyList<Category> hidden, PixelRect anchor, PixelRect monitor)
    {
        overflow = hidden; Category = null; editing = false; this.anchor = anchor; this.monitor = monitor;
        Present();
    }

    private void Present()
    {
        Rebuild();
        Reposition();
        var wasClosing = closing;
        closing = false; frame.IsHitTestVisible = true;
        if (IsVisible && !wasClosing) { Theming.ThemeMotion.Play(body, "Theme.Menu.Switch", offset: 4); return; }
        Show();
        Reposition();
        Ui.FadeIn(frame, 0, 8, 120, "Theme.Menu.Enter");
    }

    internal void Reanchor(PixelRect anchor, PixelRect monitor)
    {
        this.anchor = anchor; this.monitor = monitor;
        Reposition();
    }

    internal void HideMenu()
    {
        if (dragging) EndDrag(false);
        editing = false; renaming = null; pressed = null; busy = 0;
        ReleaseFocus();
        if (!IsVisible || closing) return;
        closing = true; frame.IsHitTestVisible = false;
        Theming.ThemeMotion.Play(frame, "Theme.Menu.Exit", () => { Hide(); closing = false; frame.IsHitTestVisible = true; }, offset: 4, milliseconds: 100, exit: true);
    }

    /// <summary>Rebuilds after the configuration changed. Returns false when the category no longer exists.</summary>
    internal bool Refresh()
    {
        if (!IsVisible) return true;
        if (overflow is not null) return false;
        if (Category is null || !session.Config.Categories.Contains(Category)) return false;
        if (!dragging) Rebuild();
        return true;
    }

    private void Reposition()
    {
        if (anchor.Width <= 0) return;
        var scale = Dpi * Theme.Scale;
        var available = (anchor.Top - monitor.Top) / scale - 190;
        scroller.MaxHeight = Math.Max(RowStride * 3, Math.Min(RowStride * 10, Math.Floor(available / RowStride) * RowStride));
        var (width, height) = MeasurePixels();
        var shadow = (int)(ShadowMargin * scale);
        var x = (anchor.Left + anchor.Right) / 2 - width / 2;
        x = Math.Clamp(x, monitor.Left + 6 - shadow, Math.Max(monitor.Left + 6 - shadow, monitor.Right - 6 - width + shadow));
        Place(x, anchor.Top - height, width, height);
    }

    // ---- Content ---------------------------------------------------------------------------------

    private void Rebuild()
    {
        body.Children.Clear();
        if (overflow is not null) { BuildOverflow(); return; }
        var category = Category!;
        var header = new Grid { Margin = new Thickness(10, 4, 4, 6), Height = 24 };
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(Ui.Text(category.Name.ToUpper() + (editing ? "  ·  " + L.T("РЕДАГУВАННЯ") : ""), 11, editing ? "Accent" : "Text.Faint", bold: true));
        if (editing)
        {
            var done = Ui.Button(L.T("Готово"), () => { editing = false; ReleaseFocus(); Rebuild(); Reposition(); }, "Button.Primary");
            done.MinHeight = 22; done.Height = 22; done.FontSize = 11.5; done.Padding = new Thickness(9, 0, 9, 0); done.Focusable = false;
            header.Children.Add(done.At(1));
        }
        else
        {
            var tools = new StackPanel { Orientation = Orientation.Horizontal };
            var count = Ui.Text(category.Shortcuts.Count.ToString(), 11, "Text.Faint"); count.Margin = new Thickness(0, 0, 4, 0);
            tools.Children.Add(count);
            var edit = Ui.IconButton(Icons.Pencil, L.T("Редагувати ярлики"), () => { editing = true; TakeFocus(); Rebuild(); Reposition(); }, "Text.Faint");
            edit.Width = edit.Height = edit.MinHeight = 22;
            ((FrameworkElement)edit.Content).LayoutTransform = new ScaleTransform(.8, .8);
            tools.Children.Add(edit);
            header.Children.Add(tools.At(1));
        }
        body.Children.Add(header);

        var threshold = session.Settings.SearchThreshold;
        if (threshold > 0 && category.Shortcuts.Count >= threshold && !editing)
        {
            body.Children.Add(searchHost);
        }
        else if (search.Text.Length > 0) search.Text = "";

        listHost.Child = null;
        body.Children.Add(listHost);
        RenderRows();

        body.Children.Add(new Border { Height = 1, Margin = new Thickness(4) }.Res(Border.BackgroundProperty, "Divider"));
        var add = ActionRow(Icons.Plus, L.T("Додати застосунок"), () => { var target = category; bar.CloseMenu(); AppPickerWindow.Open(session, target); });
        body.Children.Add(add);
        body.Children.Add(ActionRow(Icons.Apps, L.T("Створити набір"), () => { var target = category; bar.CloseMenu(); SetWindow.Open(session, target); }));
    }

    private Border ActionRow(string icon, string text, Action click)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        var glyph = Ui.Icon(icon, "Text.Muted", 14); glyph.Margin = new Thickness(5, 0, 15, 0);
        content.Children.Add(glyph);
        content.Children.Add(Ui.Text(text, 12.5, "Text.Muted"));
        var row = new Border { Height = 32, Padding = new Thickness(10, 0, 10, 0), Background = Brushes.Transparent, Child = content }.Res(Border.CornerRadiusProperty, "Radius.Small");
        row.MouseEnter += (_, _) => row.SetResourceReference(Border.BackgroundProperty, "Hover");
        row.MouseLeave += (_, _) => row.Background = Brushes.Transparent;
        row.MouseLeftButtonUp += (_, _) => click();
        return row;
    }

    private void BuildOverflow()
    {
        var header = Ui.Text(L.T("ІНШІ КАТЕГОРІЇ"), 11, "Text.Faint", bold: true); header.Margin = new Thickness(10, 8, 10, 10);
        body.Children.Add(header);
        foreach (var category in overflow!)
        {
            var line = new Grid();
            line.ColumnDefinitions.Add(new ColumnDefinition());
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            line.Children.Add(Ui.Text(category.Name));
            var count = Ui.Text(category.Shortcuts.Count.ToString(), 11, "Text.Faint"); count.Margin = new Thickness(8, 0, 6, 0);
            line.Children.Add(count.At(1));
            line.Children.Add(Ui.Icon(Icons.ChevronRight, "Text.Faint", 10).At(2));
            var row = new Border { Height = 32, Padding = new Thickness(10, 0, 10, 0), Background = Brushes.Transparent, Child = line }.Res(Border.CornerRadiusProperty, "Radius.Small");
            row.MouseEnter += (_, _) => row.SetResourceReference(Border.BackgroundProperty, "Hover");
            row.MouseLeave += (_, _) => row.Background = Brushes.Transparent;
            row.MouseLeftButtonUp += (_, _) => bar.OpenHidden(category);
            body.Children.Add(row);
        }
    }

    private List<Shortcut> Visible()
    {
        var query = search.Text.Trim();
        return query.Length == 0 ? Category!.Shortcuts : Category!.Shortcuts.Where(s => s.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)).ToList();
    }

    private bool UseGrid => session.Settings.GridForLargeCategories && Category!.Shortcuts.Count >= 20 && !editing;

    private void RenderRows()
    {
        if (Category is null) return;
        var offset = scroller.VerticalOffset;
        var items = Visible();
        if (Category.Shortcuts.Count == 0)
        {
            var empty = new StackPanel { Margin = new Thickness(14, 20, 14, 20) };
            var title = Ui.Text(L.T("Тут поки порожньо"), 13); title.HorizontalAlignment = HorizontalAlignment.Center;
            var hint = Ui.Text(L.T("Перетягніть сюди ярлик, .exe або папку"), 12, "Text.Faint", wrap: true); hint.TextAlignment = TextAlignment.Center; hint.Margin = new Thickness(0, 6, 0, 0);
            empty.Children.Add(title); empty.Children.Add(hint);
            var dashed = new Rectangle { StrokeDashArray = [4, 3], StrokeThickness = 1, RadiusX = 8, RadiusY = 8 }.Res(Shape.StrokeProperty, "Text.Faint");
            dashed.Opacity = .6;
            listHost.Child = new Grid { Margin = new Thickness(4, 2, 4, 6), Children = { dashed, empty } };
            return;
        }
        if (items.Count == 0)
        {
            var none = Ui.Text(L.F("Нічого не знайдено за «{0}»", search.Text.Trim()), 12.5, "Text.Faint", wrap: true);
            none.TextAlignment = TextAlignment.Center; none.Margin = new Thickness(10, 18, 10, 18);
            listHost.Child = none;
            return;
        }
        rows = UseGrid ? new WrapPanel { Margin = new Thickness(2, 0, 2, 0) } : new StackPanel();
        for (var i = 0; i < items.Count; i++) rows.Children.Add(UseGrid ? Tile(items[i]) : Row(items[i], i == selected));
        scroller.Content = rows;
        listHost.Child = scroller;
        scroller.ScrollToVerticalOffset(offset);
    }

    private FrameworkElement StatusIcon(Shortcut shortcut)
    {
        var status = session.Status;
        FrameworkElement? icon = status.IsBroken(shortcut) ? Ui.Icon(Icons.Warning, "Warning", 14)
            : status.IsLaunching(shortcut) ? Ui.Spinner()
            : shortcut.RunAsAdministrator ? Ui.Icon(Icons.Shield, "Accent", 14) : null;
        if (icon is null) return new Grid();
        icon.Margin = new Thickness(8, 0, 0, 0);
        return icon;
    }

    /// <summary>The icon with room below it for the "running" mark, like the taskbar's own indicator.</summary>
    private Grid IconWithStatus(Shortcut shortcut, double size)
    {
        var host = new Grid { Width = size, Height = size + 10 };
        var icon = Ui.ShortcutIcon(shortcut, size);
        if (session.Status.IsBroken(shortcut)) icon.Opacity = .4;
        host.Children.Add(icon);
        if (session.Settings.ShowRunningIndicators && session.Status.IsRunning(shortcut))
            host.Children.Add(new Border { Width = 10, Height = 3, CornerRadius = new CornerRadius(1.5), VerticalAlignment = VerticalAlignment.Bottom }.Res(Border.BackgroundProperty, "Accent"));
        return host;
    }

    private string Tip(Shortcut shortcut) => shortcut.IsSet ? shortcut.Name + "\n" + L.F("Набір: {0}", string.Join(", ", shortcut.Steps.Select(s => s.Name)))
            + (shortcut.Steps.Any(s => s.Placement is not null) ? "\n" + L.T("Вікна стануть на запам’ятані місця") : "")
        : session.Status.IsBroken(shortcut) ? shortcut.Name + "\n" + L.T("Файл не знайдено")
        : session.Status.IsRunning(shortcut) ? shortcut.Name + "\n" + L.T("Запущено — клацніть, щоб перейти до вікна") : shortcut.Name;

    private ContentControl Row(Shortcut shortcut, bool highlighted)
    {
        var line = new Grid();
        foreach (var width in new[] { GridLength.Auto, GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto, GridLength.Auto })
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = width });
        if (editing)
        {
            var grip = Ui.Icon(Icons.Grip, "Text.Faint"); grip.Margin = new Thickness(-4, 0, 6, 0);
            line.Children.Add(grip);
        }
        line.Children.Add(IconWithStatus(shortcut, Theme.StyleSheet.Number("menu-item", "icon-size", 24)).At(1));
        FrameworkElement name;
        if (renaming == shortcut) name = RenameBox(shortcut);
        else
        {
            name = Ui.Text(shortcut.Name, 13, session.Status.IsBroken(shortcut) ? "Text.Faint" : "Text");
            name.Margin = new Thickness(10, 0, 0, 0);
        }
        line.Children.Add(name.At(2));
        line.Children.Add(StatusIcon(shortcut).At(3));
        if (editing)
        {
            var remove = Ui.IconButton(Icons.Close, L.T("Прибрати з категорії"), () => session.RemoveShortcut(Category!, shortcut), "Text.Faint");
            remove.Width = remove.Height = remove.MinHeight = 24; remove.Margin = new Thickness(6, 0, -4, 0);
            line.Children.Add(remove.At(4));
        }
        var row = new ContentControl { Height = RowHeight, Content = line, Tag = shortcut, ToolTip = Tip(shortcut) }.Res(StyleProperty, "Theme.MenuItem")
            .Res(Control.PaddingProperty, "menu-item.Padding").Res(MarginProperty, "menu-item.Margin")
            .Res(Control.BorderThicknessProperty, "menu-item.Border").Res(Control.BorderBrushProperty, "menu-item.BorderBrush");
        ToolTipService.SetInitialShowDelay(row, 700);
        Theming.ThemeVisual.SetIsSelected(row, highlighted);
        Wire(row, shortcut);
        return row;
    }

    private ContentControl Tile(Shortcut shortcut)
    {
        var content = new StackPanel { Margin = new Thickness(2, 6, 2, 6) };
        content.Children.Add(IconWithStatus(shortcut, 28));
        var name = Ui.Text(shortcut.Name, 10, "Text.Muted"); name.TextAlignment = TextAlignment.Center; name.Margin = new Thickness(0, 0, 0, 0);
        content.Children.Add(name);
        var tile = new ContentControl { Width = (MenuWidth - 18) / 5, Content = content, Tag = shortcut, ToolTip = Tip(shortcut) }.Res(StyleProperty, "Theme.MenuItem");
        Wire(tile, shortcut);
        return tile;
    }

    private void Wire(ContentControl row, Shortcut shortcut)
    {
        row.MouseLeftButtonDown += (_, e) =>
        {
            if (renaming is not null) return;
            pressed = shortcut; pressPoint = Native.Cursor();
            if (e.ClickCount == 2 && editing) { pressed = null; BeginRename(shortcut); }
        };
        row.MouseMove += (_, e) =>
        {
            if (pressed != shortcut || dragging || e.LeftButton != MouseButtonState.Pressed) return;
            var now = Native.Cursor();
            if (Math.Abs(now.X - pressPoint.X) + Math.Abs(now.Y - pressPoint.Y) > 6 * Dpi) BeginDrag(shortcut, row);
        };
        row.MouseLeftButtonUp += (_, _) =>
        {
            if (pressed != shortcut || dragging) return;
            pressed = null;
            if (editing) return;
            var modifiers = Keyboard.Modifiers;
            session.Launch(shortcut, modifiers.HasFlag(ModifierKeys.Control) && modifiers.HasFlag(ModifierKeys.Shift), modifiers.HasFlag(ModifierKeys.Shift));
            bar.CloseMenu();
        };
        row.MouseDown += (_, e) =>
        {
            if (e.ChangedButton != MouseButton.Middle) return;
            session.Launch(shortcut, newInstance: true);
            bar.CloseMenu();
        };
        row.MouseRightButtonUp += (_, e) => { e.Handled = true; OpenContextMenu(shortcut, row); };
    }

    // ---- Context menu and rename -----------------------------------------------------------------

    private void OpenContextMenu(Shortcut shortcut, FrameworkElement row)
    {
        var category = Category!;
        var running = session.Status.IsRunning(shortcut);
        var isFile = !shortcut.IsSet && !LaunchTarget.IsShellApplication(shortcut.Target) && !LaunchTarget.IsLink(shortcut.Target);
        var menu = new ContextMenu();
        if (shortcut.IsSet)
        {
            menu.Items.Add(Ui.Item(L.T("Запустити набір"), () => { session.Launch(shortcut); bar.CloseMenu(); }, Icons.Play));
            menu.Items.Add(Ui.Item(L.T("Запустити все заново"), () => { session.Launch(shortcut, newInstance: true); bar.CloseMenu(); }, Icons.External, gesture: L.T("Shift+клік")));
            menu.Items.Add(Ui.Item(L.T("Запам’ятати розташування вікон"), () => { bar.CloseMenu(); session.CaptureLayout(shortcut); }, Icons.Layout));
        }
        else if (running)
        {
            menu.Items.Add(Ui.Item(L.T("Перейти до вікна"), () => { session.Launch(shortcut); bar.CloseMenu(); }, Icons.External));
            menu.Items.Add(Ui.Item(L.T("Відкрити ще одне вікно"), () => { session.Launch(shortcut, newInstance: true); bar.CloseMenu(); }, Icons.Play, gesture: L.T("Shift+клік")));
        }
        else menu.Items.Add(Ui.Item(L.T("Запустити"), () => { session.Launch(shortcut); bar.CloseMenu(); }, Icons.Play));
        if (!shortcut.IsSet)
        {
            menu.Items.Add(Ui.Item(L.T("Від імені адміністратора"), () => { session.Launch(shortcut, asAdministrator: true); bar.CloseMenu(); }, Icons.Shield, enabled: isFile));
            menu.Items.Add(Ui.Item(L.T("Відкрити розташування файлу"), () => { Shortcuts.RevealInExplorer(shortcut); bar.CloseMenu(); }, Icons.Folder, enabled: isFile));
        }
        menu.Items.Add(new Separator());
        menu.Items.Add(Ui.Item(L.T("Перейменувати"), () => BeginRename(shortcut), Icons.Pencil, gesture: "F2"));
        var move = new MenuItem { Header = L.T("Перемістити до"), Icon = Ui.Icon(Icons.Move) };
        foreach (var other in session.Config.Categories.Where(c => c != category))
            move.Items.Add(Ui.Item(other.Name, () => MoveTo(shortcut, category, other)));
        if (move.Items.Count > 0) move.Items.Add(new Separator());
        move.Items.Add(Ui.Item(L.T("Нова категорія"), () =>
        {
            var created = new Category { Name = Ui.FreshCategoryName(session.Config.Categories), Shortcuts = [] };
            session.Change(() => { category.Shortcuts.Remove(shortcut); created.Shortcuts.Add(shortcut); session.Config.Categories.Add(created); },
                L.F("Переміщено «{0}»", shortcut.Name), L.F("До нової категорії «{0}».", created.Name));
        }, Icons.Plus));
        menu.Items.Add(move);
        if (shortcut.IsSet) menu.Items.Add(Ui.Item(L.T("Розібрати набір"), () => session.Ungroup(category, shortcut), Icons.Apps));
        else menu.Items.Add(Ui.Item(L.T("Властивості ярлика…"), () => { bar.CloseMenu(); Dialogs.Properties(session, shortcut); }, Icons.Sliders));
        menu.Items.Add(new Separator());
        menu.Items.Add(Ui.Item(L.T("Прибрати з категорії"), () => session.RemoveShortcut(category, shortcut), Icons.Trash, danger: true));
        busy++;
        menu.Closed += (_, _) => busy = Math.Max(0, busy - 1);
        OpenMenu(menu, row);
    }

    private void MoveTo(Shortcut shortcut, Category from, Category to) =>
        session.Change(() => { from.Shortcuts.Remove(shortcut); to.Shortcuts.Add(shortcut); }, L.F("Переміщено «{0}»", shortcut.Name), L.F("До категорії «{0}».", to.Name));

    private void BeginRename(Shortcut shortcut)
    {
        renaming = shortcut;
        search.Text = "";
        RenderRows();
    }

    private TextBox RenameBox(Shortcut shortcut)
    {
        var box = new TextBox { Text = shortcut.Name, MaxLength = 100, Margin = new Thickness(6, 0, 0, 0), MinHeight = 26, Height = 26 };
        var finished = false;
        void Finish(bool save)
        {
            if (finished) return;
            finished = true; renaming = null;
            var name = box.Text.Trim();
            if (save && name.Length > 0 && name != shortcut.Name) session.Change(() => shortcut.Name = name);
            else RenderRows();
            if (!editing) ReleaseFocus();
        }
        box.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { e.Handled = true; Finish(true); }
            else if (e.Key == Key.Escape) { e.Handled = true; Finish(false); }
        };
        box.LostKeyboardFocus += (_, _) => Finish(true);
        FocusInput(box);
        return box;
    }

    // ---- Keyboard --------------------------------------------------------------------------------

    private void OnKey(object sender, KeyEventArgs e)
    {
        if (renaming is not null || Category is null) return;
        var items = Visible();
        switch (e.Key)
        {
            case Key.Escape:
                e.Handled = true;
                if (editing) { editing = false; ReleaseFocus(); Rebuild(); Reposition(); } else bar.CloseMenu();
                break;
            case Key.Down or Key.Up when items.Count > 0 && !UseGrid:
                e.Handled = true;
                selected = (selected + (e.Key == Key.Down ? 1 : -1) + items.Count + (selected < 0 && e.Key == Key.Up ? 1 : 0)) % items.Count;
                RenderRows();
                (rows.Children[selected] as FrameworkElement)?.BringIntoView();
                break;
            case Key.Enter when items.Count > 0 && !editing:
                e.Handled = true;
                session.Launch(items[Math.Max(0, selected)]);
                bar.CloseMenu();
                break;
            case Key.F2 when selected >= 0 && selected < items.Count:
                e.Handled = true;
                BeginRename(items[selected]);
                break;
        }
    }

    private void OnStatusChanged()
    {
        if (IsVisible && overflow is null && !dragging && renaming is null && Category is not null) RenderRows();
    }

    // ---- Drag and drop reordering ----------------------------------------------------------------
    // The shortcut moves live inside the model while it is dragged. On release the original position is
    // restored and the final move is applied as one saved, undoable change.

    private void BeginDrag(Shortcut shortcut, ContentControl row)
    {
        if (search.Text.Trim().Length > 0 || Category is null) return;
        dragging = true; dragItem = shortcut; pressed = null;
        dragOrigin = Category; dragOriginIndex = Category.Shortcuts.IndexOf(shortcut);
        MarkDragged(row);
        frame.CaptureMouse();
        Mouse.OverrideCursor = Cursors.SizeAll;
    }

    private static void MarkDragged(ContentControl row)
    {
        Theming.ThemeVisual.SetIsDropTarget(row, true);
    }

    private void DragStep()
    {
        var item = dragItem!;
        var over = bar.CategoryAt(Native.Cursor());
        if (over is not null && over != Category)
        {
            Category!.Shortcuts.Remove(item);
            over.Shortcuts.Add(item);
            bar.ShowCategory(over);
            if (rows.Children.OfType<ContentControl>().FirstOrDefault(r => r.Tag == item) is { } moved) { MarkDragged(moved); moved.BringIntoView(); }
            return;
        }
        var list = Category!.Shortcuts;
        var current = list.IndexOf(item);
        if (current < 0 || rows.Children.Count != list.Count) return;
        var pointer = Mouse.GetPosition(rows);
        var target = current;
        if (rows is WrapPanel)
        {
            for (var i = 0; i < rows.Children.Count; i++)
            {
                var cell = (FrameworkElement)rows.Children[i];
                var origin = cell.TranslatePoint(new Point(0, 0), rows);
                if (pointer.X >= origin.X && pointer.X < origin.X + cell.ActualWidth && pointer.Y >= origin.Y && pointer.Y < origin.Y + cell.ActualHeight) { target = i; break; }
            }
        }
        else
        {
            target = Math.Clamp((int)Math.Floor(pointer.Y / RowStride), 0, list.Count - 1);
            // Keep the list moving when the pointer is held near an edge of a long, scrolled list.
            var inView = Mouse.GetPosition(scroller).Y;
            if (inView < 14) scroller.ScrollToVerticalOffset(scroller.VerticalOffset - 12);
            else if (inView > scroller.ActualHeight - 14) scroller.ScrollToVerticalOffset(scroller.VerticalOffset + 12);
        }
        if (target == current) return;
        list.RemoveAt(current); list.Insert(target, item);
        var element = rows.Children[current];
        rows.Children.RemoveAt(current); rows.Children.Insert(target, element);
    }

    private void EndDrag(bool commit)
    {
        var item = dragItem!; var origin = dragOrigin!; var destination = Category!;
        dragging = false; dragItem = null; dragOrigin = null;
        Mouse.OverrideCursor = null;
        if (frame.IsMouseCaptured) frame.ReleaseMouseCapture();
        var index = destination.Shortcuts.IndexOf(item);
        destination.Shortcuts.Remove(item);
        origin.Shortcuts.Insert(Math.Min(dragOriginIndex, origin.Shortcuts.Count), item);
        var moved = destination != origin || index != dragOriginIndex;
        if (commit && moved && index >= 0)
        {
            var originIndex = dragOriginIndex;
            var done = session.Change(() =>
            {
                origin.Shortcuts.Remove(item);
                destination.Shortcuts.Insert(Math.Min(index, destination.Shortcuts.Count), item);
            }, destination != origin ? L.F("Переміщено «{0}»", item.Name) : null, destination != origin ? L.F("До категорії «{0}».", destination.Name) : "");
            if (done) return;
        }
        Rebuild();
    }
}
