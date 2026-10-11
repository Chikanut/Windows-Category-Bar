# Файлові теми Category Bar

Теми створюються у файлах, без вбудованого редактора. У **Налаштування → Вигляд** залишаються вибір, імпорт, експорт і видалення. Усі можливості, якими користуються вбудовані стилі та приклади, доступні авторам тем.

Є два рівні оформлення:

- **CSS** задає геометрію, кольори, градієнти, прозорі шари, контури, тіні та стани стандартних компонентів.
- **WPF XAML ResourceDictionary** замінює самі шаблони компонентів: довільні векторні форми, композицію шарів, маски, трансформації, анімації, переходи, easing і pixel shaders.

Це нативний WPF-рендерер. CSS — документований діалект для цього застосунку, а не браузерний CSS. XAML дозволяє вийти за межі базового CSS та створити власну структуру візуальних компонентів. Перенесення довільної вебтеми потребує адаптації.

## Початок роботи

Скопіюйте папку [Aurora Motion](../Themes/Examples/aurora-motion). Вона містить:

```text
theme.categorybar-theme.json    # метадані, дві палітри та посилання на файли
styles.css                     # базове оформлення
styles.xaml                    # власні шаблони та Storyboard-переходи
aurora.hlsl                    # вихідний текст GPU-ефекту
aurora.ps                      # готовий скомпільований pixel shader
```

Змініть `Id` і `Name`, відредагуйте CSS/XAML у своєму текстовому редакторі й імпортуйте `theme.categorybar-theme.json`. Імпорт читає файли поруч із маніфестом і зберігає самодостатню копію теми. Після редагування джерел імпортуйте тему знову; однаковий ID створює нову копію. Живого спостереження за файлами немає.

Експорт створює один переносний JSON: `StyleSheet` містить CSS, `StyleResources` — XAML, `Shaders` — bytecode у Base64. Його достатньо завантажити, імпортувати й використовувати без додаткових залежностей. Для редагування можна винести ці дані назад у файли.

Вбудовані стилі [Fluent](../Themes/Examples/fluent.categorybar-theme.json), [Glass](../Themes/Examples/glass.categorybar-theme.json), [Expressive](../Themes/Examples/expressive.categorybar-theme.json), [Midnight](../Themes/Examples/midnight.categorybar-theme.json) мають незалежні CSS. Нові стилі не обмежені цими чотирма варіантами.

## Маніфест

Основні поля: `Version: 1`, `Id`, `Name`, `Author`, `Description`, `Material`, `FontName`, `CornerRadius`, `SurfaceOpacity`, `BorderWidth`, `Dark`, `Light`.

Для файлового авторства:

```json
{
  "StyleSheetFile": "styles.css",
  "StyleResourcesFile": "styles.xaml",
  "ShaderFiles": { "aurora": "aurora.ps" }
}
```

Це фрагмент маніфесту: повний приклад також містить метадані та палітри. Не задавайте одночасно `StyleSheet` і `StyleSheetFile`, або `StyleResources` і `StyleResourcesFile`. Підпапки дозволені; абсолютні шляхи й `..` відхиляються. Шляхи відраховуються від JSON-файлу.

Палітри містять 12 кольорів `#RRGGBB`: `Bar.Bg`, `Menu.Bg`, `Popup.Bg`, `Window.Bg`, `Sidebar.Bg`, `Card.Bg`, `Control.Bg`, `Text`, `Text.Muted`, `Text.Faint`, `Surface.Border`, `Accent`. Режим «Як у Windows» перемикає світлу й темну палітри. Шрифт задається назвою встановленого сімейства. [JSON Schema](../Themes/theme.schema.json) описує поля.

`CornerRadius` є базою для глобального налаштування радіусів; конкретні радіуси задає CSS. `BorderWidth` — метадані сумісності; реальні рамки задає CSS/XAML. `Material: Glass` додає прозорість панелі й меню. Окреме поле `Backdrop: None|Acrylic|Mica` вмикає системний матеріал звичайних вікон на [Windows 11 build 22621+](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwm_systembackdrop_type), з непрозорим fallback на старіших системах. Glass використовує `Acrylic` і власний шейдер матеріалу. Це не точне відтворення Apple Liquid Glass: WPF-шейдер обробляє передану поверхню, а не зображення всього робочого столу. Для рефракції зовнішнього фону потрібне окреме джерело зображення; поточний renderer не захоплює екран. Прозорі overlay-вікна панелі/меню не використовують DWM Acrylic.

