# WebFS MVP — атомарные задачи

> Этот файл — набор самостоятельных задач в формате Jira-issue.
> Каждую задачу можно читать отдельно: в ней есть контекст, точное задание, критерии приёмки, тесты и запреты.
> Задачи выполняются по порядку, если в поле «Зависит от» не указано иное.

---

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

---

## 1. Фундамент

### WEBFS-001. Создать Go-модуль, каркас и минимальный бинарник

**Зависит от:** нет.
**Блокирует:** все задачи.

**Контекст.**
Репозиторий содержит только `spec/` и `README.md`. Нужен минимальный Go-проект, в который следующие задачи будут добавлять пакеты. Бизнес-логики в этой задаче нет.

**Задача.**
Создать модуль, структуру каталогов и `webfs`, который печатает баннер и завершается с кодом 0.

**Что сделать.**
- Выполнить `go mod init github.com/vvuri/WebFS`.
- Указать `go 1.24` в `go.mod`.
- Создать каталоги из §0.1 (пустые пакеты можно не создавать, только те, что нужны сейчас).
- `internal/version/version.go`:
  ```go
  package version

  var Version = "1.0.0"

  func String() string { return Version }
  ```
- `cmd/webfs/main.go`:
  - печатает `WebFS v` + `version.String()` и пустую строку;
  - завершается с кодом 0;
  - не содержит логики команд.
- Обновить `.gitignore`: `bin/`, `dist/`, `*.exe`, `coverage.out`, `*.test`.

**Критерии приёмки.**
- [ ] `go build ./...` проходит.
- [ ] `go test ./...` проходит.
- [ ] `go run ./cmd/webfs` печатает `WebFS v1.0.0` и завершается с кодом 0.
- [ ] В `go.mod` нет внешних зависимостей.
- [ ] Нет глобальных mutable-переменных и `init()` с побочными эффектами.

**Тесты.**
- `internal/version/version_test.go`: `String()` возвращает непустую строку.
- Smoke: собрать бинарник и запустить без аргументов.

**Запрещено.**
- Добавлять HTTP, HTML, команды, кэш, конфиг-файлы.
- Добавлять cobra, viper, testify, readline.
- Использовать `os.Exit` вне `main`.

---

### WEBFS-002. Настроить линтер и CI-скелет

**Зависит от:** WEBFS-001.

**Контекст.**
До написания кода нужно зафиксировать quality gate, чтобы стиль и ошибки ловились сразу. `golangci-lint` уже установлен в окружении.

**Задача.**
Добавить `.golangci.yml` и GitHub Actions workflow с базовыми проверками.

**Что сделать.**
- `.golangci.yml`:
  - `run.timeout: 5m`;
  - включить `govet`, `staticcheck`, `revive`, `errcheck`, `ineffassign`, `unused`, `gofmt`, `goimports`, `misspell`, `bodyclose`, `noctx`, `gocritic`, `unconvert`;
  - `gosec` не включать.
- `.github/workflows/ci.yml`:
  - триггеры: `push`, `pull_request`;
  - matrix: `ubuntu-latest`, `windows-latest`;
  - `actions/setup-go` с версией из `go.mod`;
  - шаги: `go build ./...`, `go test ./...`;
  - на ubuntu: `go test -race ./...`, `golangci-lint run ./...`;
  - кэш Go-модулей.
- Убедиться, что `golangci-lint run ./...` проходит на текущем каркасе.

**Критерии приёмки.**
- [ ] `golangci-lint run ./...` проходит.
- [ ] `go vet ./...` проходит.
- [ ] `gofmt -l .` пуст.
- [ ] YAML workflow валиден.
- [ ] CI не требует внешней сети, кроме загрузки модулей.

**Тесты.**
- Локальный прогон всех команд из §0.2.
- Проверка workflow через `act` не требуется; достаточно корректного YAML.

**Запрещено.**
- Добавлять `golint`.
- Включать `gosec`.
- Добавлять шаги деплоя/релиза.

---

### WEBFS-003. Реализовать конфиг с дефолтами и валидацией

**Зависит от:** WEBFS-001.

**Контекст.**
HTTP-клиенту, crawler и UI нужны одинаковые значения по умолчанию. Чтение файла появится позже, в WEBFS-037; сейчас нужен только тип и проверка значений.

**Задача.**
Создать `internal/config/config.go` с конфигом, дефолтами и `Validate`.

**Что сделать.**
- Типы:
  ```go
  type Config struct {
      HTTP  HTTPConfig
      Crawl CrawlConfig
      UI    UIConfig
      Log   LogConfig
  }

  type HTTPConfig struct {
      Timeout      time.Duration
      MaxBodyBytes int64
      UserAgent    string
      MaxRedirects int
  }

  type CrawlConfig struct {
      Depth         int
      MaxPages      int
      Concurrency   int
      Delay         time.Duration
      RespectRobots bool
  }

  type UIConfig struct {
      Color string // "auto" | "always" | "never"
  }

  type LogConfig struct {
      Level string // "debug" | "info" | "warn" | "error"
  }
  ```
- `Default() Config`:
  - HTTP: timeout 15s, max body 10 MiB, UA `WebFS/1.0`, max redirects 5.
  - Crawl: depth 2, max pages 200, concurrency 4, delay 100ms, respect robots true.
  - UI: color `auto`.
  - Log: level `warn`.
- `Validate() error`:
  - timeout > 0;
  - max body > 0;
  - user agent не пустой;
  - max redirects >= 0;
  - depth >= 0;
  - max pages >= 1;
  - concurrency 1..32;
  - delay >= 0;
  - color из допустимых значений;
  - log level из допустимых значений.
- Ошибки должны содержать имя поля.

**Критерии приёмки.**
- [ ] `Default()` возвращает заполненный конфиг.
- [ ] `Validate()` отклоняет каждое некорректное поле.
- [ ] `Validate()` принимает дефолтный конфиг.
- [ ] Пакет не читает файлы и не смотрит env.

**Тесты.**
- `internal/config/config_test.go`: таблица невалидных значений для каждого поля.
- Проверка дефолтов.

**Запрещено.**
- Читать `webfs.json` и env.
- Добавлять YAML/TOML.
- Использовать глобальный конфиг-синглтон.

---

## 2. URL, модель и виртуальная ФС

### WEBFS-004. Реализовать канонизацию URL и same-origin

**Зависит от:** WEBFS-001.

**Контекст.**
URL — ключ страницы, кэша и множества посещённых. Без единой канонизации `open`, `crawl` и `ls` будут считать `/blog`, `/blog/` и `/blog#x` разными страницами.

**Задача.**
Создать `internal/domain/url.go` с функциями разбора, канонизации и сравнения origin.

**Что сделать.**
- `ParseInput(raw string) (*url.URL, error)`:
  - trim пробелы;
  - если строка не содержит `://` и не является http(s)-URL, добавить `https://`;
  - разобрать;
  - требовать непустой host;
  - разрешать только http/https.
- `Canonicalize(raw string, base *url.URL) (*url.URL, error)`:
  - если `base != nil`, разрешить относительный URL через `base.ResolveReference`;
  - иначе `ParseInput`;
  - lower scheme/host;
  - убрать порт по умолчанию;
  - убрать fragment;
  - нормализовать путь: `.`/`..`, дубли `/`, trailing slash кроме `/`;
  - сохранить query без сортировки;
  - вернуть новый `*url.URL`.
- `Resolve(base, ref *url.URL) (*url.URL, error)` — обёртка над `ResolveReference` + канонизация.
- `SameOrigin(a, b *url.URL) bool` — scheme + host + port.
- `OriginKey(u *url.URL) string` — `scheme://host[:port]`.
- `IsHTTP(u *url.URL) bool`.
- Для IPv6 использовать `u.Hostname()`/`u.Port()`, не ломать скобки.

**Критерии приёмки.**
- [ ] `http://EXAMPLE.com:80/a` → `http://example.com/a`.
- [ ] `https://example.com` и `https://example.com/` дают один ключ.
- [ ] `https://example.com/a/../b` → `https://example.com/b`.
- [ ] `#fragment` не создаёт отдельный URL.
- [ ] `?b=2&a=1` сохраняет порядок.
- [ ] `mailto:`, `tel:`, `javascript:`, `data:` не проходят как страницы.
- [ ] `SameOrigin` различает `http`/`https` и разные порты.
- [ ] Ошибки содержат исходную строку.

**Тесты.**
- `internal/domain/url_test.go`: таблица 30+ кейсов.
- Идемпотентность: `Canonicalize(Canonicalize(x)) == Canonicalize(x)`.
- IPv6, unicode host, пустой путь, query-only URL.

**Запрещено.**
- Использовать `path/filepath` для URL.
- Делать HTTP-запросы.
- Смешивать с парсингом HTML.

---

### WEBFS-005. Реализовать виртуальные пути (display, parent, join, clean)

**Зависит от:** WEBFS-004.

**Контекст.**
`ls`, `cd`, `tree` и prompt работают не с полным URL, а с путями вида `/blog/2025` и `/search?q=go`. Нужны чистые функции без сети и без VFS.

**Задача.**
Создать `internal/domain/path.go` с преобразованиями URL ↔ виртуальный путь.

**Что сделать.**
- `DisplayPath(u *url.URL) string`:
  - декодированный путь;
  - если путь пустой — `/`;
  - если есть `RawQuery` — добавить `?` + query;
  - fragment не включать.
- `SplitQuery(p string) (path, query string)`.
- `CleanPath(p string) string`:
  - leading `/`;
  - свернуть `.`, `..`, дубли `/`;
  - убрать trailing slash кроме `/`;
  - `..` из корня оставить в корне.
- `ParentPath(p string) string`:
  - игнорировать query;
  - `/` → `/`;
  - `/blog` → `/`;
  - `/blog/2025` → `/blog`.
- `JoinPath(base, rel string) (string, error)`:
  - `rel` начинается с `/` → абсолютный;
  - иначе склеить с `base`;
  - вернуть `CleanPath`.
- `IsRoot(p string) bool`.

