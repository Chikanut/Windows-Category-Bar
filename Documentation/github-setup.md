# Оформлення репозиторію на GitHub

Покрокова інструкція: від порожнього GitHub до сторінки з описом, скріншотами та релізом. Команди розраховані на PowerShell у корені проєкту та встановлений `gh` (ви вже ввійшли як `Chikanut`).

У README посилання й значки ведуть на `Chikanut/Windows-Category-Bar`. Якщо назвете репозиторій інакше, замініть цей рядок у `README.md` та `README.uk.md`.

## Готові тексти

### Короткий опис (поле About → Description, до 350 символів)

GitHub показує одне поле, тому обидві мови йдуть в один рядок:

```text
Category tabs above the Windows 11 taskbar: hover a tab to open its apps. · Вкладки категорій над панеллю завдань Windows 11: наведіть курсор — і застосунки відкриються.
```

Якщо хочете коротше, лише англійською (так репозиторій краще шукається):

```text
Category tabs above the Windows 11 taskbar — hover a tab to open its apps. Portable, no install.
```

### Теми (Topics)

```text
windows-11 taskbar launcher app-launcher productivity desktop wpf dotnet csharp win32 ukrainian
```

### Текст для соцмереж або опису в профілі

Англійською:

> Category Bar puts category tabs right above the Windows 11 taskbar. Hover a tab and its apps open in a column; a click starts the app or switches to its window. It imports your pinned apps, follows the Windows theme, and runs as a single portable `.exe`.

Українською:

> Панель категорій додає вкладки над панеллю завдань Windows 11. Наведіть курсор на вкладку — її застосунки відкриються колонкою; клік запускає програму або переходить до її вікна. Програма переносить закріплені застосунки, підхоплює тему Windows і працює як один портативний `.exe`.

### Нотатки до релізу

Двомовний текст лежить у [release-notes-0.3.0.md](release-notes-0.3.0.md).

## Крок 1. Вирішіть питання з ліцензією

Зараз у репозиторії немає файлу `LICENSE`. Без нього код формально «всі права захищені»: люди можуть його читати, але не мають права використовувати чи змінювати. Для відкритого проєкту найпростіший вибір — MIT. Додати можна під час створення репозиторію на сайті або пізніше: **Add file → Create new file → `LICENSE` → Choose a license template**.

## Крок 2. Перевірте, що потрапить у відкритий доступ

Репозиторій стане публічним разом з усією історією. Перегляньте:

- папку `Design/` (PDF і HTML дизайну) та `Documentation/` — чи готові ви їх показувати;
- `Documentation/screenshots/settings-general.png` — на ньому видно шлях `D:\Projects\Windows-Category-Bar\.local\dev\config.json`.

Папки `.local/` та `artifacts/` у `.gitignore`, тож вони не публікуються.

## Крок 3. Створіть репозиторій і надішліть код

```powershell
gh repo create Windows-Category-Bar --public --source . --remote origin --push --description "Category tabs above the Windows 11 taskbar: hover a tab to open its apps. · Вкладки категорій над панеллю завдань Windows 11: наведіть курсор — і застосунки відкриються."
```

Гілку `redesign-0.2` надсилати не потрібно: вона вже повністю в `main`. Видалити її локально:

```powershell
git branch -d redesign-0.2
```

## Крок 4. Заповніть блок About

```powershell
gh repo edit --add-topic windows-11,taskbar,launcher,app-launcher,productivity,desktop,wpf,dotnet,csharp,win32,ukrainian
```

Далі на сторінці репозиторію натисніть шестерню біля **About**:

1. **Description** — уже заповнено командою з кроку 3.
2. **Website** — залиште порожнім або вкажіть посилання на останній реліз.
3. Зніміть позначки **Packages** і **Deployments**: вони порожні й лише займають місце. **Releases** залиште.

## Крок 5. Зробіть головний скріншот

У README вже є п’ять знімків вікон, але немає головного: панель із відкритим меню на чистому робочому столі. Саме його люди бачать першим, тому він вартий п’яти хвилин.

1. Згорніть усі вікна (`Win` + `D`) і поставте нейтральні шпалери без особистих файлів на робочому столі.
2. Наведіть курсор на вкладку, щоб відкрилося меню з 5–7 застосунками.
3. `Win` + `Shift` + `S` → прямокутник: нижня третина екрана на всю ширину, щоб було видно панель завдань, вкладки й меню.
4. Збережіть як `Documentation/screenshots/hero.png` (ширина 1600–1920 px).
5. У `README.md` та `README.uk.md` замініть у першому блоці `<p align="center">` файл `onboarding-appearance.png` на `hero.png` і оновіть текст `alt`.

Ще краще — коротка анімація: запишіть 5–8 секунд (наведення → меню → клік) через ScreenToGif, збережіть як `Documentation/screenshots/demo.gif` до 5 МБ і вставте замість головного знімка.

Знімки можна лишити українськими (в англійському README про це сказано) або перезняти англійською: мова перемикається в Налаштуваннях → Загальне.

## Крок 6. Додайте картинку для соцмереж

Це зображення показується, коли посилання на репозиторій вставляють у Telegram, Slack чи X.

1. Підготуйте картинку 1280×640 px: головний скріншот на темному тлі, зліва значок і назва «Category Bar».
2. **Settings → General → Social preview → Edit → Upload an image**.

## Крок 7. Опублікуйте реліз

Архів уже зібрано: `artifacts\CategoryBar-0.3.0-win-x64.zip` (один `CategoryBar.exe`, близько 56 МБ). Якщо після цього змінювався код, зберіть заново:

```powershell
.\scripts\build.ps1 -SelfContained
```

```powershell
Compress-Archive -Path artifacts\portable\CategoryBar.exe -DestinationPath artifacts\CategoryBar-0.3.0-win-x64.zip -Force
```

Перед збіркою закрийте панель, запущену з `artifacts\portable`, інакше файл буде зайнятий.

Створіть реліз разом із тегом:

```powershell
gh release create v0.3.0 artifacts\CategoryBar-0.3.0-win-x64.zip --title "Category Bar 0.3.0" --notes-file Documentation\release-notes-0.3.0.md
```

Після цього значки «release» і «downloads» у README почнуть показувати дані, а посилання «Download» відкриватиме цей реліз.

Для наступних версій: змініть `<Version>` у `src/CategoryBar/CategoryBar.csproj`, зберіть, створіть новий файл нотаток і повторіть команду з новим номером.

## Крок 8. Перевірте сторінку очима відвідувача

Відкрийте репозиторій у приватному вікні браузера й пройдіть список:

- [ ] Під назвою видно опис і теми.
- [ ] README відкривається англійською, перемикач веде на українську версію і назад.
- [ ] Усі зображення завантажуються, значки показують версію, а не «no releases».
- [ ] Праворуч у блоці **Releases** є `v0.3.0` з позначкою **Latest**.
- [ ] Архів завантажується, розпаковується, `CategoryBar.exe` запускається на чистому профілі.

## Необов’язково, але корисно

- **Закріпіть репозиторій у профілі:** профіль → **Customize your pins**.
- **Увімкніть Discussions** (Settings → Features), якщо хочете збирати ідеї окремо від помилок.
- **Шаблони issue:** Settings → Features → Issues → **Set up templates** — готові форми «Bug report» і «Feature request».
- **Підпис коду.** Поки `.exe` не підписаний, SmartScreen показуватиме попередження. Сертифікат платний; для особистого проєкту достатньо чесно написати про це в README, що вже зроблено.
