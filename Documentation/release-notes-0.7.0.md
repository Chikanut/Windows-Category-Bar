# Category Bar 0.7.0

Categories can now live on the Windows taskbar itself.

- **Icons on the taskbar.** A new mode in Settings → Bar and menus (also in the tray menu and on the last step of the first-run wizard). Every category becomes an icon on the Windows taskbar and the bar hides. Hover or click an icon to open the category's menu; right-click lists its apps.
- **Pinned for you.** The icons are pinned automatically, placed first on the taskbar and removed again when you switch the mode off. No administrator rights and no Explorer restart. You can drag the icons to reorder them; an icon you unpin yourself comes back only after **Pin all**.
- **Your choice of icon.** Categories → ⋯ → **Taskbar icon…**: an app's icon, one of 50 symbols on a coloured tile, or up to three letters on a tile in the theme's accent colour.
- **Running indicator.** A green dot on the icon while an app of the category is running.
- **Divider.** A thin line after the category icons separates them from your own icons and running apps. Clicking it opens Settings. It can be switched off.

Known limits: the pin list format is not documented by Windows, so if a future Windows update changes it the app leaves the list alone and asks you to pin the shortcuts yourself. After renaming a category its tooltip keeps the old name until the icon is pinned again. After uninstalling the app, unpin the category icons by hand. Details: `Documentation/taskbar-icons.md`.

---

Категорії тепер можуть жити просто на панелі завдань Windows.

- **Значки на панелі завдань.** Новий режим у Налаштування → «Панель і меню» (також у меню трею та на останньому кроці майстра першого запуску). Кожна категорія стає значком на панелі завдань Windows, а панель категорій ховається. Наведення або клацання відкриває меню категорії, права кнопка показує її застосунки.
- **Закріплюються самі.** Значки закріплюються автоматично, стають першими на панелі завдань і зникають, коли режим вимкнено. Без прав адміністратора й без перезапуску Провідника. Порядок значків можна змінювати перетягуванням; значок, відкріплений вручну, повернеться лише після **«Закріпити всі»**.
- **Значок на вибір.** Категорії → ⋯ → **«Значок на панелі завдань…»**: іконка застосунку, один із 50 символів на кольоровій плитці або до трьох літер на плитці акцентного кольору теми.
- **Індикатор запуску.** Зелена крапка на значку, поки застосунок категорії запущено.
- **Роздільник.** Тонка риска після значків категорій відділяє їх від ваших значків і запущених програм. Клацання по ній відкриває налаштування. Її можна вимкнути.

Відомі межі: формат списку закріплених Windows не документує, тож якщо майбутнє оновлення Windows його змінить, програма не чіпатиме список і попросить закріпити ярлики вручну. Після перейменування категорії підказка значка зберігає стару назву до повторного закріплення. Після видалення програми значки категорій треба відкріпити вручну. Подробиці: `Documentation/taskbar-icons.md`.