**Критерии приёмки.**
- [ ] `DisplayPath` для `https://example.com` → `/`.
- [ ] `DisplayPath` для `/search?q=go` → `/search?q=go`.
- [ ] `CleanPath("/a//b/../c/")` → `/a/c`.
- [ ] `ParentPath("/")` → `/`.
- [ ] `ParentPath("/blog")` → `/`.
- [ ] `ParentPath("/blog/2025")` → `/blog`.
- [ ] `JoinPath("/blog", "../about")` → `/about`.
- [ ] `JoinPath("/", "blog")` → `/blog`.

**Тесты.**
- `internal/domain/path_test.go`: таблица кейсов, включая unicode, пробелы, `..` из корня, query.

**Запрещено.**
- Использовать `path/filepath`.
- Обращаться к VFS или сети.
- Менять `url.URL` на месте.

---

### WEBFS-006. Создать доменную модель Site/Page/Link/Asset и индексацию

**Зависит от:** WEBFS-004, WEBFS-005.

**Контекст.**
После WEBFS-004/005 есть URL и пути, но негде хранить страницы, ссылки и ассеты. Парсер (WEBFS-015/016) должен вернуть данные в эти типы, а `ls`/`tree` — построить дерево. В этой задаче только модель и индексация, без HTTP и HTML.

**Задача.**
Создать `internal/domain/site.go`, `page.go`, `link.go`, `asset.go`, `error.go`.

**Что сделать.**
- Типы:
  ```go
  type Page struct {
      URL       *url.URL
      FinalURL  *url.URL
      Path      string
      Title     string
      Status    int
      Size      int64
      Latency   time.Duration
      Depth     int
      Loaded    bool
      Links     []Link
      Assets    []Asset
      FetchedAt time.Time
  }

  type Link struct {
      From *url.URL
      To   *url.URL
      Raw  string
      Kind LinkKind
  }

  type Asset struct {
      URL      *url.URL
      Path     string
      Kind     AssetKind
      Source   *url.URL
      External bool
  }

  type FetchError struct {
      URL     *url.URL
      Source  *url.URL
      Status  int
      Message string
  }

  type Site struct {
      Origin *url.URL
      Pages  map[string]*Page
      Assets map[string]*Asset
      Errors []FetchError
  }
  ```
- `LinkKind`: `LinkInternal`, `LinkExternal`, `LinkAnchor`, `LinkIgnored`.
- `AssetKind`: `AssetImage`, `AssetCSS`, `AssetJS`.
- `NewSite(origin *url.URL) *Site` — инициализирует map.
- `AddPage(p *Page)`:
  - ключ — канонический URL;
  - если `p.Path == ""`, вычислить `DisplayPath`;
  - заменить существующую запись.
- `AddKnownPage(u *url.URL)`:
  - если ключ уже есть — no-op;
  - иначе создать `Page{URL: u, Path: DisplayPath(u), Loaded: false}`.
- `AddAsset(a *Asset)`:
  - если `a.External` — no-op;
  - ключ — канонический URL;
  - дедупликация.
- `IndexPage(p *Page)`:
  - `AddPage(p)`;
  - для каждого internal link — `AddKnownPage(link.To)`;
  - для каждого same-origin asset — `AddAsset`.
- `AddError(e FetchError)`.
- `Stats() SiteStats`:
  - `KnownPages`, `LoadedPages`, `Assets`, `InternalLinks`, `ExternalLinks`, `Errors`.

**Критерии приёмки.**
- [ ] Повторный `AddPage` обновляет запись.
- [ ] `AddKnownPage` не перезаписывает загруженную страницу.
- [ ] Internal link добавляет known page.
- [ ] External link не добавляет страницу.
- [ ] External asset не попадает в `Site.Assets`.
- [ ] Ассеты дедуплицируются.
- [ ] `Stats` корректен на пустом и заполненном сайте.
- [ ] Нет nil-map паник.

**Тесты.**
- `internal/domain/site_test.go`: add/update, known→loaded, external, dedupe, stats, empty.
- Проверка, что `Page.Links` не теряет external/anchor/ignored.

**Запрещено.**
- Добавлять HTTP/HTML/CLI.
- Хранить тело страницы в `Page`.
- Делать глобальный singleton.
- Добавлять mutex в `Site`: синхронизация — в `Service` и кэше.

---

### WEBFS-007. Построить VFS-дерево и `List`

**Зависит от:** WEBFS-005, WEBFS-006.

**Контекст.**
`ls` и `tree` должны видеть не только загруженные страницы, но и известные URL из ссылок. Нужна структура, которая превращает плоские `Site.Pages`/`Site.Assets` в иерархию каталогов.

**Задача.**
Создать `internal/vfs/tree.go` и `node.go` с `Build` и `List`.

**Что сделать.**
- `NodeKind`: `NodeDir`, `NodePage`, `NodeAsset`.
- `Node`:
  ```go
  type Node struct {
      Path  string
      Name  string
      Kind  NodeKind
      Page  *domain.Page
      Asset *domain.Asset
  }
  ```
- `Tree`:
  ```go
  type Tree struct {
      byPath  map[string]*Node
      children map[string][]*Node
  }
  ```
- `Build(site *domain.Site) *Tree`:
  - создать корень `/`;
  - пройти `site.Pages` в отсортированном порядке ключей;
  - для каждого пути создать промежуточные каталоги;
  - если у пути есть потомки — `Kind=NodeDir`, `Page` прикрепить к узлу;
  - если потомков нет — `Kind=NodePage`;
  - затем пройти `site.Assets`; ассет добавлять только если по пути нет каталога/страницы;
  - заполнить `children`;
  - отсортировать: каталоги первыми, затем по `Name` (case-insensitive, затем исходная строка).
- `List(path string) ([]Node, error)`:
  - нормализовать путь;
  - если узла нет — `ErrNotFound`;
  - если узел — ассет — `ErrNotDir`;
  - если узел — страница без детей — пустой срез, `nil`;
  - иначе вернуть копию детей.
- `Name`: последний сегмент, для `/` — `/`.

**Критерии приёмки.**
- [ ] Пустой сайт: `List("/")` возвращает пустой срез.
- [ ] `/blog/2025` создаёт каталог `/blog` и страницу `/blog/2025`.
- [ ] Известная незагруженная страница видна.
- [ ] Ассет в корне виден.
- [ ] Ассет во вложенном каталоге виден.
- [ ] Query-путь `/search?q=go` виден как отдельный узел.
- [ ] При коллизии page/asset побеждает page.
- [ ] Сортировка детерминирована.
- [ ] `List` не делает сетевых запросов.

**Тесты.**
- `internal/vfs/tree_test.go`: root, nested, known, assets, query, collision, sorting, empty.
- Golden-файл списка корня.

**Запрещено.**
- Строить дерево по графу ссылок: только URL-иерархия.
- Делать fetch.
- Использовать реальные файлы.
- Добавлять CLI.

---

### WEBFS-008. Реализовать `Resolve`, `Walk`, `Exists`, `IsDir`

**Зависит от:** WEBFS-007.

**Контекст.**
`cd` должен понимать `/blog`, `blog`, `..`, `.` и query. `tree` должен обходить дерево в стабильном порядке. `List` уже умеет отдавать детей, но нет нормализации пути и обхода.

**Задача.**
Добавить в `internal/vfs/tree.go` навигационные операции.

**Что сделать.**
- `Resolve(cwd, arg string) (string, error)`:
  - пустой `arg` → `cwd`;
  - отделить query от `arg` (последний `?`);
  - если `arg` начинается с `/` — база `/`, иначе `cwd`;
  - разобрать сегменты, обработать `.`/`..`;
  - собрать путь, добавить query;
  - не проверять существование;
  - ошибка только на некорректный путь.
- `Exists(path string) bool`.
- `IsDir(path string) bool`.
- `Walk(root string, fn func(Node) error) error`:
  - найти узел;
  - обойти depth-first;
  - вызывать `fn` для корня и потомков;
  - сортировка как в `List`;
  - пробрасывать ошибку `fn`;
  - не зацикливаться.

**Критерии приёмки.**
- [ ] `Resolve("/blog", "2025")` → `/blog/2025`.
- [ ] `Resolve("/blog", "../about")` → `/about`.
- [ ] `Resolve("/", "..")` → `/`.
- [ ] `Resolve("/", ".")` → `/`.
- [ ] `Resolve("/", "/search?q=go")` → `/search?q=go`.
- [ ] `Resolve` не проверяет существование.
- [ ] `Walk` обходит в том же порядке, что `List`.
- [ ] `Walk` возвращает ошибку колбэка.
- [ ] `IsDir` для страницы без детей — false.

**Тесты.**
- `internal/vfs/resolve_test.go`: таблица путей.
- `internal/vfs/walk_test.go`: порядок, ошибка колбэка, пустое дерево.

**Запрещено.**
- Делать fetch в `Resolve`.
- Менять `Site`.
- Добавлять команды.

---

## 3. CLI-каркас

### WEBFS-009. Реализовать `Printer` с разделением stdout/stderr и TTY

**Зависит от:** WEBFS-001.

**Контекст.**
Командам нужен единый вывод. Ошибки и прогресс не должны попадать в stdout, иначе E2E и агенты получат мусор. `clear` должен работать только в TTY.

**Задача.**
Создать `internal/output/printer.go`.

**Что сделать.**
- `Printer`:
  ```go
  type Printer struct {
      Out   io.Writer
      Err   io.Writer
      TTY   bool
      Color bool
  }
  ```
- `New(out, err io.Writer) *Printer`:
  - `TTY` = true, если writer — `*os.File` и `term.IsTerminal(int(f.Fd()))`;
  - иначе false;
  - `Color` пока false.
- Методы:
  - `Printf(format string, args ...any)`;
  - `Println(args ...any)`;
  - `Errorf(format string, args ...any)` — в `Err` с префиксом `error: `;
  - `Warnf` — в `Err` с префиксом `warning: `;
  - `Prompt(s string)` — в `Out` без перевода строки;
  - `Clear()` — если `TTY`, писать `\x1b[2J\x1b[H`; иначе no-op.
- Все методы не паникуют при nil writer (nil заменять на `io.Discard`).

**Критерии приёмки.**
- [ ] `Printf`/`Println` идут в `Out`.
- [ ] `Errorf`/`Warnf` идут в `Err`.
- [ ] `Clear` в non-TTY ничего не пишет.
- [ ] `Clear` в TTY пишет ANSI-последовательность.
- [ ] Нет глобального `os.Stdout`.