## CSS

Починайте з `.theme { skin: studio; }`. Це універсальний рендерер без зашитих декоративних деталей. Старі `fluent`, `glass`, `expressive`, `midnight` збережені для сумісності.

Селектори: `.window`, `.titlebar`, `.sidebar`, `.page`, `.toast`, `.bar`, `.tab`, `.menu`, `.menu-item`, `.button`, `.button-primary`, `.input`, `.card`, `.sidebar-item`. Основні властивості:

| Властивість | Значення |
| --- | --- |
| `background`, `overlay` | `#RRGGBB`, `transparent`, `rgba(r,g,b,a)`, `var(--token)`, `linear-gradient(angledeg, color, color[, color])` |
| `color`, `border-color`, `rim-color` | Колір або змінна палітри |
| `box-shadow` | `none` або `xpx ypx blurpx color`; зміщення ±24, розмиття 0–64 |
| `border-radius`, `border-width`, `rim-width` | 0–32, 0–3, 0–3 px |
| `padding`, `margin`, `gap` | CSS-порядок top/right/bottom/left; 1, 2 чи 4 значення; padding 0–32, margin 0–12, gap 0–20 |
| `height` | `.titlebar`, `.bar`, `.tab`, `.menu-item`, `.sidebar-item`: 20–64 px |
| `min-height` | `.button`, `.input`: 24–56 px |
| `width` | `.menu`: 220–420 px |
| `font-size`, `font-weight` | Основні кнопки/вкладки: 10–18; `normal`, `semibold`, `bold` |
| `icon-size` | `.menu-item`: 16–40 px |
| `opacity` | 0–1 |

Для вкладок: `indicator: underline|dot|none|rail`, `selected-background: accent-soft|selected|control`. Не задані властивості успадковуються від базового skin. Геометрія основної кнопки успадковується від `.button`; `.button-primary` може перевизначити радіус, рамку, padding, типографіку, фарби й ефекти.

Змінні: `--bar-bg`, `--menu-bg`, `--popup-bg`, `--window-bg`, `--sidebar-bg`, `--card-bg`, `--control-bg`, `--text`, `--text-muted`, `--text-faint`, `--border`, `--accent`, `--accent-soft`, `--accent-text`, `--hover`, `--pressed`, `--selected`.

```css
.theme { skin: studio; }
.card {
  background: var(--card-bg);
  overlay: linear-gradient(135deg, rgba(100,180,255,0.08), transparent);
  border-radius: 22px;
  border-width: 0px;
  padding: 24px;
  box-shadow: 0px 6px 24px rgba(0,0,0,0.14);
}
.button:hover { background: rgba(100,180,255,0.12); }
.button:focus { border-color: var(--accent); }
```

Стандартні шаблони читають стани кнопок hover/pressed/focus/disabled, полів hover/focus/disabled, вкладок hover/selected і рядків меню hover/selected. Для власної поведінки станів, кольорів тексту вкладених елементів, анімацій і складних ефектів задавайте XAML-шаблон. CSS не виконує скриптів.

## Власні шаблони й композиція

`styles.xaml` має корінь `ResourceDictionary` зі стандартними WPF namespace. Підтримуються візуальні типи: Grid/Border/Canvas та інші панелі, ContentPresenter, текст, контролі, Path/геометрія, DrawingBrush/VisualBrush, лінійні й радіальні градієнти, OpacityMask, трансформації, BlurEffect і DropShadowEffect. Можна задавати власні `Style`, `ControlTemplate` та `DataTemplate`.

Ключі для заміни компонентів:

| Ключ | TargetType |
| --- | --- |
| `Theme.ButtonTemplate`, `Theme.PrimaryButtonTemplate` | Button |
| `Theme.CardTemplate` | ContentControl |
| `Theme.InputTemplate` | TextBox |
| `Theme.BarTemplate`, `Theme.TabTemplate`, `Theme.MenuTemplate`, `Theme.MenuItemTemplate` | ContentControl |
| `Navigation.Template`, `Button.IconTemplate` | Button |

Для стилів доступні `Theme.Button`, `Button.Primary`, `Button.Secondary`, `Button.Danger`, `Button.Icon`, `Theme.Card`, `Theme.Bar`, `Theme.Tab`, `Theme.Menu`, `Theme.MenuItem`, `Navigation.Item`, а також існуючі `Toggle`, `Segment` та стилі ComboBox/Slider. Авторські ресурси застосовуються поверх базових і прибираються при перемиканні теми.

