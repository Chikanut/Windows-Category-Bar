# Category Bar 0.4.0

- Install with `CategoryBar-win-Setup.exe`; .NET is included.
- Check for stable GitHub updates at startup or in Settings → General.
- Download and install an update with **Update and restart**. Updates never restart the app without your action.
- Keep existing categories, shortcuts, language and appearance settings.
- Release packages are built and tested automatically with GitHub Actions.

## Moving from 0.3.0

Exit the old portable app from the tray before installing. Run `CategoryBar-win-Setup.exe` and use the installed copy from the Start menu. Existing settings in `%LOCALAPPDATA%\WindowsCategoryBar` are reused, and enabled autostart is redirected to the installed copy. The old portable EXE can then be removed.

This release deliberately retains the 0.3.0 appearance. The new theme system will arrive in a separate release to test the update flow.

---

- Установлення через `CategoryBar-win-Setup.exe`, .NET уже включено.
- Перевірка оновлень при запуску та в Налаштуваннях → Загальне.
- Встановлення лише після натискання **Оновити та перезапустити**.
- Категорії, ярлики й налаштування зберігаються.

Перед установленням вийдіть зі старої portable-версії через трей. Після встановлення запускайте додаток із меню «Пуск». У цій версії ще немає нової системи стилів — вона прийде окремим оновленням.
