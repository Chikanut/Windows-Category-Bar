## Category Bar 0.3.0

App sets and eight interface languages.

### What's new

- **App sets.** One icon that starts several apps together. Build a set from the windows that are open now and it remembers the size and position of each one, or pick apps from the installed list to start them the usual way. A click starts only what is not open yet and moves open windows back into place; Shift + click starts everything again.
- **Eight languages.** English, Ukrainian, German, Spanish, French, Portuguese, Simplified Chinese and Japanese. A new installation follows the Windows display language; change it in Settings → General.
- **First-run wizard, themes, drag and drop, app status and miniature mode** from the rewritten interface.

### Fixed

- A shortcut dragged from the desktop no longer breaks when you delete the desktop icon: the app keeps its own copy.

### Good to know

- A minimized window has no position for a set to remember, and an app running as administrator cannot be moved.
- The translations have not been reviewed by native speakers yet. Corrections are welcome: `src/CategoryBar.Core/Languages`.
- Do not go back to an earlier version after creating sets: it cannot read them and restores the settings from a backup.

### Install

Download `CategoryBar-0.3.0-win-x64.zip`, unpack it and run `CategoryBar.exe`. Windows 11 x64; .NET does not need to be installed. The file is not code-signed, so SmartScreen may warn on first launch: "More info" → "Run anyway".

---

## Панель категорій 0.3.0

Набори застосунків і вісім мов інтерфейсу.

### Що нового

- **Набори застосунків.** Одна іконка, яка запускає кілька застосунків разом. Зберіть набір із відкритих зараз вікон, і він запам’ятає розмір та місце кожного, або виберіть програми зі списку встановлених, щоб вони запускалися у звичному вигляді. Клік запускає лише те, що ще не відкрито, а відкриті вікна повертає на місця; Shift + клік запускає все заново.
- **Вісім мов.** Українська, англійська, німецька, іспанська, французька, португальська, спрощена китайська та японська. Нове встановлення бере мову Windows; змінити можна в Налаштуваннях → Загальне.
- **Майстер першого запуску, теми, перетягування, статуси застосунків і мініатюрний режим** із переписаного інтерфейсу.

### Виправлено

- Ярлик, перетягнутий із робочого столу, більше не ламається після видалення значка з робочого столу: програма зберігає власну копію.

### Варто знати

- Згорнуте вікно не має розташування, яке набір міг би запам’ятати, а програму, запущену від імені адміністратора, пересунути не вдасться.
- Переклади ще не вичитували носії мов. Виправлення вітаються: `src/CategoryBar.Core/Languages`.
- Не повертайтеся на попередню версію після створення наборів: вона їх не розуміє і відновить налаштування з резервної копії.

### Встановлення

Завантажте `CategoryBar-0.3.0-win-x64.zip`, розпакуйте та запустіть `CategoryBar.exe`. Windows 11 x64; встановлювати .NET не потрібно. Файл не підписаний, тому під час першого запуску SmartScreen може показати попередження: «Докладніше» → «Виконати в будь-якому разі».