Зберігайте `ContentPresenter`, щоб показувати контент компонента. У TextBox обов’язковий `ScrollViewer x:Name="PART_ContentHost"`. Підтримуйте клавіатурний фокус і disabled-стан. Шаблони змінюють графічне подання; системні дії, відкриття застосунків та структура даних залишаються в коді програми.

## Анімації та переходи

Підтримуються Storyboard, Trigger/MultiTrigger/DataTrigger, EventTrigger, EnterActions/ExitActions, Double/Color/Point/ThicknessAnimation, keyframes, VisualStateManager, easing та повторення. Анімуйте opacity, позицію, масштаб, поворот, кольори, маски або параметри ефекту.

Ключі життєвого циклу: `Theme.Window.Enter`, `Theme.Page.Enter`, `Theme.Tab.Enter`, `Theme.Menu.Enter`, `Theme.Menu.Exit`, `Theme.Menu.Switch`, `Theme.Bar.Enter`, `Theme.Bar.Expand`, `Theme.Bar.Collapse`, `Theme.Toast.Enter`, `Theme.Content.Enter`.

`ThemeMotion` запускає переходи на host-елементі; анімації без TargetName адресують цей host. Його RenderTransform належить рушію переходів і є `TranslateTransform` (X/Y). Для масштабу, повороту й незалежної анімації матеріалу використовуйте трансформації всередині шаблону. Порожній Storyboard вимикає відповідний перехід. Якщо ключа немає — працює короткий стандартний перехід.

Повторне відкриття скасовує попередній clock і callback закриття; вивантаження прибирає clocks, перемикання теми завершує активні переходи. Меню перестає приймати кліки під час виходу. Lifecycle-переходи мають бути скінченними: імпорт відхиляє Forever й довгі тривалості, runtime завершує перехід не пізніше 3 секунд. Власні template-анімації можуть повторюватися.

Системне вимкнення анімацій пропускає lifecycle-переходи, включно з мініатюрною панеллю. High Contrast використовує базову композицію та системні кольори замість авторських шейдерів/декору. Авторські template Storyboard повинні самі враховувати доступність і не ховати критичні дії.

WPF-приклади: [анімації у шаблонах](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/how-to-animate-in-a-controltemplate), [Storyboard](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/storyboards-overview).

## Pixel shaders

Використовуйте `xmlns:fx="clr-namespace:CategoryBar.Theming;assembly=CategoryBar"` і ефект:

```xml
<Border.Effect>
  <fx:ThemeShaderEffect Shader="aurora" P0="0" P1="0.6"/>
</Border.Effect>
```

`Shader` — ключ із `ShaderFiles`/`Shaders`. Вхід елемента — sampler `s0`; додатковий brush `Auxiliary` — `s1`. Параметри `P0`…`P7` — scalar float-константи `c0`…`c7`, які можна анімувати через Storyboard. Ефект працює з премноженим RGBA: зберігайте альфу та коректне премноження.

Компіляція джерела HLSL у Windows:

```powershell
./scripts/compile-theme-shader.ps1 `
  -Source Themes/Examples/aurora-motion/aurora.hlsl `
  -Output Themes/Examples/aurora-motion/aurora.ps
```

Компілятор використовує системну `d3dcompiler_47.dll`, entry point `main`, профіль `ps_2_0`; доступний `-Profile ps_3_0`. Програма завантажує готовий bytecode, а не компілює HLSL при запуску. Профіль ps_3_0 залежить від можливостей GPU; ps_2_0 є практичнішим базовим варіантом. Це можливості [WPF ShaderEffect](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.effects.shadereffect).

## Зберігання та обмеження

Встановлені теми лежать у `Themes` поруч із `config.json`; `--config` задає окрему бібліотеку. Видалення користувацької теми повертає Fluent. Пошкоджені файли пропускаються з повідомленням.

Маніфест/переносний JSON: максимум 1 МБ; XAML: 256 КБ; CSS: 12000 символів; до 8 шейдерів по 64 КБ. Дозволено лише візуальний XAML з перевіреними типами. CLR-код, довільні assembly, ObjectDataProvider, обробники подій, URI-ресурси та зовнішні словники не завантажуються. Для графіки використовуйте векторні форми й brush-композиції; завантаження зовнішніх bitmap-файлів цим форматом наразі не підтримується.

Автор відповідає за контраст, читабельність, DPI, довгі назви, фокус і продуктивність власних ефектів. Наявність WPF-шаблонів чи шейдерів сама по собі не робить дизайн сучасним: це інструменти для створення різних візуальних систем.
## Архітектурний контракт

