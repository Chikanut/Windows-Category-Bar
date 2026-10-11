# Category Bar 0.5.1

- Fixed pointer hit testing for the miniature arrow, theme tiles, and themed controls. The arrow reveals categories on hover, click, and supported shortcut drags; theme previews and tile padding are clickable.
- Added nine surface, text, and border colors with a Windows color picker and HEX input. Overrides are independent for each theme and light/dark mode, persist between launches, and are included in exported themes. Previews update immediately.
- Fixed custom accent selection when the theme accent happened to match the saved custom color.
- Refreshed Glass with lighter translucent layers, subtle highlights and flat buttons. Removed the raised glossy treatment from small controls and improved mode selectors. Existing Glass users can reselect the theme to use its new opacity default, or adjust opacity themselves.
- Fixed theme previews inheriting the current theme's material.

Glass uses the existing native Windows backdrop where supported. Its material shader affects its own highlight; it does not refract the desktop image like Apple's Liquid Glass.

Update an installed app through **Settings → General → Check for updates → Update and restart**. Categories, shortcuts and settings are preserved.

---

- Виправлено натискання стрілки мініатюрного режиму, прев’ю та всієї площі карток тем. Панель розкривається при наведенні, натисканні та перетягуванні ярлика.
- Додано дев’ять кольорів поверхонь, тексту та контурів зі звичайним діалогом вибору кольору і HEX-полем. Кольори зберігаються окремо для кожної теми та світлого/темного режиму, потрапляють в експорт. Прев’ю оновлюються одразу.
- Виправлено вибір власного акценту, коли його значення збігалося зі збереженим кольором.
- Оновлено Glass: прозоріші поверхні, стримані відблиски, пласкі кнопки. Прибрано важкий глянець малих елементів і покращено перемикачі режимів. Щоб застосувати нову стандартну прозорість, повторно виберіть Glass або змініть її вручну.
- Прев’ю інших тем більше не успадковують матеріал поточної теми.

Glass використовує системне розмиття Windows, де воно підтримується. Шейдер змінює відблиск власного матеріалу; справжнє заломлення зображення робочого столу як в Apple Liquid Glass у цьому релізі не реалізовано.

Для оновлення: **Налаштування → Загальне → Перевірити оновлення → Оновити та перезапустити**. Налаштування, категорії та ярлики зберігаються.