**Тесты.**
- `internal/output/printer_test.go`: буферы, TTY/non-TTY, nil writer.

**Запрещено.**
- Добавлять цвет (WEBFS-034).
- Использовать `fmt.Println` напрямую в других пакетах.

---

### WEBFS-010. Реализовать токенизатор командной строки

**Зависит от:** WEBFS-001.

**Контекст.**
Пользователь вводит `open https://example.com`, `cd "my page"`, `crawl --depth 2`. Нужен разбор на аргументы без shell-подстановок.

**Задача.**
Создать `internal/cli/parser.go` с `Tokenize`.

**Что сделать.**
- `Tokenize(line string) ([]string, error)`.
- Правила:
  - пробелы и табы разделяют аргументы;
  - `'...'` и `"..."` группируют;
  - `\` экранирует следующий символ;
  - кавычки внутри других кавычек — обычный символ;
  - пустая строка → пустой срез;
  - незакрытая кавычка → ошибка;
  - никакой подстановки переменных, glob, `~`, комментариев.
- Склейка: `a"b c"d` → `ab cd`.

**Критерии приёмки.**
- [ ] `help` → `["help"]`.
- [ ] `open https://example.com` → 2 аргумента.
- [ ] `cd "my page"` → `["cd", "my page"]`.
- [ ] `cd 'a b'` → `["cd", "a b"]`.
- [ ] `a\ b` → `["a b"]`.
- [ ] `""` → один пустой аргумент.
- [ ] Незакрытая кавычка — ошибка.
- [ ] `$HOME` остаётся `$HOME`.

**Тесты.**
- `internal/cli/parser_test.go`: таблица 20+ кейсов.

**Запрещено.**
- Использовать `strings.Fields` как единственный парсер.
- Делать shell-подстановки.
- Добавлять внешние библиотеки.

---

### WEBFS-011. Реализовать реестр команд и REPL

**Зависит от:** WEBFS-009, WEBFS-010.

**Контекст.**
Нужен цикл: prompt → чтение строки → токенизация → поиск команды → выполнение → повтор. Ошибка команды не должна завершать приложение.

**Задача.**
Создать `internal/cli/command.go`, `registry.go`, `app.go`.

**Что сделать.**
- `Command`:
  ```go
  type Command interface {
      Name() string
      Aliases() []string
      Summary() string
      Usage() string
      Run(ctx context.Context, env *Env, args []string) error
  }
  ```
- `Env`:
  ```go
  type Env struct {
      Out     *output.Printer
      Session *Session
      Service *service.Service
      Config  config.Config
      Logger  *slog.Logger
  }
  ```
  На этом шаге `Session`/`Service` могут быть nil; команды их пока не используют.
- `Registry`:
  - `Register(cmd Command) error`;
  - дубликат имени/алиаса — ошибка;
  - `Lookup(name string) (Command, bool)`;
  - `Commands() []Command` — сортировка по имени.
- `App`:
  - поля `In io.Reader`, `Out *output.Printer`, `Registry *Registry`, `Env *Env`;
  - `Run(ctx context.Context) error`:
    - печатать баннер `WebFS v1.0.0`;
    - цикл: prompt `webfs>`, читать строку, токенизировать, выполнить;
    - пустая строка — продолжить;
    - ошибка токенизации/команды — `Printer.Errorf`, продолжить;
    - `ErrExit` — выйти с nil;
    - EOF — выйти с nil;
  - `Execute(ctx, line string) error` — для тестов, без цикла.
- `var ErrExit = errors.New("exit")`.

**Критерии приёмки.**
- [ ] `Run` печатает баннер и prompt.
- [ ] Неизвестная команда → `error: unknown command: x`, REPL жив.
- [ ] Пустая строка игнорируется.
- [ ] `ErrExit` завершает цикл.
- [ ] EOF завершает цикл.
- [ ] Ошибка команды не завершает цикл.
- [ ] Реестр сортирует команды.
- [ ] Дубликат имени/алиаса — ошибка при регистрации.

**Тесты.**
- `internal/cli/registry_test.go`.
- `internal/cli/app_test.go` с `strings.Reader` и `bytes.Buffer`.
- Фейковая команда, возвращающая ошибку и `ErrExit`.

**Запрещено.**
- Добавлять HTTP/domain.
- Использовать `os.Exit` в командах.
- Делать raw terminal.

---

### WEBFS-012. Добавить команды `help`, `clear`, `exit`, `version`

**Зависит от:** WEBFS-011.

**Контекст.**
Каркас готов, но пользователю нечего выполнять. Нужны базовые команды, чтобы проверить REPL и получить справку.

**Задача.**
Создать `internal/cli/commands/help.go`, `clear.go`, `exit.go`, `version.go` и зарегистрировать их в `main`.

**Что сделать.**
- `help`:
  - без аргументов: таблица `NAME`, `DESCRIPTION` по всем командам, сортировка по имени;
  - `help <command>`: `Usage()` и `Summary()`;
  - неизвестная команда: `unknown command: x`.
- `clear`: вызвать `env.Out.Clear()`.
- `exit`: вернуть `cli.ErrExit`.
- `version`: напечатать `WebFS v` + `version.String()`.
- В `cmd/webfs/main.go` собрать `Registry`, `Printer`, `App`.

**Критерии приёмки.**
- [ ] `help` выводит все зарегистрированные команды.
- [ ] `help open` печатает usage.
- [ ] `clear` не пишет ANSI в pipe.
- [ ] `exit` завершает приложение с кодом 0.
- [ ] `version` печатает версию.
- [ ] Все команды имеют непустые `Name`, `Summary`, `Usage`.

**Тесты.**
- `internal/cli/commands/help_test.go`.
- `clear_test.go`, `exit_test.go`, `version_test.go`.
- Smoke: `printf "help\nexit\n" | go run ./cmd/webfs`.

**Запрещено.**
- Добавлять домен/HTTP.
- Делать цветной help.
- Использовать `os.Exit`.

---

## 4. HTTP и парсинг

### WEBFS-013. Реализовать базовый HTTP-клиент

**Зависит от:** WEBFS-003, WEBFS-004.

**Контекст.**
`open` и `crawl` должны получать страницы. Весь HTTP должен быть изолирован в `internal/fetch`, чтобы команды не работали с сетью напрямую.

**Задача.**
Создать `internal/fetch/client.go`, `document.go`, `errors.go` с базовым GET.

**Что сделать.**
- `Document`:
  ```go
  type Document struct {
      FinalURL    *url.URL
      Status      int
      Header      http.Header
      Body        []byte
      Size        int64
      Latency     time.Duration
      FetchedAt   time.Time
      ContentType string
  }
  ```
- `Client`:
  - `New(cfg config.HTTPConfig, logger *slog.Logger) *Client`;
  - внутри `http.Client` с `Timeout: cfg.Timeout`;
  - `CheckRedirect`: не более `cfg.MaxRedirects`, разрешать только http/https.
- `Fetch(ctx context.Context, u *url.URL) (*Document, error)`:
  - `GET`;
  - заголовки `User-Agent`, `Accept: text/html,application/xhtml+xml`;
  - замерить `Latency` от старта до конца чтения тела;
  - 2xx — успех; иначе `HTTPStatusError`;
  - `FinalURL` = `resp.Request.URL`;
  - `Size` = длина тела;
  - `ContentType` = `Content-Type`;
  - тело читать с лимитом `cfg.MaxBodyBytes` (см. WEBFS-014 для деталей ошибок).
- Логировать на debug: метод, URL, статус, время; без тела.

**Критерии приёмки.**
- [ ] 200 возвращает `Document`.
- [ ] 404/500 возвращают `HTTPStatusError` с кодом и URL.
- [ ] Редирект сохраняет финальный URL.
- [ ] Редирект-петля — ошибка.
- [ ] `User-Agent` и `Accept` отправляются.
- [ ] `Latency > 0`.
- [ ] Контекст отменяет запрос.
- [ ] Тело закрывается всегда.

**Тесты.**
- `internal/fetch/client_test.go` с `httptest`:
  - 200, 404, 500;
  - redirect 1/5/6, loop;
  - проверка заголовков;
  - отмена контекста;
  - финальный URL.

**Запрещено.**
- Парсить HTML.
- Использовать кэш.
- Делать retries.
- Отключать TLS-проверку.
- Логировать тело/cookies.

---

### WEBFS-014. Добавить лимит тела, charset, gzip и типизированные ошибки

**Зависит от:** WEBFS-013.

**Контекст.**
Базовый клиент уже умеет GET, но без лимита можно получить OOM, а без charset — сломать кириллицу. Нужны предсказуемые ошибки для crawler.

**Задача.**
Доработать `internal/fetch` безопасным чтением и декодированием.

**Что сделать.**
- Типы ошибок:
  - `HTTPStatusError{URL string; Status int}`;
  - `BodyTooLargeError{URL string; Limit int64}`;
  - `UnsupportedContentError{URL, ContentType string}`;
  - `RedirectError{URL, Reason string}`;
  - реализовать `Error()`.
- Лимит:
  - если `Content-Length > MaxBodyBytes` — ошибка до чтения;
  - иначе `io.LimitReader(resp.Body, MaxBodyBytes+1)`;
  - если прочитано больше лимита — `BodyTooLargeError`.
- Content-Type:
  - разрешать `text/html`, `application/xhtml+xml`;
  - пустой Content-Type допускать;
  - остальные — `UnsupportedContentError`.
- Charset:
  - `golang.org/x/net/html/charset.NewReader(bytes.NewReader(raw), contentType)`;
  - читать декодированное тело;
  - `Size` = длина декодированного тела.
- Gzip: не отключать автоматическую распаковку стандартного транспорта.
- `FetchRaw` для robots.txt (без проверки Content-Type, тот же лимит) — понадобится в WEBFS-026.

**Критерии приёмки.**
- [ ] `Content-Length` больше лимита → `BodyTooLargeError`.
- [ ] Chunked-тело больше лимита → `BodyTooLargeError`.
- [ ] windows-1251 декодируется в UTF-8.
- [ ] gzip-ответ читается.
- [ ] `application/json` → `UnsupportedContentError`.
- [ ] Пустой Content-Type обрабатывается.
- [ ] `FetchRaw` не проверяет HTML.
- [ ] Ошибки не содержат тело.

