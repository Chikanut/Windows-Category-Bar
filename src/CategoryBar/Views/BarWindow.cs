using System.Windows.Shapes;
using System.Windows.Threading;
using CategoryBar.Interop;
using Microsoft.Win32;

namespace CategoryBar.Views;

/// <summary>
/// The strip of category tabs that sits on top of a Windows taskbar and follows its visibility.
/// The primary bar tracks Shell_TrayWnd (which is recreated when Explorer restarts); secondary bars are
/// bound to one secondary taskbar and close themselves when it disappears.
/// </summary>
internal sealed class BarWindow : OverlayWindow
{
    private sealed class Tab
    {
        public Category? Category;
        public required ContentControl Element;
        public Grid NameHost = new();
        public bool DropTarget;
    }

    private readonly Session session;
    private readonly IntPtr boundTaskbar;
    private readonly StackPanel tabs = new() { Orientation = Orientation.Horizontal };
    private readonly ContentControl surface;
    private readonly MenuWindow menu;
    private readonly DispatcherTimer tracker = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly DispatcherTimer openTimer = new(), closeTimer = new(), dropTimer = new() { Interval = TimeSpan.FromMilliseconds(600) };
    private readonly List<Tab> views = [];
    private List<Category> hidden = [];
    private Tab? overflowTab, pending, pressedTab;
    private ToastWindow? toast;
    private AppBarReservation? reservation;
    private PixelRect monitor, taskbar;
    private Native.Point pressPoint;
    private DateTime openedAt;
    private bool renaming, reordering, expanded, sliding, escapeDown, contextOpen, dragHover;
    private int idleTicks, staleRenameTicks, lastX = int.MinValue, lastY, lastWidth, lastHeight;

    protected override bool NeedsKeyboard => renaming;
    internal bool IsPrimary => boundTaskbar == IntPtr.Zero;
    internal IntPtr BoundTaskbar => boundTaskbar;
    internal bool HiddenByUser { get; set; }

    internal BarWindow(Session session, IntPtr secondaryTaskbar = default, bool trackTaskbar = true)
    {
        this.session = session; boundTaskbar = secondaryTaskbar;
        surface = new ContentControl { Content = tabs }.Res(StyleProperty, "Theme.Bar")
            .Res(Control.BackgroundProperty, "Bar.Bg").Res(Control.BorderBrushProperty, "bar.BorderBrush").Res(Control.PaddingProperty, "bar.Padding");
        SetRoot(surface);
        menu = new MenuWindow(session, this);
        surface.AllowDrop = true;
        surface.PreviewDragEnter += (_, e) => dragHover = MenuWindow.DroppedPaths(e.Data).Count > 0;
        surface.PreviewDragOver += (_, e) => dragHover = MenuWindow.DroppedPaths(e.Data).Count > 0;
        surface.PreviewDragLeave += (_, _) => dragHover = false;
        surface.PreviewDrop += (_, _) => dragHover = false;
        // The gaps reveal/keep the bar but do not accept a drop without a category.
        surface.DragOver += (_, e) => { e.Effects = DragDropEffects.None; e.Handled = true; };
        surface.Drop += (_, e) => { e.Effects = DragDropEffects.None; e.Handled = true; };

        surface.MouseRightButtonUp += (_, e) => { if (!e.Handled) OpenBarMenu(); };
        openTimer.Tick += (_, _) =>
        {
            openTimer.Stop();
            if (pending is null || !views.Contains(pending)) return;
            if (pending == overflowTab) OpenOverflow(); else Open(pending);
        };
        closeTimer.Tick += (_, _) => { closeTimer.Stop(); if (!menu.Pinned) CloseMenu(); };
        dropTimer.Tick += (_, _) => { dropTimer.Stop(); if (views.FirstOrDefault(v => v.DropTarget) is { Category: not null } target) Open(target); };
        tracker.Tick += (_, _) => Track();

        session.Changed += OnChanged;
        session.Status.Changed += UpdateStates;
        Theme.Changed += OnThemeChanged;
        Closed += (_, _) =>
        {
            tracker.Stop(); openTimer.Stop(); closeTimer.Stop(); dropTimer.Stop();
            session.Changed -= OnChanged; session.Status.Changed -= UpdateStates; Theme.Changed -= OnThemeChanged;
            reservation?.Dispose(); menu.Close(); toast?.Close();
        };
        if (trackTaskbar) { Track(); tracker.Start(); }
        else Rebuild();
    }

