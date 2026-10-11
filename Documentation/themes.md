# Теми Category Bar

Тема — це звичайна тека з файлами. Вона описує **все**, що видно: кольори, форму й розміри, композицію вікон, вигляд кожного компонента в кожному стані, анімації появи та GPU-шейдери матеріалів. Усі чотири вбудовані дизайни зроблені цим самим способом, без жодного рядка спеціального коду в застосунку, — їх можна відкрити, скопіювати й переробити.

У **Налаштування → Вигляд** користувач вибирає тему, імпортує чужу, експортує свою і крутить **параметри**, які автор теми виніс у налаштування.

## Чотири вбудовані дизайни

| Тема | Ідея | Чим відрізняється по суті |
| --- | --- | --- |
| **Fluent** | Спокійний стиль Windows | Панель лежить на панелі завдань, підкреслення під відкритою категорією, бічна навігація у вікнах |
| **Liquid Glass** | Прозоре скло | Плаваюча капсула показує екран за собою: фон розмивається, а край, як лінза, заломлює його з легкою райдужною каймою; відкрита категорія — другий аркуш скла, що пружно «набухає» |
| **Expressive** | Material Expressive | Тональні поверхні з домішкою акценту; фігури змінюють заокруглення під курсором; навігація — плаваюча панель згори; пружні каскадні анімації |
| **Neo-Brutal** | Необруталізм | Панелі немає — кожна категорія окремий блок із жорсткою тінню; блок піднімається й нахиляється під курсором і «втискається» в тінь, коли відкритий; різкі анімації без згасання |

Джерела лежать у [`src/CategoryBar.Core/ThemeStyles/<id>/`](../src/CategoryBar.Core/ThemeStyles): `theme.json`, `styles.css`, `styles.xaml` і, для скла, `liquid.hlsl` + `liquid.ps`. Це найкращі приклади. Готові до імпорту однофайлові копії — у [`Themes/Examples`](../Themes/Examples).

## Як зробити свою тему

1. Скопіюйте теку вбудованої теми (або [`Themes/Examples/midnight`](../Themes/Examples/midnight) — найпростіший приклад) і перейменуйте маніфест на `щось.categorybar-theme.json`.
2. Змініть `Id` та `Name`, редагуйте CSS/XAML у будь-якому редакторі.
3. **Налаштування → Вигляд → Імпортувати тему…** і виберіть маніфест. Застосунок прочитає файли поруч і збереже самодостатню копію. Після правок імпортуйте ще раз.

**Експортувати тему…** створює один переносний JSON: CSS, XAML і шейдери (Base64) всередині, а поточні значення параметрів, кольори й заокруглення користувача записані як нові значення за замовчуванням. Цей файл достатньо переслати.

Тема складається з трьох шарів, і кожен наступний потрібен лише тоді, коли не вистачає попереднього:

| Шар | Що задає | Коли достатньо |
| --- | --- | --- |
| **Маніфест** (JSON) | Назва, дві палітри, шрифт, параметри для користувача | Перефарбувати наявний дизайн |
| **CSS** | Розміри, відступи, заокруглення, рамки, фарби, тіні, стани, розташування панелі й меню | Новий вигляд зі стандартною композицією |
| **XAML** (+ шейдери) | Шаблони компонентів, композиція вікон, анімації, матеріали | Усе інше: власні форми, шари, рух |

## Маніфест

```json
{
  "Version": 1,
  "Id": "my-theme",
  "Name": "My Theme",
  "Description": "Одне речення для галереї.",
  "FontName": "Segoe UI Variable Text",
  "CornerRadius": 10,
  "SurfaceOpacity": 0.96,
  "Backdrop": "None",
  "Dark":  { "Bar.Bg": "#26262C", "Menu.Bg": "#24242A", "Popup.Bg": "#2C2C32", "Window.Bg": "#1B1B1E", "Sidebar.Bg": "#202024", "Card.Bg": "#26262B", "Control.Bg": "#2E2E34", "Text": "#ECECEF", "Text.Muted": "#A6A6B0", "Text.Faint": "#9797A2", "Surface.Border": "#414148", "Accent": "#8EA8FF" },
  "Light": { "…": "ті самі 12 ключів" },
  "StyleSheetFile": "styles.css",
  "StyleResourcesFile": "styles.xaml",
  "ShaderFiles": { "liquid": "liquid.ps" },
  "Parameters": [ ]
}
```