**Тесты.**
- `internal/fetch/limits_test.go`.
- `internal/fetch/charset_test.go` с фикстурой windows-1251.
- `internal/fetch/errors_test.go` через `errors.As`.

**Запрещено.**
- Читать тело без лимита.
- Логировать тело.
- Добавлять retries.
- Менять TLS-настройки.

---

### WEBFS-015. Реализовать HTML-парсер: title и ссылки

**Зависит от:** WEBFS-004, WEBFS-006.

**Контекст.**
После загрузки HTML нужно получить title и ссылки. Парсер возвращает доменные объекты, чтобы `Service` мог проиндексировать сайт. Ассеты — в следующей задаче.

**Задача.**
Создать `internal/parse/parse.go` и `links.go`.

**Что сделать.**
- `Result`:
  ```go
  type Result struct {
      Title  string
      Links  []domain.Link
      Assets []domain.Asset
  }
  ```
- `Parse(docURL, finalURL *url.URL, body []byte) (*Result, error)`.
- Base URL:
  - первый `<base href>` разрешить относительно `finalURL`;
  - если невалиден — использовать `finalURL`.
- Title:
  - первый `<title>`;
  - текст, trim, collapse whitespace.
- Links:
  - `<a href>`;
  - пустой href пропускать;
  - `#...` → `LinkAnchor`;
  - не-http(s) → `LinkIgnored`;
  - same-origin → `LinkInternal`;
  - иначе `LinkExternal`;
  - `To` — канонический URL;
  - `Raw` — исходный href;
  - дедупликация: internal/external по каноническому URL, anchor/ignored по `Raw`.
- Ошибка `html.Parse` — обернуть; битый HTML не должен паниковать.

**Критерии приёмки.**
- [ ] Относительные ссылки становятся абсолютными.
- [ ] `<base href>` учитывается.
- [ ] `#anchor` → anchor.
- [ ] `mailto:`/`tel:`/`javascript:`/`data:` → ignored.
- [ ] Внешняя ссылка → external.
- [ ] Дубликаты удаляются.
- [ ] Title с entities нормализуется.
- [ ] Битый HTML не паникует.

**Тесты.**
- `internal/parse/parse_test.go` с `testdata/*.html`.
- Кейсы: relative, base, anchor, external, mailto, duplicates, malformed, title entities, no title.

**Запрещено.**
- Делать HTTP.
- Использовать кэш.
- Парсить ассеты (WEBFS-016).
- Добавлять goquery.

---

### WEBFS-016. Добавить в парсер ассеты: img/srcset/css/js

**Зависит от:** WEBFS-015.

**Контекст.**
`ls`/`tree` должны показывать CSS, JS и картинки. Ассеты не скачиваются, но их URL и тип нужны в модели.

**Задача.**
Расширить `internal/parse` извлечением ассетов.

**Что сделать.**
- `<img src>` → image.
- `<img srcset>`:
  - разбить по запятым;
  - взять часть до пробела;
  - пропускать пустые/невалидные;
  - каждый кандидат — отдельный asset.
- `<source src>` → image.
- `<link rel="stylesheet" href>` → css.
- `<script src>` → js.
- Все URL разрешить относительно base, канонизировать.
- Только http(s); `data:` пропускать.
- `domain.Asset{URL, Path: DisplayPath, Kind, Source: docURL, External: !SameOrigin}`.
- Дедупликация по каноническому URL.
- Внешние ассеты остаются в `Result.Assets`, но `Site.AddAsset` их отфильтрует.

**Критерии приёмки.**
- [ ] `img src` извлекается.
- [ ] `srcset` даёт все валидные кандидаты.
- [ ] CSS и JS извлекаются.
- [ ] Относительные ассеты становятся абсолютными.
- [ ] `data:` пропускается.
- [ ] Дубликаты удаляются.
- [ ] Внешний ассет имеет `External=true`.
- [ ] Ассеты не скачиваются.

**Тесты.**
- `internal/parse/assets_test.go`.
- Фикстуры: img, srcset, css, js, external, data URI, duplicates, missing href.

**Запрещено.**
- Скачивать ассеты.
- Парсить inline CSS `url()`.
- Парсить iframe/form/meta refresh.
- Добавлять goquery.

---

## 5. Кэш и сервис загрузки

### WEBFS-017. Реализовать in-memory кэш документов

**Зависит от:** WEBFS-006.

**Контекст.**
Повторный `open`, `cd`, `crawl` не должны скачивать одно и то же. Кэш хранит документы и тела для `grep`; граф сайта хранится отдельно, чтобы `cache clear` не ломал `ls`/`tree`.

**Задача.**
Создать `internal/cache/cache.go` и `memory.go`.

**Что сделать.**
- `Document`:
  ```go
  type Document struct {
      Key       string
      Page      *domain.Page
      Body      []byte
      Status    int
      FinalURL  *url.URL
      FetchedAt time.Time
      Size      int64
  }
  ```
- `Cache` интерфейс: `Get`, `Put`, `Delete`, `Clear`, `List`, `Stats`.
- `Stats{Count int; Bytes int64}`.
- `Memory`:
  - `map[string]*Document`;
  - `sync.RWMutex`;
  - `List` сортирует по `Key`;
  - `Clear` возвращает количество;
  - `Stats` считает документы и суммарный `len(Body)`.
- nil body допустим; `Stats` его не считает.

**Критерии приёмки.**
- [ ] `Get` после `Put` возвращает документ.
- [ ] `Delete` удаляет.
- [ ] `Clear` возвращает количество и очищает.
- [ ] `List` отсортирован.
- [ ] `Stats` корректен.
- [ ] Параллельные `Get`/`Put` проходят `-race`.
- [ ] Нет disk cache/TTL.

**Тесты.**
- `internal/cache/memory_test.go`.
- Параллельный тест с `sync.WaitGroup`.

**Запрещено.**
- Писать на диск.
- Добавлять TTL.
- Хранить `*http.Response`.

---

### WEBFS-018. Реализовать `Service.Load` с cache-first и индексацией

**Зависит от:** WEBFS-013, WEBFS-014, WEBFS-015, WEBFS-016, WEBFS-017.

**Контекст.**
Команды не должны знать про fetch/parse/cache. Нужен сервис, который берёт URL, проверяет кэш, при промахе скачивает, парсит и обновляет `Site`.

**Задача.**
Создать `internal/service/service.go` и `interfaces.go` с `Load`.

**Что сделать.**
- Интерфейсы:
  ```go
  type Fetcher interface {
      Fetch(ctx context.Context, u *url.URL) (*fetch.Document, error)
  }

  type Parser interface {
      Parse(docURL, finalURL *url.URL, body []byte) (*parse.Result, error)
  }
  ```
- `Service`:
  ```go
  type Service struct {
      mu      sync.Mutex
      site    *domain.Site
      cache   cache.Cache
      fetcher Fetcher
      parser  Parser
      logger  *slog.Logger
  }
  ```
- `New(fetcher Fetcher, parser Parser, cache cache.Cache, logger *slog.Logger) *Service`.
- `Load(ctx, canonical string) (*domain.Page, error)`:
  - если `site == nil` — ошибка `site is not mounted`;
  - разобрать canonical URL;
  - `cache.Get(key)` → вернуть `doc.Page`;
  - иначе `fetcher.Fetch`;
  - `parser.Parse`;
  - собрать `domain.Page`:
    - URL, FinalURL, Path, Title, Status, Size, Latency, Loaded=true, Links, Assets, FetchedAt;
  - под `mu`:
    - `site.IndexPage(page)`;
    - `cache.Put`;
  - вернуть страницу.
- `AddError(e domain.FetchError)` под `mu` — для crawler.
- `Site() *domain.Site`, `Cache() cache.Cache`.
- Не держать `mu` во время сетевого запроса.
- Ошибка fetch/parse не кэшируется.

**Критерии приёмки.**
- [ ] Повторный `Load` не делает второй HTTP-запрос.
- [ ] При промахе страница индексируется.
- [ ] Internal links появляются как known pages.
- [ ] Same-origin assets появляются в `Site.Assets`.
- [ ] Ошибки не попадают в кэш.
- [ ] `Load` потокобезопасен.
- [ ] `Service` не импортирует `net/http` напрямую.

**Тесты.**
- `internal/service/load_test.go` с фейковыми fetcher/parser.
- Счётчик вызовов, cache hit/miss, индексация, ошибки.
- Конкурентный `Load` под `-race`.

**Запрещено.**
- Делать disk cache.
- Добавлять TTL.
- Вызывать `net/http`.
- Логировать тело.

---

### WEBFS-019. Реализовать `Service.Open` и `Service.Refresh`

**Зависит от:** WEBFS-018.

**Контекст.**
`open` должен создавать новый сайт только после успешной загрузки. `refresh` должен принудительно обновлять страницу, не ломая старую версию при ошибке.

**Задача.**
Добавить в `internal/service` операции открытия и обновления.

**Что сделать.**
- `Open(ctx, rawURL string) (*domain.Site, *domain.Page, error)`:
  - `ParseInput` + `Canonicalize`;
  - создать новый `Site`;
  - загрузить стартовую страницу во временный сайт (без замены текущего);
  - при ошибке вернуть ошибку и не менять текущий `site`;
  - при успехе: очистить кэш, положить корневой документ, заменить `site`;
  - вернуть сайт и страницу.
- `Refresh(ctx, canonical string) (*domain.Page, error)`:
  - принудительный fetch, минуя кэш;
  - parse;
  - обновить `Page` в `Site`;
  - обновить `cache.Put`;
  - при ошибке вернуть ошибку и оставить старую версию.
- `Open` не должен сбрасывать кэш при ошибке.
- `Refresh` не должен удалять страницу при ошибке.

**Критерии приёмки.**
- [ ] Успешный `Open` заменяет сайт и очищает кэш.
- [ ] Ошибка `Open` сохраняет старый сайт.
- [ ] `Refresh` делает HTTP-запрос даже при наличии кэша.
- [ ] `Refresh` обновляет title/links/assets.
- [ ] Ошибка `Refresh` сохраняет старую страницу.
- [ ] `Open` с редиректом сохраняет финальный origin.