    private void OnThemeChanged() { lastX = int.MinValue; Dispatcher.BeginInvoke(Rebuild, DispatcherPriority.Loaded); }

    private void OnChanged()
    {
        Rebuild();
        if (!menu.IsOpen) return;
        if (!menu.Refresh()) { CloseMenu(); return; }
        UpdateLayout();
        if (menu.Category is { } category) menu.Reanchor(AnchorFor(category), monitor);
        UpdateStates();
    }

    // ---- Following the taskbar -------------------------------------------------------------------

    private void Track()
    {
        var window = IsPrimary ? Native.FindWindow("Shell_TrayWnd", null) : boundTaskbar;
        if (!IsPrimary && !Native.IsWindow(window)) { Close(); return; }
        var observed = Native.Observe(window);
        var interacting = dragHover || renaming || reordering || contextOpen || menu.IsOpen && (menu.Pinned || menu.ContainsCursor() || ContainsCursor());
        if (VisibilityPolicy.ShouldShow(observed) && !HiddenByUser)
        {
            var moved = observed.Monitor != monitor || observed.Taskbar != taskbar;
            var resized = observed.Monitor.Width != monitor.Width;
            monitor = observed.Monitor; taskbar = observed.Taskbar;
            if (!IsVisible)
            {
                Rebuild();
                Show();
                Arrange();
                Ui.FadeIn(surface, 0, 6, 140, "Theme.Bar.Enter");
            }
            else if (resized) Rebuild();
            else if (moved) Arrange();
        }
        else if (IsVisible && !(interacting && observed.Exists && !observed.FullscreenForeground && !HiddenByUser))
        {
            // An auto-hidden taskbar slides away as soon as the pointer moves up into a menu; the bar stays
            // for as long as the user is working with it, and leaves together with the taskbar otherwise.
            CloseMenu();
            Hide();
            toast?.Dismiss();
        }
        if (!IsVisible) { ReleaseReservation(); return; }

        if (session.Settings.ReserveSpace)
        {
            reservation ??= new AppBarReservation();
            reservation.Update(monitor, Bounds.Height, taskbar.Top);
        }
        else ReleaseReservation();

        // A rename box that never received, or has lost, the keyboard must not linger on the bar.
        staleRenameTicks = renaming && Native.GetForegroundWindow() != Handle ? staleRenameTicks + 1 : 0;
        if (staleRenameTicks > 4) { staleRenameTicks = 0; Rebuild(); }

        var escape = (Native.GetAsyncKeyState(0x1B) & 0x8000) != 0;
        if (escape && !escapeDown && menu.IsOpen && !menu.IsActive) CloseMenu();
        escapeDown = escape;

        if (menu.IsOpen && !menu.Pinned)
        {
            if (ContainsCursor() || menu.ContainsCursor()) closeTimer.Stop();
            else if (!closeTimer.IsEnabled) { closeTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(1, session.Settings.CloseDelayMs)); closeTimer.Start(); }
        }
        if (session.Settings.MiniatureMode && expanded)
        {
            idleTicks = interacting || menu.IsOpen || ContainsCursor(6) ? 0 : idleTicks + 1;
            if (idleTicks > 6 && !sliding)
            {
                // Slide the tabs down behind the taskbar, then let the small handle rise in their place.
                idleTicks = 0;
                Slide(false, () => { expanded = false; Rebuild(); Slide(true); });
            }
        }
    }

    /// <summary>
    /// Moves the bar vertically inside its own window. The window ends at the top edge of the taskbar,
    /// so the bar appears to slide out from behind the taskbar and back.
    /// </summary>
    private void Slide(bool up, Action? then = null)
    {
        UpdateLayout();
        var distance = Math.Max(surface.ActualHeight, Theme.StyleSheet.Number("bar", "height", 34));
        sliding = true;
        Theming.ThemeMotion.Play(surface, up ? "Theme.Bar.Expand" : "Theme.Bar.Collapse",
            () => { sliding = false; then?.Invoke(); }, offset: distance, milliseconds: up ? 180 : 140, exit: !up);
    }

    private void ReleaseReservation() { reservation?.Dispose(); reservation = null; }

    private void Arrange()
    {
        if (monitor.Width <= 0) return;
        var (width, height) = MeasurePixels();
        var alignment = session.Settings.Alignment;
        if (alignment == "Windows")
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced");
            alignment = key?.GetValue("TaskbarAl") is int value && value == 0 ? "Left" : "Center";
        }
        var gap = (int)(12 * Dpi);
        var x = alignment switch
        {
            "Left" => monitor.Left + gap,
            "Right" => monitor.Right - gap - width,
            _ => monitor.Left + (monitor.Width - width) / 2
        };
        var y = taskbar.Top - height;
        if (x == lastX && y == lastY && width == lastWidth && height == lastHeight) return;
        (lastX, lastY, lastWidth, lastHeight) = (x, y, width, height);
        Place(x, y, width, height);
    }

    // ---- Tabs ------------------------------------------------------------------------------------

    private void Rebuild()
    {
        if (reordering) return;
        tabs.Children.Clear(); views.Clear(); hidden = []; overflowTab = null; pending = null; renaming = false;
        var categories = session.Config.Categories;
        var tucked = session.Settings.MiniatureMode && !expanded;
        surface.Height = tucked ? 16 : Theme.StyleSheet.Number("bar", "height", 34);
        surface.BorderThickness = new Thickness(Theme.StyleSheet.Number("bar", "border-width", 1));
        if (tucked) surface.Padding = new Thickness(2, 2, 2, 0);
        else surface.SetResourceReference(Control.PaddingProperty, "bar.Padding");
        if (tucked)
        {
            // Only a small arrow peeks out above the taskbar; the tabs stay tucked behind it.
            var handle = Chrome(Ui.Icon(Icons.ChevronUp, "Text.Muted", 12), null);
            handle.Element.Height = 13;
            handle.Element.Padding = new Thickness(22, 0, 22, 0);
            handle.Element.MouseEnter += (_, _) => ExpandMiniature();
            handle.Element.AllowDrop = true;
            void RevealForDrag(object sender, DragEventArgs e)
            {
                e.Effects = ShortcutDragEffect(e);
                e.Handled = true;
                if (e.Effects != DragDropEffects.None) { dragHover = true; ExpandMiniature(); }
            }
            handle.Element.DragEnter += RevealForDrag;
            handle.Element.DragOver += RevealForDrag;
            tabs.Children.Add(handle.Element);
            Arrange();
            return;
        }
        if (categories.Count == 0)
        {
            var first = new StackPanel { Orientation = Orientation.Horizontal };
            var plus = Ui.Icon(Icons.Plus, "Accent", 14); plus.Margin = new Thickness(0, 0, 8, 0);
            first.Children.Add(plus); first.Children.Add(Ui.Text(L.T("Створити першу категорію"), 13, "Accent"));
            var outline = new Rectangle { StrokeDashArray = [4, 3], StrokeThickness = 1, RadiusX = 6, RadiusY = 6, Opacity = .7, IsHitTestVisible = false }.Res(Shape.StrokeProperty, "Accent");
            var create = Chrome(new Grid { Children = { outline, new Border { Padding = new Thickness(14, 0, 14, 0), Child = first } } }, null);
            create.Element.Padding = new Thickness(0);
            create.Element.MouseLeftButtonUp += (_, _) => CreateCategory();
            tabs.Children.Add(create.Element);
            Arrange();
            return;
        }

        var available = monitor.Width / (Dpi * Theme.Scale) - 48 - 120;
        double used = 0;
        for (var i = 0; i < categories.Count; i++)
        {
            var tab = CategoryTab(categories[i]);
            tab.Element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            used += tab.Element.DesiredSize.Width;
            if (used > available && i > 0) { hidden = categories.Skip(i).ToList(); break; }
            views.Add(tab); tabs.Children.Add(tab.Element);
        }
        if (hidden.Count > 0)
        {
            var more = new StackPanel { Orientation = Orientation.Horizontal };
            more.Children.Add(Ui.Text(L.F("Ще {0}", hidden.Count), 13, "Text.Muted"));
            var chevron = Ui.Icon(Icons.ChevronUp, "Text.Muted", 12); chevron.Margin = new Thickness(6, 0, -2, 0);
            more.Children.Add(chevron);
            overflowTab = Chrome(more, null);
            var tab = overflowTab;
            tab.Element.MouseEnter += (_, _) => { if (menu.IsOpen && !menu.Pinned) OpenOverflow(); else { pending = tab; StartOpenTimer(); } };
            tab.Element.MouseLeave += (_, _) => { if (pending == tab) openTimer.Stop(); };
            tab.Element.MouseLeftButtonUp += (_, _) =>
            {
                openTimer.Stop();
                if (menu.ShowsOverflow && (DateTime.UtcNow - openedAt).TotalMilliseconds > 450) CloseMenu(); else OpenOverflow();
            };
            views.Add(tab); tabs.Children.Add(tab.Element);
        }
        var add = Chrome(Ui.Icon(Icons.Plus, "Text.Faint", 13), L.T("Нова категорія"));
        add.Element.Padding = new Thickness(8, 0, 8, 0);
        add.Element.MouseLeftButtonUp += (_, _) => CreateCategory();
        tabs.Children.Add(add.Element);
        UpdateStates();
        Arrange();
    }

    private void ExpandMiniature()
    {
        if (sliding || expanded || !session.Settings.MiniatureMode) return;
        expanded = true;
        idleTicks = 0;
        Rebuild();
        Slide(true);
    }

    private static DragDropEffects ShortcutDragEffect(DragEventArgs e)
    {
        if (MenuWindow.DroppedPaths(e.Data).Count == 0) return DragDropEffects.None;
        if ((e.AllowedEffects & DragDropEffects.Link) != 0) return DragDropEffects.Link;
        return (e.AllowedEffects & DragDropEffects.Copy) != 0 ? DragDropEffects.Copy : DragDropEffects.None;
    }

    /// <summary>A tab-shaped element that is not a category: the add button, overflow and miniature handle.</summary>
    private Tab Chrome(UIElement content, string? tip)
    {
        var element = new ContentControl { Content = content, ToolTip = tip }.Res(StyleProperty, "Theme.Tab")
            .Res(HeightProperty, "tab.Height").Res(Control.PaddingProperty, "tab.Padding").Res(MarginProperty, "tab.Margin")
            .Res(Control.BorderThicknessProperty, "tab.Border").Res(Control.BorderBrushProperty, "tab.BorderBrush");
        var tab = new Tab { Element = element };
        element.Loaded += (_, _) => Theming.ThemeMotion.Play(element, "Theme.Tab.Enter", offset: 4);
        return tab;
    }

    private Tab CategoryTab(Category category)
    {
        var name = Ui.Text(category.Name); name.MaxWidth = 150;
        name.Res(TextBlock.FontSizeProperty, "tab.FontSize").Res(TextBlock.FontWeightProperty, "tab.FontWeight");
        var host = new Grid { Children = { name } };
        var label = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        label.Children.Add(host);
        if (session.Settings.ShowCounts)
        {
            var count = Ui.Text(category.Shortcuts.Count.ToString(), 11, "Text.Faint"); count.Margin = new Thickness(6, 1, 0, 0);
            label.Children.Add(count);
        }
        var tab = Chrome(label, null);
        tab.Category = category; tab.NameHost = host;
        var element = tab.Element;
        element.MouseEnter += (_, _) =>
        {
            if (renaming || reordering || menu.Dragging) return;
            if (menu.IsOpen) { if (!menu.Pinned && menu.Category != category) Open(tab); }
            else { pending = tab; StartOpenTimer(); }
        };
        element.MouseLeave += (_, _) => { if (pending == tab) openTimer.Stop(); };
        element.MouseLeftButtonDown += (_, e) =>
        {
            if (renaming) return;
            if (e.ClickCount == 2) { pressedTab = null; BeginRename(tab); return; }
            pressedTab = tab; pressPoint = Native.Cursor();
            element.CaptureMouse();
        };
        element.MouseMove += (_, _) => DragTab(tab);
        element.MouseLeftButtonUp += (_, _) =>
        {
            if (pressedTab != tab) return;
            pressedTab = null;
            element.ReleaseMouseCapture();
            if (reordering) { EndReorder(tab); return; }
            openTimer.Stop();
            if (menu.IsOpen && menu.Category == category && (DateTime.UtcNow - openedAt).TotalMilliseconds > 450) CloseMenu();
            else Open(tab);
        };
        element.LostMouseCapture += (_, _) => { if (reordering && pressedTab == tab) { pressedTab = null; reordering = false; Rebuild(); } };
        element.MouseRightButtonUp += (_, e) => { e.Handled = true; OpenTabMenu(tab); };

        element.AllowDrop = true;
        element.DragEnter += (_, e) =>
        {
            e.Effects = ShortcutDragEffect(e); e.Handled = true;
            if (e.Effects == DragDropEffects.None) return;
            tab.DropTarget = true; Paint(tab);
            dropTimer.Stop(); dropTimer.Start();
        };
        element.DragOver += (_, e) => { e.Effects = ShortcutDragEffect(e); e.Handled = true; };
        element.DragLeave += (_, _) => { tab.DropTarget = false; Paint(tab); dropTimer.Stop(); };
        element.Drop += (_, e) =>
        {
            e.Effects = ShortcutDragEffect(e); e.Handled = true;
            tab.DropTarget = false; dropTimer.Stop(); Paint(tab);
            if (e.Effects != DragDropEffects.None) session.AddPaths(category, MenuWindow.DroppedPaths(e.Data));
        };
        return tab;
    }

    private void StartOpenTimer()
    {
        openTimer.Stop();
        openTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(1, session.Settings.OpenDelayMs));
        openTimer.Start();
    }

    private void Paint(Tab tab)
    {
        var open = menu.IsOpen && (tab.Category is not null ? menu.Category == tab.Category : tab == overflowTab && menu.ShowsOverflow);
        Theming.ThemeVisual.SetIsSelected(tab.Element, open);
        Theming.ThemeVisual.SetIsDropTarget(tab.Element, tab.DropTarget || reordering && pressedTab == tab);
        Theming.ThemeVisual.SetIsRunning(tab.Element, tab.Category is not null && session.Settings.ShowRunningIndicators && session.Status.AnyRunning(tab.Category));
    }

    private void UpdateStates() { foreach (var tab in views) Paint(tab); }

    // ---- Menus -----------------------------------------------------------------------------------

    private PixelRect AnchorFor(Category category)
    {
        var tab = views.FirstOrDefault(v => v.Category == category) ?? overflowTab;
        var bounds = Bounds;
        if (tab is null || !tab.Element.IsVisible) return bounds;
        var rect = ScreenRect(tab.Element);
        return new(rect.Left, bounds.Top, rect.Right, bounds.Bottom);
    }

    private void Open(Tab tab, bool edit = false)
    {
        if (tab.Category is null) return;
        UpdateLayout();
        if (!menu.IsOpen || menu.Category != tab.Category) openedAt = DateTime.UtcNow;
        menu.ShowFor(tab.Category, AnchorFor(tab.Category), monitor, edit);
        closeTimer.Stop();
        UpdateStates();
    }

    private void OpenOverflow()
    {
        if (overflowTab is null) return;
        UpdateLayout();
        var rect = ScreenRect(overflowTab.Element);
        if (!menu.ShowsOverflow) openedAt = DateTime.UtcNow;
        closeTimer.Stop();
        menu.ShowOverflow(hidden, new(rect.Left, Bounds.Top, rect.Right, Bounds.Bottom), monitor);
        UpdateStates();
    }

    /// <summary>Opens a category that does not fit on the bar, anchored to the overflow tab.</summary>
    internal void OpenHidden(Category category)
    {
        menu.ShowFor(category, AnchorFor(category), monitor);
        UpdateStates();
    }

    internal void ShowCategory(Category category, bool edit = false)
    {
        if (views.FirstOrDefault(v => v.Category == category) is { } tab) Open(tab, edit);
        else if (hidden.Contains(category)) { menu.ShowFor(category, AnchorFor(category), monitor, edit); UpdateStates(); }
    }

    internal void CloseMenu()
    {
        openTimer.Stop(); closeTimer.Stop(); pending = null;
        if (menu.IsOpen) menu.HideMenu();
        UpdateStates();
    }

    /// <summary>The category whose tab is under a screen point; used while a shortcut is dragged.</summary>
    internal Category? CategoryAt(Native.Point point)
    {
        var bounds = Bounds;
        if (point.Y < bounds.Top - 4 || point.Y > bounds.Bottom + 40) return null;
        foreach (var tab in views.Where(v => v.Category is not null))
        {
            var rect = ScreenRect(tab.Element);
            if (point.X >= rect.Left && point.X < rect.Right) return tab.Category;
        }
        return null;
    }

    private void Popup(ContextMenu context, UIElement? target = null)
    {
        CloseMenu();
        contextOpen = true;
        context.Closed += (_, _) => contextOpen = false;
        OpenMenu(context, target);
    }

    private void OpenTabMenu(Tab tab)
    {
        var category = tab.Category!;
        var list = session.Config.Categories;
        var index = list.IndexOf(category);
        var context = new ContextMenu();
        context.Items.Add(Ui.Item(L.T("Перейменувати"), () => BeginRename(tab), Icons.Pencil));
        context.Items.Add(Ui.Item(L.T("Редагувати ярлики"), () => Open(tab, edit: true), Icons.Sliders));
        context.Items.Add(Ui.Item(L.T("Додати застосунок…"), () => AppPickerWindow.Open(session, category), Icons.Plus));
        context.Items.Add(Ui.Item(L.T("Створити набір…"), () => SetWindow.Open(session, category), Icons.Apps));
        context.Items.Add(new Separator());
        context.Items.Add(Ui.Item(L.T("Перемістити ліворуч"), () => session.Change(() => ConfigurationEditor.Move(list, category, index - 1)), Icons.ChevronLeft, enabled: index > 0));
        context.Items.Add(Ui.Item(L.T("Перемістити праворуч"), () => session.Change(() => ConfigurationEditor.Move(list, category, index + 1)), Icons.ChevronRight, enabled: index < list.Count - 1));
        context.Items.Add(Ui.Item(L.T("Нова категорія"), CreateCategory, Icons.Plus));
        context.Items.Add(new Separator());
        context.Items.Add(Ui.Item(L.T("Упорядкувати категорії…"), () => App.Instance.ShowSettings("Categories"), Icons.Apps));
        context.Items.Add(Ui.Item(L.T("Налаштування…"), () => App.Instance.ShowSettings(), Icons.Sliders));
        context.Items.Add(new Separator());
        context.Items.Add(Ui.Item(L.T("Видалити категорію…"), () => Dialogs.DeleteCategory(session, category), Icons.Trash, danger: true));
        Popup(context, tab.Element);
    }

    private void OpenBarMenu()
    {
        var context = new ContextMenu();
        context.Items.Add(Ui.Item(L.T("Нова категорія"), CreateCategory, Icons.Plus));
        context.Items.Add(Ui.Item(L.T("Упорядкувати категорії…"), () => App.Instance.ShowSettings("Categories"), Icons.Apps));
        context.Items.Add(Ui.Item(L.T("Налаштування…"), () => App.Instance.ShowSettings(), Icons.Sliders));
        context.Items.Add(new Separator());
        context.Items.Add(Ui.Item(L.T("Сховати панель до перезапуску"), App.Instance.HideBars, Icons.ChevronDown));
        context.Items.Add(Ui.Item(L.T("Вийти"), App.Instance.Quit, Icons.Close));
        Popup(context);
    }

    // ---- Creating, renaming, reordering ----------------------------------------------------------

    private void CreateCategory()
    {
        CloseMenu();
        if (session.AddCategory() is not { } category) return;
        UpdateLayout();
        if (views.FirstOrDefault(v => v.Category == category) is { } tab) BeginRename(tab);
        else session.Toast(L.T("Категорію створено"), L.F("«{0}» — у списку «Ще». Перейменувати можна в налаштуваннях.", category.Name), ToastKind.Info);
    }

    private void BeginRename(Tab tab)
    {
        if (tab.Category is not { } category || renaming) return;
        CloseMenu();
        renaming = true;
        var box = new TextBox { Text = category.Name, MaxLength = 100, MinWidth = 90, MinHeight = 22, Height = 22, Padding = new Thickness(5, 0, 5, 0), Margin = new Thickness(-6, 0, -6, 0), ToolTip = L.T("Enter — зберегти · Esc — скасувати") };
        box.SetResourceReference(Control.BorderBrushProperty, "Accent");
        var finished = false;
        void Finish(bool save)
        {
            if (finished) return;
            finished = true; renaming = false;
            ReleaseFocus();
            var name = box.Text.Trim();
            if (save && name.Length > 0 && name != category.Name) session.Change(() => category.Name = name);
            else Rebuild();
        }
        box.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { e.Handled = true; Finish(true); }
            else if (e.Key == Key.Escape) { e.Handled = true; Finish(false); }
        };
        box.TextChanged += (_, _) => Arrange();
        box.LostKeyboardFocus += (_, _) => Finish(true);
        tab.NameHost.Children.Clear();
        tab.NameHost.Children.Add(box);
        Arrange();
        FocusInput(box);
    }

    private void DragTab(Tab tab)
    {
        if (pressedTab != tab || tab.Category is null) return;
        var cursor = Native.Cursor();
        if (!reordering)
        {
            if (Math.Abs(cursor.X - pressPoint.X) < 8 * Dpi || session.Config.Categories.Count < 2) return;
            reordering = true;
            CloseMenu();
            Mouse.OverrideCursor = Cursors.SizeWE;
            Paint(tab);
        }
        // Slide the tab among the other category tabs; nothing is saved until the button is released.
        var categoryTabs = views.Where(v => v.Category is not null).ToList();
        var current = tabs.Children.IndexOf(tab.Element);
        var target = current;
        for (var i = 0; i < categoryTabs.Count; i++)
        {
            var rect = ScreenRect(categoryTabs[i].Element);
            if (cursor.X >= rect.Left && cursor.X < rect.Right) { target = tabs.Children.IndexOf(categoryTabs[i].Element); break; }
        }
        if (target == current || target < 0) return;
        tabs.Children.RemoveAt(current);
        tabs.Children.Insert(target, tab.Element);
        UpdateLayout();
    }

    private void EndReorder(Tab tab)
    {
        reordering = false;
        Mouse.OverrideCursor = null;
        var index = tabs.Children.IndexOf(tab.Element);
        var list = session.Config.Categories;
        if (tab.Category is { } category && index >= 0 && index != list.IndexOf(category))
        {
            if (session.Change(() => ConfigurationEditor.Move(list, category, index))) return;
        }
        Rebuild();
    }

    // ---- Notifications ---------------------------------------------------------------------------

    /// <summary>Shows a toast above the bar. Returns false when the bar is hidden and Windows should notify instead.</summary>
    internal bool ShowToast(ToastRequest request)
    {
        if (!IsVisible) return false;
        toast ??= new ToastWindow();
        toast.Show(request, monitor, Bounds.Top);
        return true;
    }
}