`CategoryBar.Core` містить переносну модель і валідацію пакета; WPF-контроли не входять у конфігурацію. `ThemeResourceLoader` завантажує візуальні ресурси, `ThemeMotion` керує переходами, `ThemeSurface` передає шаблону готові функціональні слоти. Обробники запуску програм, збереження, перейменування й drag/drop залишаються в застосунку.

Нові шаблони дозволяють змінювати композицію, а не лише палітру:

| Ключ | TargetType | Слоти |
| --- | --- | --- |
| `Theme.MaterialTemplate` | ContentControl | Background / BorderBrush / BorderThickness; Tag містить CornerRadius |
| `Theme.WindowTemplate` | fx:ThemeSurface | Header — заголовок з керуванням вікном; Content — тіло |
| `Theme.CaptionTemplate` | fx:ThemeSurface | Header — назва; Content — кнопки вікна |
| `Theme.WorkspaceTemplate` | fx:ThemeSurface | Header — навігація; Content — поточна сторінка |
| `Theme.SettingTemplate`, `Theme.SettingStackedTemplate` | fx:ThemeSurface | Header — назва/пояснення; Content — інтерактивне налаштування |
| `Theme.SidebarTemplate`, `Theme.TitleBarTemplate`, `Theme.PageTemplate`, `Theme.ToastTemplate` | ContentControl | Content |

Namespace `fx` — `clr-namespace:CategoryBar.Theming;assembly=CategoryBar`. Заголовок вікна перетягується за своїм фактичним положенням, тому шаблон може переміщати його. Зберігайте обидва ContentPresenter у слотних шаблонах, щоб не втратити навігацію, налаштування або кнопки закриття.

`Theme.MaterialTemplate` малює фон окремо від тексту. На нього можна поставити один `ThemeShaderEffect`, який буде використано стандартними studio-шаблонами вікон, панелі, вкладок, меню, карток, кнопок і сповіщень. Інші контроли (наприклад ComboBox/CheckBox) мають власні WPF templates, які також можна замінити. Старі skin-шаблони збережено для сумісності; для нового авторства використовуйте `skin: studio`.

Стилі контейнерів: `Theme.Window`, `Theme.Caption`, `Theme.Workspace`, `Theme.Setting`, `Theme.Sidebar`, `Theme.TitleBar`, `Theme.Page`, `Theme.Toast`, `Theme.BoardColumn`. `Theme.NavigationPanel` керує орієнтацією та відступами навігації. `Theme.Text.Caption|Small|Body|Title|Heading|Display` задають типографіку текстів, створених через Ui.Text; `Typography.Emphasis` — виділення. Спеціальні поля/вкладки мають власні CSS-ресурси.

Стан не потребує прив’язки до приватних класів застосунку. У шаблонах вкладок і рядків меню доступні attached properties:

```xml
<Trigger Property="fx:ThemeVisual.IsSelected" Value="True">
  <Setter TargetName="selection" Property="Opacity" Value="1"/>
</Trigger>
```

`IsSelected` означає відкрите меню або клавіатурний вибір; `IsRunning` — активні застосунки категорії; `IsDropTarget` — drag/drop. `IsMouseOver`, `IsPressed`, `IsKeyboardFocused`, `IsEnabled` — стандартні WPF-стани. Індикатор вкладки повністю належить `Theme.TabTemplate`; код більше не малює лінію чи крапку.

## Три альтернативні дизайни

- **Glass**: окрема плаваюча навігація, спільний HLSL-матеріал, світлові краї, Acrylic у звичайних вікнах і плавна поява поверхонь.
- **Expressive**: горизонтальна навігація над сторінкою, великі м’які поверхні та пружний просторовий перехід сторінок.
- **Midnight**: вузька бічна навігація, прямокутна рамка вікна, щільні контролі та короткі переходи.

База — Fluent. Вбудовані XAML/CSS знаходяться в `src/CategoryBar.Core/ThemeStyles`; JSON-приклади містять ті самі ресурси, перевірені тестом. Жоден із цих дизайнів не потребує спеціального розгалуження логіки сторінок.

Для початку зі скла скопіюйте [Liquid Workspace](../Themes/Examples/liquid-workspace): окремі CSS, XAML, HLSL, готовий shader і маніфест. Імпортуйте його JSON у **Налаштування → Вигляд → Імпортувати тему**. Змінюйте загальний material template або окремі компоненти; файли автора після імпорту не потрібні.