**Тесты.**
- `internal/service/open_test.go`.
- `internal/service/refresh_test.go`.
- Фейковый fetcher с ошибкой и успехом.

**Запрещено.**
- Прямой HTTP в сервисе.
- Очищать сайт при ошибке.
- Добавлять TTL/фон.

---

## 6. Команды подключения и навигации

### WEBFS-020. Добавить сессию, prompt и команду `open`/`mount`

**Зависит от:** WEBFS-011, WEBFS-012, WEBFS-019.

**Контекст.**
Пользователь должен подключиться к сайту и увидеть prompt сайта. Сессия хранит текущий сайт, cwd и историю.

**Задача.**
Создать `internal/cli/session.go` и `internal/cli/commands/open.go`.

**Что сделать.**
- `Session`:
  ```go
  type Session struct {
      Site *domain.Site
      Cwd  string
      Back []string
      Host string
  }
  ```
- Методы:
  - `Mounted() bool`;
  - `Prompt() string` — `webfs>` или `<host>>`;
  - `Mount(site *domain.Site, cwd string)`;
  - `Reset()`;
  - `PushHistory(path string)`;
  - `PopHistory() (string, bool)`.
- `open <url>`:
  - алиас `mount`;
  - ровно один аргумент;
  - `Service.Open`;
  - `Session.Mount(site, page.Path)`;
  - напечатать:
    ```text
    mounted https://example.com
    status 200 | size 1256 B | time 123 ms | title "Example Domain"
    ```
  - при ошибке — `Printer.Errorf`, состояние не менять.
- Prompt host: `site.Origin.Host`; порт входит, если он нестандартный.
- `open` без схемы использует https.

**Критерии приёмки.**
- [ ] `open https://example.com` монтирует и меняет prompt.
- [ ] `mount` работает.
- [ ] `open example.com` использует https.
- [ ] `open ftp://...` — ошибка.
- [ ] `open not-a-url` — ошибка.
- [ ] 404/500 не монтируют.
- [ ] Повторный `open` заменяет сайт.
- [ ] Ошибка не ломает REPL.
- [ ] В команде нет `net/http`.

**Тесты.**
- `internal/cli/commands/open_test.go` с фейковым сервисом.
- `internal/cli/session_test.go` для prompt/history.
- Integration с `httptest`.

**Запрещено.**
- Добавлять `ls`/`cd`.
- Строить Site в команде.
- Поддерживать несколько сайтов.

---

### WEBFS-021. Реализовать `ls` и `ls -l`

**Зависит от:** WEBFS-007, WEBFS-020.

**Контекст.**
После `open` пользователь должен увидеть ссылки главной. `ls` работает по VFS, не делает сетевых запросов и показывает известные, но ещё не загруженные страницы.

**Задача.**
Создать `internal/cli/commands/ls.go`.

**Что сделать.**
- Флаги:
  - `-l` — длинный формат;
  - максимум один позиционный аргумент (путь).
- Без аргумента — `Session.Cwd`.
- `vfs.Build(Session.Site)`.
- `List(path)`.
- Обычный вывод:
  - каталоги с `/`;
  - страницы и ассеты — имя.
- `-l`:
  - колонки `TYPE`, `SIZE`, `STATUS`, `TITLE`, `PATH`;
  - `text/tabwriter`;
  - для dir/page/asset разные типы;
  - неизвестные значения — `-`.
- Ошибки:
  - нет mount;
  - неизвестный путь;
  - путь — ассет → `not a directory`.
- `ls` на страницу без детей — пустой вывод, успех.

**Критерии приёмки.**
- [ ] После `open` корень показывает `about`, `blog`, `products`, `contacts`.
- [ ] Известные незагруженные страницы видны.
- [ ] Вложенные пути и query видны.
- [ ] Ассеты видны как файлы.
- [ ] `ls -l` различает dir/page/asset.
- [ ] Сортировка детерминирована.
- [ ] Нет fetch.
- [ ] Нет паники при пустом сайте/неизвестном пути.

**Тесты.**
- `internal/cli/commands/ls_test.go`.
- Golden: plain, long, nested, assets, query, empty.
- Фейковый fetcher с паникой при вызове — доказательство отсутствия сети.

**Запрещено.**
- Загружать страницы из `ls`.
- Использовать title как имя.
- Добавлять цвет (WEBFS-034).
- Реализовывать `tree`.

---

### WEBFS-022. Реализовать `cd` с автозагрузкой страниц

**Зависит от:** WEBFS-008, WEBFS-018, WEBFS-020.

**Контекст.**
`ls` показывает известные URL, но чтобы войти в незагруженную страницу, `cd` должен её загрузить. Каталог без страницы загружать не нужно.

**Задача.**
Создать `internal/cli/commands/cd.go`.

**Что сделать.**
- `cd` без аргумента → `/`.
- Аргумент может быть:
  - виртуальным путём (`/blog`, `../about`, `search?q=go`);
  - URL (`https://same-origin/path`).
- Для URL: `ParseInput`/`Canonicalize`; проверить same-origin; взять `DisplayPath`.
- Для пути: `vfs.Resolve(Session.Cwd, arg)`.
- Построить `vfs.Tree`.
- Если узел существует:
  - asset → ошибка `not a directory`;
  - dir/page:
    - если у узла есть `Page` и страница не загружена — `Service.Load`;
    - установить `Cwd`;
    - запушить старый cwd в историю.
- Если узла нет:
  - собрать URL из `site.Origin` + путь;
  - `Service.Load`;
  - при успехе установить cwd и историю;
  - при ошибке cwd не менять.
- Prompt не менять.

**Критерии приёмки.**
- [ ] `cd blog`, `cd /blog`, `cd ..`, `cd .`, `cd` работают.
- [ ] `cd` на незагруженную страницу загружает её.
- [ ] `cd` на 404 не меняет cwd.
- [ ] `cd` на внешний URL отклоняется.
- [ ] `cd` на asset — ошибка.
- [ ] `pwd` после `cd` корректен.
- [ ] История пополняется только при успехе.
- [ ] Prompt не меняется.

**Тесты.**
- `internal/cli/commands/cd_test.go` с фейковым сервисом.
- Проверка вызова `Load` для known page.
- Проверка отсутствия `Load` для чистого каталога.
- Интеграция: `open` → `cd` → `pwd`.

**Запрещено.**
- Добавлять `pushd`/`popd`/`forward`.
- Делать fetch в `pwd`.
- Менять prompt на путь.
- Поддерживать несколько историй.

---

### WEBFS-023. Реализовать `pwd` и `back`

**Зависит от:** WEBFS-020.

**Контекст.**
Пользователь должен видеть текущий виртуальный путь и возвращаться назад по истории.

**Задача.**
Создать `internal/cli/commands/pwd.go` и `back.go`.

**Что сделать.**
- `pwd`: напечатать `Session.Cwd`.
- `back`:
  - `PopHistory`;
  - если пусто — `error: no previous location`;
  - иначе установить `Cwd`;
  - не делать fetch;
  - не менять prompt.
- `open` очищает историю.

**Критерии приёмки.**
- [ ] `pwd` печатает `/`, `/blog`, `/blog/2025`.
- [ ] `back` возвращает предыдущий путь.
- [ ] `back` на пустой истории — ошибка.
- [ ] `back` не делает сетевых запросов.
- [ ] `open` сбрасывает историю.

**Тесты.**
- `pwd_test.go`, `back_test.go`.
- История: несколько `cd`, затем несколько `back`.

**Запрещено.**
- Добавлять `forward`.
- Делать fetch.
- Менять prompt.

---

## 7. Дерево и обход

### WEBFS-024. Реализовать `tree` с флагами

**Зависит от:** WEBFS-008, WEBFS-020.

**Контекст.**
`tree` должен показать структуру сайта по VFS, а не по графу ссылок. Ассеты включены по умолчанию; `--pages-only` их скрывает.

**Задача.**
Создать `internal/cli/commands/tree.go`.

**Что сделать.**
- Флаги:
  - `--depth N` — глубина от корня; 0 — только `/`;
  - `--ascii` — ASCII-символы;
  - `--pages-only` — скрыть ассеты.
