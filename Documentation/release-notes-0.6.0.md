# Category Bar 0.6.0

Four designs that differ in structure, not just colour, and a theme engine that lets a theme change everything you see.

- **Fluent** — the familiar look, unchanged.
- **Liquid Glass** — a floating capsule of clear glass. The bar, menus and notifications show the screen behind them: the picture is blurred and bent at the rim like a lens, with a trace of colour fringing. The glossy bands of the old Glass are gone.
- **Expressive** — Material Expressive: tonal surfaces, shapes that change their corners under the pointer, navigation as a floating toolbar, springy cascading motion.
- **Neo-Brutal** — every category is a separate block on a hard shadow; blocks tilt under the pointer and drop onto their shadow when open. Replaces Midnight, which remains available as an importable example.

Each design has its own grouped parameters in **Settings → Appearance**: distance from the taskbar, roundness, glass blur, refraction and density, outline and shadow, motion style and speed, and more.

For theme authors:

- Parameters now work in the stylesheet: `var(--p-id)` drives sizes, colours and shadows.
- Layout rules: a floating bar (`offset`), menu distance, navigation width and margins.
- Motion: scale, rotation and movement through `fx:ThemeTransform`; staggered entrance of tabs, menu rows and cards; new transitions; user-selectable motion variants.
- Live backdrop: `backdrop: live` gives a surface the picture of the screen behind it for the theme's own shader.
- Stylesheet cascade, `alpha()` and `mix()`, radial gradients, hard shadows, per-state text colours.
- Built-in themes are ordinary theme folders that can be copied as a starting point. See `Documentation/themes.md`.

Fixed: menus and notifications painted their background twice; previews picked up the active theme's templates; glass outlines appeared on every menu row; title bar text and icons did not follow the theme; the miniature bar with an offset did not slide fully out of sight.

Notes:

- If Midnight was selected, the app switches to Fluent. Import `Themes/Examples/midnight` to keep using it.
- Themes exported from earlier versions still import. Copies of the old Glass look slightly different.
- The live backdrop of Liquid Glass can be switched off in the theme's parameters. It is also off when Windows transparency effects are disabled.

Update an installed app through **Settings → General → Check for updates → Update and restart**. Categories, shortcuts and settings are preserved.

---

Чотири дизайни, що відрізняються будовою, а не лише кольором, і рушій тем, який дозволяє темі змінити все, що видно.

- **Fluent** — знайомий вигляд без змін.
- **Liquid Glass** — плаваюча капсула з прозорого скла. Панель, меню й сповіщення показують екран за собою: зображення розмивається й заломлюється на краях, як у лінзі, з легкою райдужною каймою. Глянцевих смуг старого Glass більше немає.
- **Expressive** — Material Expressive: тональні поверхні, фігури змінюють заокруглення під курсором, навігація — плаваюча панель, пружні каскадні анімації.
- **Neo-Brutal** — кожна категорія є окремим блоком із жорсткою тінню; блок нахиляється під курсором і «втискається» в тінь, коли відкритий. Замінює Midnight, яку можна імпортувати з прикладів.

Кожен дизайн має власні згруповані параметри в **Налаштування → Вигляд**: відстань від панелі завдань, округлість, розмиття, заломлення й густина скла, контур і тінь, характер і швидкість руху тощо.

Для авторів тем:

- Параметри працюють у таблиці стилів: `var(--p-id)` керує розмірами, кольорами й тінями.
- Правила розташування: плаваюча панель (`offset`), відстань меню, ширина й відступи навігації.
- Рух: масштаб, поворот і зсув через `fx:ThemeTransform`; каскадна поява вкладок, рядків меню й карток; нові переходи; варіанти руху на вибір користувача.
- Живий фон: `backdrop: live` дає поверхні зображення екрана за нею для власного шейдера теми.
- Каскад у CSS, `alpha()` і `mix()`, радіальні градієнти, жорсткі тіні, кольори тексту для станів.
- Вбудовані теми — звичайні теки-теми, які можна скопіювати як основу. Див. `Documentation/themes.md`.

Виправлено: меню й сповіщення малювали фон двічі; прев’ю підхоплювали шаблони активної теми; скляні обводки з’являлися на кожному рядку меню; текст та іконки заголовка вікна не перефарбовувалися; мініатюрна панель із відступом не ховалася повністю.

Примітки:

- Якщо була вибрана Midnight, застосунок перейде на Fluent. Щоб користуватися нею далі, імпортуйте `Themes/Examples/midnight`.
- Теми, експортовані з попередніх версій, імпортуються. Копії старого Glass виглядають трохи інакше.
- Живий фон Liquid Glass вимикається в параметрах теми. Він також вимкнений, коли у Windows вимкнено ефекти прозорості.

Для оновлення: **Налаштування → Загальне → Перевірити оновлення → Оновити та перезапустити**. Налаштування, категорії та ярлики зберігаються.