## Перевірка 11 жовтня 2026

`scripts/build.ps1` запускає core-тести та WPF render/behavior-тести. Останні перевіряють світлі/темні варіанти, геометрію галереї при масштабі 100/125/150/200%, переносність вбудованих прикладів, шейдери й анімовані параметри, semantic-стани вкладок, гаряче перемикання ресурсів, відхилення несумісних шаблонів та нескінченних lifecycle-анімацій, скасування старого exit при повторному вході й прибирання clocks після unload.

PNG зберігаються в `.local/theme-renders`. Це WPF-рендери, а не перевірка всього робочого столу: DWM Acrylic, фізичний multi-monitor DPI, Narrator і системний High Contrast потребують окремої ручної перевірки на цільовій машині.

## Параметри, які користувач налаштовує для теми

Автор додає масив `Parameters` у маніфест. У **Налаштування → Вигляд → Параметри теми** автоматично з’являються поля поточної теми. Для числа значення застосовується після відпускання повзунка або клавіші; перемикачі й списки — одразу; колір — після Enter або втрати фокуса. Кнопка скидання стосується тільки поточної теми.

```json
"Parameters": [
  { "Id": "refraction", "Label": "Заломлення скла", "Type": "Number",
    "Default": "0.025", "Minimum": 0, "Maximum": 0.08, "Step": 0.005 },
  { "Id": "show-rim", "Label": "Світловий контур", "Type": "Toggle", "Default": "true" },
  { "Id": "rim-color", "Label": "Колір контуру", "Type": "Color", "Default": "#BBDFFF" },
  { "Id": "navigation", "Label": "Навігація", "Type": "Choice", "Default": "top",
    "Options": { "top": "Згори", "left": "Ліворуч" } }
]
```

`Default` завжди рядок; числа записуються з крапкою, без залежності від мови Windows. `Label` обов’язковий, `Description` — необов’язкове пояснення під назвою. До 24 параметрів на тему, до 16 варіантів у Choice. ID унікальні, формату `lowercase-with-hyphens`. Number має скінченні Minimum < Maximum у межах ±10000 і додатний Step. Неприпустимі defaults або дублікати відхиляються при імпорті.

Рушій створює типізовані WPF-ресурси, доступні через `DynamicResource`:

| Type | Ключ і тип ресурсу |
| --- | --- |
| Number | `Theme.Parameter.<id>` → double |
| Toggle | `Theme.Parameter.<id>` → bool; `Theme.Parameter.<id>.Visibility` → Visible/Collapsed |
| Color | `Theme.Parameter.<id>` → SolidColorBrush; `Theme.Parameter.<id>.Color` → Color |
| Choice | `Theme.Parameter.<id>` → string із ключів Options |

```xml
<fx:ThemeShaderEffect Shader="liquid"
    P0="{DynamicResource Theme.Parameter.refraction}"
    P1="{DynamicResource Theme.Parameter.rim-light}"/>
<Border BorderBrush="{DynamicResource Theme.Parameter.rim-color}"
    Visibility="{DynamicResource Theme.Parameter.show-rim.Visibility}"/>
```

Number можна прив’язати до Width/Height/Opacity або констант шейдера; Color — до brush чи GradientStop.Color через відповідний ключ. Choice можна покласти в Tag і змінювати Template через Trigger, як у Expressive. Тип ресурсу має відповідати WPF-властивості: double не є Thickness або GridLength. Сама декларація параметра не змінює дизайн, доки шаблон його не використовує. Ресурси `Theme.Parameter.*` зарезервовані: їх не можна перевизначати вручну в XAML.

Значення живуть у `Settings.ThemeParameters[themeId][parameterId]`, незалежно для кожної теми. Перемикання не скидає попередній тюнінг; невідомі/застарілі значення не застосовуються, некоректні значення повертаються до default. Експорт записує поточні налаштування як defaults у переносну копію, зберігаючи доступні межі та варіанти для наступного користувача. Метадані й початкові значення встановленої теми не змінюються.

Перевірки параметрів додатково підтверджують запис через реальний Slider форми налаштувань, повторне читання конфігурації, передачу Number у GPU-константу, типи Toggle/Color/Choice, незалежність значень між темами, reset та експорт. Підсумкова Release-збірка пройшла без попереджень; 89 core-перевірок і WPF-набір пройшли успішно. Рендер форми параметрів: `.local/theme-renders/glass-parameters.png`.