- Построить `vfs.Tree`.
- Вывести `/`.
- Обойти детей рекурсивно:
  - Unicode: `├──`, `└──`, `│   `, `    `;
  - ASCII: `|--`, `` `-- ``, `|   `, `    `;
  - каталоги с `/`.
- Сортировка как в `List`.
- Никаких сетевых запросов.

**Критерии приёмки.**
- [ ] Дерево из спеки воспроизводится по структуре.
- [ ] Вложенность и последний элемент корректны.
- [ ] `--depth` ограничивает.
- [ ] `--ascii` не содержит Unicode box-drawing.
- [ ] `--pages-only` скрывает ассеты.
- [ ] Пустой сайт печатает `/`.
- [ ] Вывод детерминирован.
- [ ] Нет fetch/crawl.

**Тесты.**
- `internal/cli/commands/tree_test.go`.
- Golden: simple, deep, assets, query, depth, ascii, pages-only, empty.

**Запрещено.**
- Скачивать страницы.
- Использовать title как имя.
- Строить по графу ссылок.
- Добавлять цвет.

---

### WEBFS-025. Реализовать последовательный BFS-crawler

**Зависит от:** WEBFS-018, WEBFS-019.

**Контекст.**
`open` загружает только одну страницу. Нужен обход сайта по internal-ссылкам с ограничением глубины и количества страниц. Параллельность будет позже.

**Задача.**
Создать `internal/crawler/crawler.go`, `config.go`, `result.go`.

**Что сделать.**
- `Config`:
  ```go
  type Config struct {
      Start        *url.URL
      Depth        int
      MaxPages     int
      Concurrency  int // пока 1
      Delay        time.Duration
      RespectRobots bool // пока true, robots в WEBFS-026
      IgnoreRobots bool
  }
  ```
- `Result`:
  ```go
  type Result struct {
      Fetched   int
      Cached    int
      Errors    int
      External  int
      Cancelled bool
  }
  ```
- `Progress` интерфейс:
  ```go
  type Progress interface {
      Update(done, total, errors int)
      Done()
  }
  ```
- `Crawl(ctx, svc, cfg, progress) (*Result, error)`:
  - BFS по уровням;
  - старт depth 0;
  - visited — канонические URL;
  - на каждом уровне загружать страницы через `svc.Load`;
  - cache hit считать в `Cached`;
  - ссылки internal добавлять в следующий уровень;
  - external считать, но не скачивать;
  - depth > `Depth` не загружать;
  - `MaxPages` ограничивает;
  - ошибку страницы писать в `svc.AddError`, продолжать;
  - ошибку стартовой страницы возвращать;
  - `ctx` проверять перед каждым fetch;
  - `Delay` между запросами.
- `Concurrency` пока игнорировать (всегда 1).

**Критерии приёмки.**
- [ ] `Depth=0` — только старт.
- [ ] `Depth=1` — старт + прямые ссылки.
- [ ] Циклы не зацикливают.
- [ ] Внешние ссылки не скачиваются.
- [ ] `MaxPages` останавливает.
- [ ] 404/500 не ломают обход.
- [ ] Повторный crawl использует кэш.
- [ ] Отмена завершает обход.
- [ ] Ошибка старта — ошибка.

**Тесты.**
- `internal/crawler/crawler_test.go` с `httptest`.
- Граф root → a/b/c, цикл, external, 404, depth 0/1/2, max pages.
- Счётчик HTTP-запросов при повторном запуске.

**Запрещено.**
- Добавлять параллельность.
- Скачивать ассеты.
- Обходить внешние origin.
- Делать неограниченный обход.
- Мутировать `Site` из горутин (горутин пока нет).

---

### WEBFS-026. Реализовать `robots.txt` и подключить к crawler

**Зависит от:** WEBFS-014, WEBFS-025.

**Контекст.**
Crawler не должен ходить туда, куда сайт запрещает. Нужен минимальный парсер без внешних библиотек.

**Задача.**
Создать `internal/robots/robots.go` и интегрировать в crawler.

**Что сделать.**
- `Rules`:
  ```go
  type Rules struct {
      // ...
  }
  func Parse(body []byte) *Rules
  func (r *Rules) Allowed(u *url.URL) bool
  ```
- Поддержать:
  - `User-agent: *`;
  - `User-agent: WebFS`;
  - `Allow:`;
  - `Disallow:`;
  - пустой `Disallow` = разрешить всё;
  - самое длинное совпадение;
  - при равенстве — `Allow` важнее;
  - игнорировать `Sitemap`, комментарии, пустые строки.
- В crawler:
  - при `RespectRobots && !IgnoreRobots` загрузить `origin + /robots.txt` через `FetchRaw`;
  - 404 — разрешить всё;
  - сетевая ошибка/5xx — предупреждение и разрешить;
  - кэшировать правила на время обхода;
  - перед загрузкой страницы проверять `Allowed`.
- `--ignore-robots` отключает.

**Критерии приёмки.**
- [ ] `Disallow: /private` блокирует `/private`.
- [ ] `Allow: /private/public` разрешает более длинный путь.
- [ ] Группы `User-agent` обрабатываются.
- [ ] `404 robots.txt` не блокирует.
- [ ] `--ignore-robots` игнорирует правила.
- [ ] `robots.txt` запрашивается один раз.
- [ ] Ошибка robots не ломает crawl.

**Тесты.**
- `internal/robots/robots_test.go`.
- `internal/crawler/robots_test.go` с `httptest`.

**Запрещено.**
- Добавлять внешнюю библиотеку robots.
- Рекурсивно парсить robots.
- Блокировать весь сайт при сетевой ошибке.

---

### WEBFS-027. Добавить команду `crawl` и прогресс

**Зависит от:** WEBFS-025, WEBFS-026.

**Контекст.**
Crawler готов, но пользователю нужна команда с флагами и понятным итогом.

**Задача.**
Создать `internal/cli/commands/crawl.go`.

**Что сделать.**
- Флаги:
  - `--depth N` (default из конфига);
  - `--max-pages N`;
  - `--concurrency N` (пока 1, валидация 1..32);
  - `--ignore-robots`.
- Требовать mount.
- Старт — корень origin (`site.Origin` + `/`).
- Прогресс:
  - TTY: `\rCrawling... N pages, E errors` в stderr;
  - non-TTY: строка каждые 10 страниц;
  - по завершении очистить строку.
- Итог в stdout:
  ```text
  crawl finished: fetched=12 cached=3 errors=1 external=4 elapsed=1.2s
  ```
- Отмена:
  ```text
  crawl cancelled: fetched=5 cached=0 errors=0 external=1 elapsed=0.4s
  ```
- Валидация: depth >= 0, max pages >= 1, concurrency 1..32.
- Ошибка crawler — `Printer.Errorf`, REPL продолжает.

**Критерии приёмки.**
- [ ] `crawl` использует дефолты.
- [ ] Флаги переопределяют конфиг.
- [ ] Некорректные флаги — ошибка.
- [ ] Без mount — ошибка.
- [ ] Итог содержит все счётчики.
- [ ] Прогресс только в stderr.
- [ ] В non-TTY нет ANSI.
- [ ] Отмена печатает cancelled.

**Тесты.**
- `internal/cli/commands/crawl_test.go` с фейковым crawler.
- Проверка парсинга флагов и итоговой строки.
- Проверка отсутствия прогресса в stdout.

**Запрещено.**
- Делать HTTP в команде.
- Скачивать ассеты.
- Игнорировать `robots.txt` по умолчанию.

---

## 8. Поиск и кэш-команды

### WEBFS-028. Реализовать `find`

**Зависит от:** WEBFS-006, WEBFS-020.

**Контекст.**
Пользователь должен находить страницы по URL и title среди известных страниц, без сети.

**Задача.**
Создать `internal/cli/commands/find.go`.

**Что сделать.**
- Синтаксис: `find <text> [--url] [--title] [--case-sensitive]`.
- Ровно один текстовый аргумент.
- По умолчанию искать по `Page.Path` и `Page.Title`.
- `--url` — только path.
- `--title` — только title.
- Регистр: по умолчанию insensitive.
- Известные незагруженные страницы участвуют по path; title пустой.
- Вывод: отсортированные пути, по одному на строку.
- Нет совпадений — пустой вывод, успех.
- Нет mount — ошибка.

**Критерии приёмки.**
- [ ] Находит по URL.
- [ ] Находит по title.
- [ ] Видит known-но-незагруженные страницы по URL.
- [ ] `--url`/`--title` ограничивают.
- [ ] `--case-sensitive` работает.
- [ ] Нет fetch/crawl.
- [ ] Пустой запрос — ошибка.

**Тесты.**
- `internal/cli/commands/find_test.go` с фикстурным Site.
- Case-sensitive/insensitive, no match, empty query.

**Запрещено.**
- Делать fetch.
- Добавлять regex.
- Искать по ассетам.

---

### WEBFS-029. Реализовать `grep` по кэшу

**Зависит от:** WEBFS-017, WEBFS-020.

**Контекст.**
`find` ищет по метаданным. `grep` должен искать по телу уже загруженных страниц; если кэш очищен, он не должен ходить в сеть.

**Задача.**
Создать `internal/cli/commands/grep.go`.

**Что сделать.**
- Синтаксис: `grep <text> [--case-sensitive] [--max-per-page N] [--max-total N]`.
- Значения по умолчанию: 3 и 100.
- Искать по `cache.List()`.
- Пропускать документы с `nil`/пустым body.
- Делить body по `\n`.
- Вывод: `path:line: text`.
- `text` обрезать до 200 символов, trim.
- Регистр по умолчанию insensitive.
- Если кэш пуст:
  ```text
  cache is empty; run crawl or open pages first
  ```
- Нет mount — ошибка.
- Никаких сетевых запросов.

**Критерии приёмки.**
- [ ] Находит по телу.
- [ ] Выводит номер строки.
- [ ] Работают лимиты.
- [ ] `--case-sensitive` работает.
- [ ] Ассеты не ищутся.
- [ ] Пустой кэш — сообщение, не паника.
- [ ] Нет fetch.

**Тесты.**
- `internal/cli/commands/grep_test.go`.
- Кейсы: match, no match, limits, case, empty cache, nil body.

**Запрещено.**
- Делать fetch/crawl.
- Добавлять regex.
- Искать в бинарных данных.
- Использовать внешний grep.

---

### WEBFS-030. Реализовать `cache list` и `cache clear`

**Зависит от:** WEBFS-017, WEBFS-020.

**Контекст.**
Пользователь должен видеть, что закэшировано, и уметь очистить кэш. Очистка не должна ломать граф сайта.

**Задача.**
Создать `internal/cli/commands/cache.go`.

**Что сделать.**
- `cache list`:
  - таблица `URL`, `STATUS`, `SIZE`, `AGE`, `TITLE`;
  - сортировка по URL;
  - `AGE` — человекочитаемое (`12s`, `3m`, `1h`);
  - пустой кэш — `cache is empty`.
- `cache clear`:
  - `cache.Clear()`;
  - напечатать `cleared N cached documents`;
  - граф `Site` не трогать.
- `cache` без подкоманды — usage-ошибка.
- Нет mount — ошибка.

**Критерии приёмки.**
- [ ] `list` показывает документы.
- [ ] `clear` возвращает количество.
- [ ] После `clear` список пуст.
- [ ] `ls`/`tree` продолжают работать.
- [ ] `grep` сообщает о пустом кэше.
- [ ] Нет disk cache/TTL.

**Тесты.**
- `internal/cli/commands/cache_test.go` с фейковым кэшем.
- Golden-таблица.
- Проверка сохранения Site.

**Запрещено.**
- Удалять граф сайта.
- Писать на диск.
- Добавлять TTL.

---

### WEBFS-031. Реализовать `refresh`

**Зависит от:** WEBFS-019, WEBFS-022.

**Контекст.**
После `cache clear` или изменения сайта пользователю нужно принудительно обновить страницу. Ошибка обновления не должна удалять старую версию.

**Задача.**
Создать `internal/cli/commands/refresh.go`.

**Что сделать.**
- Синтаксис: `refresh [path] [--all]`.
- Без аргумента:
  - если `Cwd` — страница, обновить её;
  - если `Cwd` — каталог с index-страницей, обновить её;
  - иначе ошибка `no page at /...`.
- С аргументом:
  - `vfs.Resolve`;
  - asset → ошибка;
  - обновить страницу по пути.
- `--all`:
  - обновить все документы из кэша;
  - ограничить `MaxPages`;
  - печатать итог.
- Использовать `Service.Refresh`.
- При ошибке — `Printer.Errorf`, старую версию оставить.

**Критерии приёмки.**
- [ ] `refresh` делает новый HTTP-запрос.
- [ ] Обновляет title/links/assets.
- [ ] Ошибка не удаляет старую версию.
- [ ] `refresh --all` ограничен.
- [ ] Нет mount — ошибка.
- [ ] Каталог без страницы — ошибка.

**Тесты.**
- `internal/cli/commands/refresh_test.go`.
- Success/error, cache bypass, `--all`, no page.

**Запрещено.**
- Фоновое обновление.
- TTL.
- Disk cache.
- Удаление страницы при ошибке.

---

## 9. Параллельность

### WEBFS-032. Распараллелить crawl внутри уровня BFS

**Зависит от:** WEBFS-025, WEBFS-027.

**Контекст.**
Последовательный crawl работает, но медленный. Нужно загружать страницы одного уровня параллельно, сохранив детерминированный результат.

**Задача.**
Добавить worker pool в `internal/crawler`.

**Что сделать.**
- Использовать `Config.Concurrency` (1..32).
- Для каждого уровня:
  - собрать уникальные URL;
  - запустить не более N воркеров;
  - семафор — буферизованный канал;
  - `sync.WaitGroup`;
  - результаты собирать в срез, индексированный по URL, для детерминизма.
- Воркеры вызывают `svc.Load` (сервис потокобезопасен).
- Только координатор формирует следующий уровень.
- `Delay` применять перед fetch в каждом воркере.
- `--concurrency 1` должен давать тот же результат, что последовательный crawl.

**Критерии приёмки.**
- [ ] Фактическая конкурентность не превышает N.
- [ ] Результат совпадает с `Concurrency=1`.
- [ ] Нет гонок (`-race`).
- [ ] Нет горутин-сирот.
- [ ] `MaxPages` соблюдается.
- [ ] Ошибки страниц не прерывают уровень.

**Тесты.**
- `internal/crawler/parallel_test.go`.
- Сервер считает максимум одновременных запросов.
- Сравнение результатов N=1 и N=4.
- `-race`.

**Запрещено.**
- Запускать неограниченное число горутин.
- Мутировать Site из координатора без синхронизации (Service уже синхронизирует).
- Использовать `errgroup` или внешние библиотеки.
- Менять семантику глубины.

---

### WEBFS-033. Добавить отмену, Ctrl+C и прогресс

**Зависит от:** WEBFS-027, WEBFS-032.

**Контекст.**
Долгий crawl должен отменяться, а прогресс — не ломать pipe. Ctrl+C во время команды не должен закрывать приложение.

**Задача.**
Доработать `App` и `crawler` обработкой `context.Context` и сигналов.

**Что сделать.**
- `App`:
  - на каждую команду создавать `context.WithCancel`;
  - слушать `os.Interrupt` во время команды;
  - Ctrl+C → `cancel()`, команда возвращает `context.Canceled`;
  - `App` печатает `cancelled` и продолжает REPL;
  - Ctrl+C на prompt → выход с кодом 130.
- Для тестов сделать `Interrupt <-chan struct{}` в `App`; в production подключать `os/signal`.
- Crawler:
  - проверять `ctx` перед fetch и после;
  - не запускать новые задачи после отмены;
  - дождаться текущих воркеров;
  - вернуть частичный `Result{Cancelled: true}`.
- Прогресс:
  - TTY — `\r`, очистка строки;
  - non-TTY — строка каждые 10 страниц;
  - только stderr.

**Критерии приёмки.**
- [ ] Отмена после K страниц возвращает `Cancelled=true`.
- [ ] Нет горутин-сирот.
- [ ] `-race` чистый.
- [ ] Приложение после отмены продолжает принимать команды.
- [ ] В non-TTY нет ANSI/`\r`.
- [ ] Ctrl+C на prompt завершает с 130.

**Тесты.**
- `internal/crawler/cancel_test.go`.
- `internal/cli/app_signal_test.go` с `Interrupt`-каналом.
- Проверка прогресса в буфер.

**Запрещено.**
- Использовать OS-сигналы в unit-тестах.
- Игнорировать контекст.
- Оставлять горутины после возврата.

---

## 10. Полировка

### WEBFS-034. Добавить цветной вывод с `NO_COLOR`

**Зависит от:** WEBFS-009, WEBFS-021, WEBFS-024.

**Контекст.**
Цвет улучшает читаемость, но не должен попадать в pipe/E2E. Нужно уважать `NO_COLOR` и TTY.

**Задача.**
Добавить цвет в `internal/output` и применить в командах.

**Что сделать.**
- `Printer`:
  - `Color` включается, если `TTY && os.Getenv("NO_COLOR")=="" && config.UI.Color != "never"`;
  - `config.UI.Color == "always"` включает цвет даже без TTY, кроме `NO_COLOR`.
- Методы:
  - `Dir(s string) string`;
  - `Asset(s string) string`;
  - `Error(s string) string`;
  - `Success(s string) string`.
- Цвета:
  - dir — cyan;
  - asset — yellow;
  - error — red;
  - success — green.
- При выключенном цвете методы возвращают строку без изменений.
- Применить в `ls`, `tree`, `open`, `stats`, `check`.

**Критерии приёмки.**
- [ ] В non-TTY нет ANSI.
- [ ] `NO_COLOR=1` отключает цвет.
- [ ] `--color=never` (конфиг) отключает.
- [ ] В TTY каталоги/ассеты/ошибки окрашены.
- [ ] Текст без цвета не меняется.

**Тесты.**
- `internal/output/color_test.go`.
- Golden для цветного/нецветного.
- `NO_COLOR` через `t.Setenv`.

**Запрещено.**
- Красить stdout в non-TTY.
- Менять машинно-читаемые поля.
- Добавлять внешние color-библиотеки.

---

### WEBFS-035. Реализовать `stats` и `check`

**Зависит от:** WEBFS-006, WEBFS-017, WEBFS-020.

**Контекст.**
Пользователю нужна сводка по сайту и отчёт о ссылках, которые не загрузились во время crawl. `check` не должен ходить в сеть.

**Задача.**
Создать `internal/cli/commands/stats.go` и `check.go`.

**Что сделать.**
- `stats`:
  ```text
  origin: https://example.com
  pages: 12 known, 8 loaded
  assets: 5
  links: 30 internal, 4 external
  errors: 1
  cache: 8 documents, 120 KiB
  cwd: /blog
  ```
  - использовать `Site.Stats()` и `Cache.Stats()`.
- `check`:
  - вывести `Site.Errors`;
  - строка: `URL STATUS MESSAGE source=...`;
  - если пусто — `no broken links`;
  - итог `broken links: N`.
- Нет mount — ошибка.
- Без сети.

**Критерии приёмки.**
- [ ] `stats` считает корректно.
- [ ] `check` показывает ошибки.
- [ ] `check` пустой — сообщение.
- [ ] Нет fetch.
- [ ] Вывод стабилен.

**Тесты.**
- `stats_test.go`, `check_test.go`.
- Golden-вывод.

**Запрещено.**
- Перепроверять ссылки по сети.
- Менять Site.
- Добавлять TTL.

---

### WEBFS-036. Реализовать `export` в JSON и DOT

**Зависит от:** WEBFS-006, WEBFS-020.

**Контекст.**
Нужно сохранить структуру сайта для внешних инструментов. JSON — для программ, DOT — для Graphviz.

**Задача.**
Создать `internal/cli/commands/export.go`, `internal/export/json.go`, `dot.go`.

**Что сделать.**
- Синтаксис: `export [--format json|dot] [--output FILE]`.
- Формат по умолчанию — json.
- Без `--output` или `--output -` — stdout.
- JSON:
  ```json
  {
    "version": 1,
    "origin": "https://example.com",
    "pages": [...],
    "links": [...],
    "assets": [...],
    "errors": [...]
  }
  ```
  - страницы сортировать по URL;
  - ссылки — по from/to;
  - ассеты — по URL;
  - ошибки — по URL.
- DOT:
  - `digraph webfs`;
  - узлы страниц и ассетов;
  - рёбра internal/external;
  - экранирование `"` и `\`.
