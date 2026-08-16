# Playwright Page Provider 0.1

`PlaywrightPageResourceProvider` implements `web.page` using the official `Microsoft.Playwright` package. Playwright types remain inside the provider assembly.

Supported constraints are `engine`, `visibility`, `profile`, `userDataDirectory`, `viewportWidth`, `viewportHeight`, `locale`, `userAgent`, and `defaultTimeoutMilliseconds`.

Persistent profile mode requires an explicit user-data directory. Raw browser launch arguments are not accepted.
