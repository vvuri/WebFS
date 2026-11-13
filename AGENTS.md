## 0. Общий контекст проекта

- Репозиторий: `C:\code\sharp\WebFS`.
- Модуль: `github.com/vvuri/WebFS`.
- Бинарник: `webfs`.
- Go: `go 1.24` в `go.mod`; локально проект должен собираться на установленном Go.
- Зависимости: стандартная библиотека, `golang.org/x/net/html`, `golang.org/x/term`.
- CLI и сообщения — на английском. Документация и этот план — на русском.
- Приложение интерактивное. Агенты работают через stdin/stdout. JSON/MCP — вне MVP.
- Один смонтированный сайт за раз.
- Обход только same-origin. Внешние ссылки сохраняются, но не скачиваются.
- Ассеты показываются в `ls`/`tree`, но не скачиваются.
- Кэш только в памяти.
- Поддерживается только статический HTML; JavaScript не выполняется.

### 0.1. Карта пакетов

```text
cmd/webfs/main.go          // composition root
internal/version/          // версия
internal/config/           // дефолты, webfs.json, валидация
internal/domain/           // Site, Page, Link, Asset, URL и пути
internal/vfs/              // виртуальное дерево: Build, List, Resolve, Walk
internal/fetch/            // HTTP-клиент
internal/parse/            // HTML → title, links, assets
internal/cache/            // in-memory кэш документов
internal/service/          // cache-first загрузка и обновление Site
internal/crawler/          // BFS, robots, глубина, параллельность
internal/robots/           // минимальный парсер robots.txt
internal/cli/              // parser, registry, app, session, output
internal/cli/commands/     // команды
test/integration/          // //go:build integration
test/e2e/                  // //go:build e2e
testdata/                  // HTML-фикстуры и golden-файлы
```

### 0.2. Базовые правила, на которые ссылаются задачи

**URL.**

- Схема и host — в нижнем регистре.
- Порт по умолчанию убирается: `:80` для http, `:443` для https.
- Fragment отбрасывается.
- Trailing slash удаляется, кроме корня `/`.
- Query сохраняется как есть, без сортировки.
- Разрешены только `http` и `https`.
- Относительные ссылки разрешаются через `url.ResolveReference`.
- Same-origin = одинаковые scheme, host и port.

**Виртуальные пути.**

- `/` — корень.
- `/blog` — путь страницы или каталога.
- `/blog/2025` — вложенный путь.
- `/search?q=go` — путь с query.
- Каталог — это корень или путь, у которого есть потомки.
- Если у пути есть потомки и страница, это каталог с index-страницей.
- При коллизии пути: каталог важнее страницы, страница важнее ассета. Ассет с тем же путём в VFS не попадает.

**Вывод.**

- stdout — результаты команд.
- stderr — ошибки, предупреждения, прогресс, логи.
- Цвет только в TTY и только если `NO_COLOR` не задан.
- Сортировка: каталоги первыми, затем по имени без учёта регистра, затем по исходной строке.
- Время — `N ms`; тесты не проверяют точное значение.

**Качество.**

```bash
gofmt -l .
go vet ./...
golangci-lint run ./...
go test ./...
go test -race ./...
go test -tags=integration ./...
go test -tags=e2e ./...
```

`golint` не используется: он архивирован. Quality gate — `golangci-lint`.