- Не включать тела страниц и cookies.

**Критерии приёмки.**
- [ ] JSON валиден и детерминирован.
- [ ] DOT открывается Graphviz.
- [ ] `--output` пишет файл.
- [ ] `--output -` пишет stdout.
- [ ] Нет секретов.
- [ ] Пустой сайт экспортируется.

**Тесты.**
- `internal/export/json_test.go`, `dot_test.go`.
- Golden JSON/DOT.
- Экранирование.

**Запрещено.**
- Включать body/cookies.
- Менять Site.
- Делать fetch.

---

### WEBFS-037. Загрузить `webfs.json`

**Зависит от:** WEBFS-003.

**Контекст.**
Конфиг с дефолтами есть, но пользователь должен уметь переопределить параметры файлом. Нужна простая загрузка JSON без hot reload.

**Задача.**
Создать `internal/config/load.go`.

**Что сделать.**
- `Load() (Config, error)`:
  - если `WEBFS_CONFIG` задан — читать этот файл;
  - иначе если `./webfs.json` существует — читать его;
  - иначе `Default()`.
- JSON-схема:
  ```json
  {
    "http": {
      "timeout_seconds": 15,
      "max_body_bytes": 10485760,
      "user_agent": "WebFS/1.0",
      "max_redirects": 5
    },
    "crawl": {
      "depth": 2,
      "max_pages": 200,
      "concurrency": 4,
      "delay_ms": 100,
      "respect_robots": true
    },
    "ui": { "color": "auto" },
    "log": { "level": "warn" }
  }
  ```
- `json.Decoder.DisallowUnknownFields()`.
- Отсутствующие поля — дефолты.
- `Validate()` после загрузки.
- CLI-флаги переопределяют значения (реализуется в командах).
- `cmd/webfs/main.go` использует `Load()`.