- Обидві палітри обов’язкові, по 12 кольорів `#RRGGBB`. Режим «Як у Windows» перемикає їх автоматично. Користувач може перевизначити будь-який колір окремо для кожної теми й режиму.
- `SurfaceOpacity` (0.5–1) — початкова прозорість панелі та меню; `CornerRadius` (0–16) — база для загального повзунка заокруглення: він множить усі радіуси CSS. `RadiusScale` — той самий множник, який записує експорт.
- `Backdrop`: `None`, `Acrylic` або `Mica` — системний матеріал звичайних вікон (Windows 11 22H2+). Для панелі, меню та сповіщень є окремий механізм — [живий фон](#живий-фон-заломлення-й-розмиття).
- Файли вказуються відносно маніфесту; `..`, абсолютні шляхи й одночасне `StyleSheet` + `StyleSheetFile` відхиляються. Поля `Material` і `BorderWidth` лишилися для сумісності й нічого не малюють.
- [JSON Schema](../Themes/theme.schema.json) описує всі поля.

## Параметри теми

Параметр — це налаштування, яке автор віддає користувачу. У вікні налаштувань вони з’являються автоматично, згруповані за `Group`.

```json
"Parameters": [
  { "Id": "float", "Group": "Панель", "Label": "Відстань від панелі завдань", "Type": "Number", "Default": "8", "Minimum": 0, "Maximum": 20, "Step": 1 },
  { "Id": "rim-color", "Group": "Скло", "Label": "Колір контуру", "Type": "Color", "Default": "#FFFFFF" },
  { "Id": "show-glow", "Group": "Скло", "Label": "Сяйво", "Type": "Toggle", "Default": "true" },
  { "Id": "navigation", "Group": "Вікна", "Label": "Навігація", "Type": "Choice", "Default": "top", "Options": { "top": "Згори", "left": "Ліворуч" }, "Description": "Пояснення під назвою." }
]
```

До 40 параметрів, до 16 варіантів у `Choice`. `Default` — завжди рядок, числа з крапкою. Значення зберігаються окремо для кожної теми; «Скинути параметри теми» повертає авторські.

Параметр можна використати у двох місцях.

**У CSS** — `var(--p-<id>)` підставляє значення як текст. Число підходить усюди, де очікується число, колір — де колір, ключ `Choice` — де ключове слово:

```css
.bar  { offset: var(--p-float); border-radius: var(--p-roundness); }
.card { box-shadow: var(--p-shadow)px var(--p-shadow)px 0px var(--var(--p-shadow-tone)); }  /* Choice: border | accent-fill */
.tab  { indicator: var(--p-tab-indicator); }                                               /* Choice: underline | dot | rail | none */
```

Імпорт перевіряє таблицю стилів на краях діапазону кожного параметра, тому повзунок не зможе вивести властивість за межі.

**У XAML** — як ресурси через `DynamicResource`:

| Type | Ресурси |
| --- | --- |
| Number | `Theme.Parameter.<id>` (double), `.Thickness`, `.CornerRadius`, `.Length` (GridLength) |
| Toggle | `Theme.Parameter.<id>` (bool), `.Visibility`, `.Opacity` (1 або 0) |
| Color | `Theme.Parameter.<id>` (brush), `.Color` |
| Choice | `Theme.Parameter.<id>` (рядок-ключ) — зручно класти в `Tag` і перемикати шаблон тригером |

Три ID рушій розуміє сам, якщо тема їх оголосить:

| Id | Type | Дія |
| --- | --- | --- |
| `motion` | Toggle | вимикає анімації появи |
| `motion-speed` | Number | множник темпу (0.25–4) |
| `motion-style` | Choice | вибирає варіант анімації: ключ `Theme.Menu.Enter.<варіант>` має перевагу над `Theme.Menu.Enter` |

## CSS

Це власний діалект для нативного рендерера, не браузерний CSS. Правила читаються згори вниз, **пізніше правило перемагає**; усе, чого тема не згадала, береться зі спільних значень за замовчуванням ([`studio.css`](../src/CategoryBar.Core/ThemeStyles/studio.css)).

Селектори: `.window` `.titlebar` `.sidebar` `.sidebar-item` `.page` `.card` `.button` `.button-primary` `.input` `.bar` `.tab` `.menu` `.menu-item` `.toast`.
Стани (для `button`, `button-primary`, `input`, `tab`, `menu-item`, `sidebar-item`): `:hover` `:pressed` `:focus` `:selected` `:disabled`.

| Властивість | Значення |
| --- | --- |
| `background`, `overlay` | колір, `linear-gradient(кут deg, 2–5 кольорів)`, `radial-gradient(2–4 кольори)` |
| `color`, `border-color`, `rim-color`, `icon-color` (лише `.sidebar-item`) | колір |
| `box-shadow` | `none` або `x y blur колір`; зсув ±24, розмиття 0–64 (0 — жорстка тінь) |
| `border-radius` · `border-width`, `rim-width` | 0–40 · 0–4 |
| `padding` · `margin` | 1, 2 або 4 значення (top right bottom left); до 40 · до 24 |
| `gap` | `.bar`, `.menu`, `.card`: 0–24 |
| `height` | `.titlebar`, `.bar`, `.tab`, `.menu-item`, `.sidebar-item`: 20–72 |
| `min-height` | `.button`, `.button-primary`, `.input`: 24–56 |
| `width` | `.menu`: 220–420; `.sidebar`: 160–320 |
| `font-size`, `font-weight` | 10–20; `normal` `medium` `semibold` `bold` |
| `icon-size` | `.menu-item`: 16–40 |
| `opacity` | 0–1 |
| `indicator` | `.tab`: `underline` `dot` `rail` `none` |
| **`offset`** | `.bar`: відстань до панелі завдань; `.menu`: відстань до панелі; 0–24 |
| **`backdrop`**, **`backdrop-blur`** | `.bar`, `.menu`, `.toast`: `live` \| `none` і радіус розмиття 0–40 — див. [живий фон](#живий-фон-заломлення-й-розмиття) |
| **`stagger`** | `.tab`, `.menu-item`, `.card`: пауза між сусідами в каскадній появі, 0–120 мс |

Стани приймають `background`, `color`, `border-color`, `box-shadow`, `opacity`, `icon-color`.

**Кольори:** `#RRGGBB`, `transparent`, `rgba(r,g,b,a)`, змінна, а також функції, які можна вкладати:

```css
alpha(var(--accent), 0.3)                    /* прозорість */
mix(var(--card-bg), var(--accent-fill), 0.1) /* суміш двох кольорів */
```

Змінні палітри й стану: `--bar-bg` `--menu-bg` `--popup-bg` `--window-bg` `--sidebar-bg` `--card-bg` `--control-bg` `--text` `--text-muted` `--text-faint` `--border` `--accent` `--accent-soft` `--accent-text` `--accent-fill` `--accent-fill-text` `--hover` `--pressed` `--selected` `--danger` `--warning` `--success`.

`--accent` — це акцент, підправлений для читабельності як текст; `--accent-fill` — акцент точно як вибраний, для заливок, а `--accent-fill-text` — контрастний до нього колір тексту.

## Правила побудови: панель, меню, вікна

- **Панель** — `.bar { offset }` піднімає її над панеллю завдань (плаваюча панель), `box-shadow` дає тінь, `background: transparent` прибирає спільну підкладку (тоді вкладки стають окремими блоками, як у Neo-Brutal). `gap` і `padding` розставляють вкладки.
- **Меню** — `.menu { width; offset; padding; gap }`, рядки — `.menu-item { height; icon-size }`. Заголовок меню — окремий шаблон `Theme.MenuHeaderTemplate`.
- **Вікна** складаються зі слотів, які шаблон може розставити як завгодно:

| Ключ шаблону | TargetType | Слоти |
| --- | --- | --- |
| `Theme.WindowTemplate` | `fx:ThemeSurface` | `Header` — рядок заголовка, `Content` — тіло вікна |
| `Theme.CaptionTemplate` | `fx:ThemeSurface` | `Header` — назва, `Content` — кнопки вікна |
| `Theme.WorkspaceTemplate` | `fx:ThemeSurface` | `Header` — навігація, `Content` — сторінка |
| `Theme.SettingTemplate`, `Theme.SettingStackedTemplate` | `fx:ThemeSurface` | `Header` — підпис, `Content` — елемент керування |
| `Theme.MenuHeaderTemplate` | `fx:ThemeSurface` | `Header` — назва категорії, `Tag` — вона ж великими літерами, `Content` — інструменти |

Стиль `Theme.NavigationPanel` (StackPanel) визначає орієнтацію навігації. Так Expressive переносить навігацію нагору й дає користувачу вибір через параметр `navigation`. Заголовок вікна перетягується там, де його розмістив шаблон.

## XAML: шаблони компонентів

`styles.xaml` — `ResourceDictionary` зі стандартними просторами імен WPF та `xmlns:fx="clr-namespace:CategoryBar.Theming;assembly=CategoryBar"`. Ресурс із відомим ключем **замінює** спільний; решта ключів — ваші власні (пензлі, допоміжні шаблони).

| Ключ | TargetType | Що малює |
| --- | --- | --- |
| `Theme.MaterialTemplate` | ContentControl | матеріал великих поверхонь (панель, меню, картки, навігація, вікно) |
| `Theme.ControlMaterialTemplate` | ContentControl | матеріал вкладок, рядків, кнопок, полів |
| `Theme.BarTemplate`, `Theme.TabTemplate`, `Theme.MenuTemplate`, `Theme.MenuItemTemplate` | ContentControl | панель і меню |
| `Theme.ButtonTemplate`, `Theme.PrimaryButtonTemplate`, `Button.IconTemplate`, `Navigation.Template` | Button | кнопки й пункти навігації |
| `Theme.InputTemplate` | TextBox | поле вводу (потрібен `ScrollViewer x:Name="PART_ContentHost"`) |
| `Theme.CardTemplate`, `Theme.SidebarTemplate`, `Theme.TitleBarTemplate`, `Theme.PageTemplate`, `Theme.ToastTemplate` | ContentControl | поверхні вікон |

Стилі також замінюються за ключем: `Toggle` і `Segment` (перемикач і сегменти), `Theme.Workspace`, `Theme.NavigationPanel`, `Theme.MenuHeader`, `Theme.Text.Caption|Small|Body|Title|Heading|Display`, а неявні стилі (`<Style TargetType="Slider">`) — за типом.

Матеріал отримує `Background`, `BorderBrush`, `BorderThickness`, а радіус — у `Tag`:

```xml
<ControlTemplate x:Key="Theme.MaterialTemplate" TargetType="ContentControl">
  <Border Background="{TemplateBinding Background}" CornerRadius="{Binding Tag, RelativeSource={RelativeSource TemplatedParent}}"/>
</ControlTemplate>
```

Усе, що порахував CSS, доступне як ресурси `<селектор>.<Властивість>`: `tab.Radius`, `tab.Padding`, `tab.Height`, `tab.Background`, `tab.Foreground`, `tab.BorderBrush`, `tab.Shadow`, `tab:selected.Background`, `tab:hover.Foreground`, `menu-item.IconSize`, `card.Margin` тощо, плюс палітра (`Text`, `Accent`, `Accent.Fill`, `Accent.FillText`, `Surface.Border`, `Hover`, …) і кольори для градієнтів (`Accent.Color`, `Accent.Fill.Color`, `Text.Color`).

**Стани.** Застосунок лише повідомляє стан, малює його шаблон:

```xml
<Trigger Property="fx:ThemeVisual.IsSelected" Value="True"> … </Trigger>   <!-- відкрита категорія / рядок під клавіатурою -->
<Trigger Property="fx:ThemeVisual.IsRunning" Value="True"> … </Trigger>    <!-- у категорії є запущені застосунки -->
<Trigger Property="fx:ThemeVisual.IsDropTarget" Value="True"> … </Trigger> <!-- над елементом тягнуть ярлик -->
<Trigger Property="IsMouseOver" Value="True"> … </Trigger>                 <!-- також IsPressed, IsKeyboardFocused, IsEnabled -->
```

Текст та іконки вкладок, рядків меню й пунктів навігації успадковують `Foreground` компонента — змініть його в тригері, і вміст перефарбується.

**Рух усередині шаблону.** `fx:ThemeTransform` дає анімовані властивості без ручного опису трансформацій: `X`, `Y`, `Scale`, `ScaleX`, `ScaleY`, `Angle`, `Origin`.

```xml
<Grid x:Name="lens" Opacity="0" fx:ThemeTransform.Scale="1"> … </Grid>
…
<Trigger Property="fx:ThemeVisual.IsSelected" Value="True">
  <Setter TargetName="lens" Property="Opacity" Value="1"/>                    <!-- сталий стан -->
  <Trigger.EnterActions><BeginStoryboard><Storyboard FillBehavior="Stop">     <!-- перехід до нього -->
    <DoubleAnimation Storyboard.TargetName="lens" Storyboard.TargetProperty="(fx:ThemeTransform.Scale)" From="0.72" To="1" Duration="0:0:0.4">
      <DoubleAnimation.EasingFunction><ElasticEase EasingMode="EaseOut" Oscillations="1" Springiness="5"/></DoubleAnimation.EasingFunction>
    </DoubleAnimation>
  </Storyboard></BeginStoryboard></Trigger.EnterActions>
</Trigger>
```

Порада: сталий вигляд стану задавайте `Setter`-ом, а анімацію робіть з `FillBehavior="Stop"` — тоді стан правильний і одразу після побудови, і після анімації. У шаблонних Storyboard не можна використовувати `DynamicResource`; значення з параметрів подавайте через `Setter` (як нахил блока в Neo-Brutal).

## Анімації появи

Storyboard з відомим ключем програється на самому елементі в момент події. Анімуйте `Opacity` та `(fx:ThemeTransform.…)`:

```xml
<Storyboard x:Key="Theme.Menu.Enter" FillBehavior="Stop">
  <DoubleAnimation Storyboard.TargetProperty="Opacity" From="0" To="1" Duration="0:0:0.14"/>
  <DoubleAnimation Storyboard.TargetProperty="(fx:ThemeTransform.Scale)" From="0.82" To="1" Duration="0:0:0.42">
    <DoubleAnimation.EasingFunction><ElasticEase EasingMode="EaseOut" Oscillations="1" Springiness="7"/></DoubleAnimation.EasingFunction>
  </DoubleAnimation>
</Storyboard>
```

| Ключ | Коли |
| --- | --- |
| `Theme.Bar.Enter` | панель з’являється разом із панеллю завдань |
| `Theme.Bar.Expand`, `Theme.Bar.Collapse` | мініатюрний режим: панель виїжджає та ховається |
| `Theme.Tab.Enter` | кожна вкладка; каскад через `.tab { stagger }` |
| `Theme.Menu.Enter`, `Theme.Menu.Exit`, `Theme.Menu.Switch` | меню відкривається, закривається, перемикається на іншу категорію |
| `Theme.MenuItem.Enter` | кожен рядок меню; каскад через `.menu-item { stagger }` |
| `Theme.Window.Enter`, `Theme.Page.Enter` | вікно та сторінка налаштувань |
| `Theme.Card.Enter` | картки сторінки; каскад через `.card { stagger }` |
| `Theme.Toast.Enter`, `Theme.Toast.Exit` | сповіщення |
| `Theme.Content.Enter` | інший вміст, що з’являється |

- Меню масштабується від нижнього краю (воно «виростає» з панелі), сповіщення — від правого нижнього кута, вкладки — від низу.
- Для `Exit` використовуйте `FillBehavior="HoldEnd"` і не задавайте `From`.
- Порожній Storyboard вимикає перехід. Без ключа панель, меню, вікна й сповіщення отримують короткий стандартний перехід, а рядки меню й картки з’являються одразу.
- Варіанти для параметра `motion-style`: `Theme.Menu.Enter.calm`, `Theme.Tab.Enter.smooth` тощо.
- Переходи мусять бути скінченними: `Forever` і тривалості понад 3 с відхиляються під час імпорту. Анімації всередині шаблонів можуть повторюватися.
- Якщо у Windows вимкнено анімації, переходи не програються. У режимі високої контрастності використовується базова композиція із системними кольорами.

## Шейдери

```xml
<Border Background="{TemplateBinding Background}" CornerRadius="{Binding Tag, RelativeSource={RelativeSource TemplatedParent}}">
  <Border.Effect>
    <fx:ThemeShaderEffect Shader="liquid" Bleed="3"
        P0="{DynamicResource Theme.Parameter.specular}"
        P2="{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}"
        P3="{Binding ActualHeight, RelativeSource={RelativeSource TemplatedParent}}"/>
  </Border.Effect>
</Border>
```

- `Shader` — ключ із `ShaderFiles`. Елемент потрапляє в sampler `s0`, додатковий пензель `Auxiliary` — у `s1`; `P0`…`P7` — константи `c0`…`c7`, їх можна прив’язувати до параметрів і розмірів та анімувати.
- `Bleed` додає навколо елемента прозорі пікселі, щоб шейдер бачив його власний край. Саме так `liquid.hlsl` знаходить напрямок до найближчого краю за альфа-каналом і підсвічує форму з будь-яким заокругленням.
- Ставте шейдер на шар матеріалу, а не на текст. Колір премножений на альфу — зберігайте це на виході.

### Живий фон: заломлення й розмиття

Панель, меню та сповіщення — прозорі вікна поверх робочого столу. Тема може попросити для них зображення того, що лежить **за** поверхнею:

```css
.bar  { backdrop: live; backdrop-blur: 10px; background: alpha(var(--bar-bg), 0.5); }
.menu { backdrop: var(--p-backdrop); backdrop-blur: var(--p-blur); }   /* вибір і сила — параметри користувача */
```

Рушій кілька разів на секунду (а коли за панеллю щось рухається — плавно) знімає цю ділянку екрана **без власних вікон застосунку**, розмиває її на `backdrop-blur` пікселів і віддає вікну як пензель `Theme.Backdrop`; `Theme.Backdrop.Enabled` дорівнює 1, коли зображення є, і 0, коли його немає. Що з ним робити, вирішує шейдер матеріалу теми:

```xml
<fx:ThemeShaderEffect Shader="liquid" Auxiliary="{DynamicResource Theme.Backdrop}" P5="{DynamicResource Theme.Backdrop.Enabled}"
    P1="{DynamicResource Theme.Parameter.refraction}" P4="{DynamicResource Theme.Parameter.bevel}"
    P7="{Binding Tag.TopLeft, RelativeSource={RelativeSource TemplatedParent}}" … />
```

[`liquid.hlsl`](../src/CategoryBar.Core/ThemeStyles/glass/liquid.hlsl) — готовий приклад: за розміром і радіусом елемента він обчислює відстань до краю, у смузі вздовж краю зсуває вибірку фону всередину (лінза), трохи по-різному для червоного й синього каналів (райдужна кайма), накладає колір скла з його альфою та підсвічує край. Змінюючи шейдер, можна отримати матове скло, воду, збільшувальне скло чи кольоровий фільтр.

- Знімання використовує Magnification API Windows: знімки екрана й запис відео інших програм показують панель як зазвичай.
- Якщо у Windows вимкнено ефекти прозорості, увімкнено високу контрастність або система не віддає зображення, `Theme.Backdrop.Enabled` лишається 0 — шейдер має намалювати звичайне напівпрозоре скло (у `liquid.hlsl` воно при цьому щільніше, щоб текст читався).
- На нерухомому робочому столі живий фон коштує близько відсотка одного ядра процесора; коли за панеллю грає відео — більше.

Компіляція (потрібен лише Windows):

```powershell
./scripts/compile-theme-shader.ps1 -Source my-theme/glow.hlsl -Output my-theme/glow.ps
```

Точка входу `main`, профіль `ps_2_0` (або `-Profile ps_3_0`). Застосунок завантажує лише готовий байт-код.

## Обмеження й безпека

Тему можна завантажити з інтернету, тому вона не може виконувати код.

- XAML: лише візуальні типи (панелі, фігури, пензлі, трансформації, анімації, тригери, стилі, шаблони) та `fx:ThemeShaderEffect`, `fx:ThemeSurface`, `fx:ThemeVisual`, `fx:ThemeTransform`. Заборонено обробники подій, команди, `x:Class`/`x:Code`, конвертери, `ObjectDataProvider`, довільні типи CLR, URI, зовнішні словники й зображення. Розширення розмітки: `StaticResource`, `DynamicResource`, `TemplateBinding`, `Binding`, `RelativeSource`, `x:Type`, `x:Null`.
- Розміри: маніфест або переносний JSON — до 1 МБ, XAML — 256 КБ, CSS — 16 000 символів, до 8 шейдерів по 64 КБ.
- Ресурси `Theme.Parameter.*` створює рушій; оголошувати їх у XAML не можна.
- Шаблон із неправильним `TargetType` або нескінченний перехід зупиняють імпорт із поясненням, а не ламають інтерфейс. Пошкоджений файл у бібліотеці пропускається.
- Автор відповідає за контраст, фокус клавіатури, довгі назви та продуктивність власних ефектів. У шаблонах зі слотами зберігайте обидва `ContentPresenter`, інакше зникне навігація чи кнопки вікна.

Встановлені теми лежать у теці `Themes` поруч із `config.json`. Видалення користувацької теми повертає Fluent.

## Сумісність

Теми, написані для першої версії формату, імпортуються як є: `.theme { skin: … }` ігнорується (рендерер тепер один), а шлях `(UIElement.RenderTransform).(TranslateTransform.Y)` у переходах автоматично стає `(fx:ThemeTransform.Y)`. Приклади такого формату — [`aurora-motion`](../Themes/Examples/aurora-motion) і [`midnight`](../Themes/Examples/midnight). Вбудовану тему Midnight замінив Neo-Brutal; її можна імпортувати з прикладів.

## Для розробників

- `CategoryBar.Core`: `ThemeDefinition`/`ThemeFiles` (формат і перевірка), `ThemeStyleSheet`, `ThemePaint`, `ThemeMarkup`, `ThemeParameter`. Вбудовані теми — вбудовані ресурси з `ThemeStyles/<id>`, які читає той самий код, що й імпорт.
- `CategoryBar`: `Theme.Fill` записує палітру, параметри й ресурси CSS у словник ресурсів (застосунку або ізольованого прев’ю); `ThemeStyleResources` перетворює CSS на ресурси; `ThemeResourceLoader` перевіряє й завантажує XAML та шейдери; `ThemeMotion` програє переходи; `Themes/ThemeSurfaces.xaml` і `ThemeControls.xaml` — спільні шаблони.
- `scripts/build.ps1` запускає перевірки ядра та WPF-перевірки тем. `dotnet run --project tests/CategoryBar.ThemeTests -c Release -- --lab [id…]` малює кожну тему (панель із меню, сповіщення, сторінки налаштувань, діалоги, стани) у `.local/theme-lab` — зручно переглядати зміни дизайну без запуску застосунку. `-- --export-examples` оновлює однофайлові копії вбудованих тем у `Themes/Examples`.