**Критерии приёмки.**
- [ ] Нет файла — дефолты.
- [ ] Файл загружается.
- [ ] `WEBFS_CONFIG` имеет приоритет.
- [ ] Неизвестное поле — ошибка.
- [ ] Невалидные значения — ошибка.
- [ ] Нет YAML/TOML.
- [ ] Нет hot reload.

**Тесты.**
- `internal/config/load_test.go`.
- `t.TempDir`, `t.Setenv`.
- Валидный/невалидный JSON.

**Запрещено.**
- Добавлять YAML/TOML.
- Читать конфиг при каждой команде.
- Хранить секреты.

---

## 11. Интеграционные и E2E-тесты

### WEBFS-038. Написать интеграционный набор

**Зависит от:** WEBFS-019, WEBFS-027, WEBFS-031, WEBFS-032.

**Контекст.**
Unit-тесты проверяют части отдельно. Нужно проверить связку fetch + parse + cache + service + crawler + vfs на локальном сервере.

**Задача.**
Создать `test/integration` с тегом `//go:build integration`.

**Что сделать.**
- Fixture-сервер:
  - `/` со ссылками на `/about`, `/blog`, `/products`, `/contacts`;
  - `/blog` со ссылками на `/blog/2025`, `/blog/archive`;
  - CSS/JS/картинки;
  - внешняя ссылка;
  - `/missing` → 404;
  - `/redirect` → `/`;
  - `/robots.txt`;
  - windows-1251 страница;
  - `/slow` с задержкой.
- Хелперы:
  - `newFixtureServer(t) *httptest.Server`;
  - `newService(t, server.URL) *service.Service`.
- Тесты:
  - `TestOpenAndList`: open → VFS root содержит about/blog/products/contacts.
  - `TestCrawlDepth`: crawl depth 2 → ожидаемые страницы, внешние не загружены.
  - `TestCrawlUsesCache`: повторный crawl не увеличивает HTTP-запросы.
  - `TestTree`: дерево содержит вложенные узлы.
  - `TestFindGrep`: поиск по URL/title/body.
  - `TestRefresh`: обновляет title/links.
  - `TestCacheClearKeepsGraph`: clear → ls работает, grep пуст.
  - `TestParallelCrawl`: concurrency 4, результат как при 1.
  - `TestCancel`: отмена после K страниц.
- Без реального интернета.
- Без `time.Sleep` для синхронизации; использовать каналы/контекст.

**Критерии приёмки.**
- [ ] `go test -tags=integration ./...` зелёный.
- [ ] `go test -race -tags=integration ./...` зелёный (где поддерживается).
- [ ] Нет реальных сетевых запросов.
- [ ] Нет flaky-тестов.
- [ ] Все сценарии детерминированы.

**Тесты.** Сам набор.

**Запрещено.**
- Заменять unit-тесты интеграционными.
- Использовать реальные сайты.
- Проверять точное время.
- Оставлять открытые серверы/файлы.

---

### WEBFS-039. Собрать E2E-харнесс

**Зависит от:** WEBFS-012, WEBFS-020, WEBFS-027.

**Контекст.**
Нужно проверить бинарник как чёрный ящик: stdin-скрипт → stdout/stderr → код возврата. Это же доказывает, что приложение пригодно для скриптов/агентов.

**Задача.**
Создать `test/e2e/harness_test.go` с тегом `//go:build e2e`.

**Что сделать.**
- `TestMain`:
  - найти корень модуля через `go env GOMOD` или `runtime.Caller`;
  - собрать `webfs`/`webfs.exe` в `t.TempDir()`;
  - сохранить путь в пакетной переменной.
- `runCLI(t, script string) (stdout, stderr string, exitCode int)`:
  - `exec.CommandContext` с timeout 30s;
  - `Stdin = strings.NewReader(script)`;
  - `Stdout`/`Stderr` — буферы;
  - вернуть код.
- `normalize(s string) string`:
  - заменить `\d+ ms` на `<time>`;
  - убрать `\r`;
  - нормализовать CRLF.
- Базовый тест:
  - скрипт `help\nexit\n`;
  - проверить баннер, prompt, `help`, код 0;
  - проверить отсутствие ANSI.
- Fixture-сервер можно поднять в тесте и передать URL в скрипт.

**Критерии приёмки.**
- [ ] Бинарник собирается.
- [ ] `runCLI` не зависает.
- [ ] Код возврата проверяется.
- [ ] Нет shell-пайпов.
- [ ] Нет требования TTY.
- [ ] Нет ANSI в pipe.

**Тесты.** Базовый `help`/`exit`.

**Запрещено.**
- Использовать `expect`.
- Коммитить бинарник.
- Ходить в интернет.
- Проверять точное время.

---

### WEBFS-040. Покрыть E2E-сценарии

**Зависит от:** WEBFS-039, WEBFS-038.

**Контекст.**
Харнесс готов. Нужно покрыть основные пользовательские сценарии и ошибки.

**Задача.**
Добавить E2E-тесты в `test/e2e`.

**Что сделать.**
- Успешный сценарий:
  ```text
  open http://127.0.0.1:PORT
  ls
  ls -l
  cd blog
  pwd
  back
  crawl --depth 2
  tree
  find blog
  grep example
  cache list
  refresh
  cache clear
  exit
  ```
  Проверить:
  - prompt;
  - ожидаемые имена;
  - пути;
  - дерево;
  - поиск;
  - кэш;
  - код 0;
  - отсутствие ANSI.
- Ошибки:
  - `open ftp://example.com`;
  - `open http://127.0.0.1:PORT/missing`;
  - `ls` без mount;
  - `cd /missing`;
  - `crawl --depth -1`;
  - `unknown-command`;
  - `cache clear` при пустом кэше;
  - `refresh` без mount.
- Отмена:
  - `crawl` на `/slow` + `Interrupt`? В E2E сигнал сложен; можно проверить через таймаут и `context.Canceled` в integration. В E2E достаточно, что процесс жив после ошибки.
- Все проверки — по стабильным строкам; время нормализовать.

**Критерии приёмки.**
- [ ] `go test -tags=e2e ./...` зелёный на Windows и Linux.
- [ ] Все команды покрыты.
- [ ] Ошибки не роняют процесс.
- [ ] Нет ANSI.
- [ ] Нет внешней сети.
- [ ] Нет flaky-тестов.

**Тесты.** Сам E2E-набор.

**Запрещено.**
- Использовать реальные сайты.
- Проверять точное время.
- Требовать TTY.
- Использовать shell.

---

## 12. Документация и релиз

### WEBFS-041. Обновить README, добавить `docs/commands.md`, финализировать CI

**Зависит от:** все обязательные задачи.

**Контекст.**
Нужно, чтобы новый человек мог собрать, запустить и понять ограничения. CI должен прогонять все наборы.

**Задача.**
Завершить документацию и релизную сборку.

**Что сделать.**
- `README.md`:
  - установка зависимостей;
  - `go build -o bin/webfs ./cmd/webfs`;
  - запуск;
  - быстрый пример `open` → `ls` → `crawl` → `tree`;
  - список команд;
  - конфиг `webfs.json`;
  - тесты;
  - ограничения MVP.
- `docs/commands.md`:
  - синтаксис каждой команды;
  - флаги;
  - примеры вывода.
- CI:
  - unit, `-race` на ubuntu, integration, e2e, lint, build;
  - matrix ubuntu/windows;
  - без внешней сети.
- Релизная сборка:
  ```bash
  go build -trimpath -ldflags "-s -w -X github.com/vvuri/WebFS/internal/version.Version=v1.0.0" ./cmd/webfs
  ```
- Smoke-test:
  - локальный fixture-сервер;
  - один реальный статический сайт вручную.
- Проверить `go mod tidy`, `gofmt`, `go vet`, `golangci-lint`.

**Критерии приёмки.**
- [ ] README позволяет собрать и запустить.
- [ ] `docs/commands.md` покрывает все команды.
- [ ] CI зелёный на двух ОС.
- [ ] Бинарник собирается с версией.
- [ ] Smoke-test пройден.
- [ ] Все задачи WEBFS-001…WEBFS-040 закрыты.
- [ ] Ограничения описаны.

**Тесты.**
- Полный прогон всех наборов.
- Ручной сценарий из README.

**Запрещено.**
- Пушить/релизить без подтверждения.
- Добавлять внешнюю сеть в CI.
- Коммитить бинарники и секреты.
- Обещать JS-рендеринг, авторизацию, MCP.

---

## 13. Порядок выполнения

```text
WEBFS-001  модуль и каркас
WEBFS-002  линтер и CI
WEBFS-003  конфиг с дефолтами
WEBFS-004  канонизация URL
WEBFS-005  виртуальные пути
WEBFS-006  доменная модель
WEBFS-007  VFS Build/List
WEBFS-008  VFS Resolve/Walk
WEBFS-009  Printer
WEBFS-010  токенизатор
WEBFS-011  реестр и REPL
WEBFS-012  help/clear/exit/version
WEBFS-013  HTTP-клиент
WEBFS-014  лимиты/charset/ошибки
WEBFS-015  парсер: title/links
WEBFS-016  парсер: assets
WEBFS-017  кэш
WEBFS-018  Service.Load
WEBFS-019  Service.Open/Refresh
WEBFS-020  session и open
WEBFS-021  ls
WEBFS-022  cd
WEBFS-023  pwd/back
WEBFS-024  tree
WEBFS-025  crawler BFS
WEBFS-026  robots.txt
WEBFS-027  crawl-команда
WEBFS-028  find
WEBFS-029  grep
WEBFS-030  cache list/clear
WEBFS-031  refresh
WEBFS-032  параллельный crawl
WEBFS-033  отмена и прогресс
WEBFS-034  цвет
WEBFS-035  stats/check
WEBFS-036  export JSON/DOT
WEBFS-037  webfs.json
WEBFS-038  integration-тесты
WEBFS-039  E2E-харнесс
WEBFS-040  E2E-сценарии
WEBFS-041  README/CI/релиз
```

Правила:

- Одна задача — один логический коммит.
- Перед коммитом: `gofmt`, `go test`, `go vet`, `golangci-lint`.
- Не начинать задачу, пока не закрыты её зависимости.
- Если в задаче не хватает контекста — это дефект плана: сначала дополнить задачу, потом писать код.
