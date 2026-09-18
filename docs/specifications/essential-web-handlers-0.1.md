# Essential Web Handlers 0.1

Phase 0-15 defines version-one handlers for `web.navigate`, `web.click`, `web.fill`, `web.press`, `web.selectOption`, `web.setChecked`, `web.wait`, `web.getText`, `web.getAttribute`, `web.getCount`, and `web.screenshot`.

Phase 3 adds `web.extractCollection`, `web.scroll`, `web.scrollIntoView`, `web.type`, `web.insertText`, `web.clear`, `web.focus`, and `web.waitForCondition`. Collection field locators are scoped relative to each matched parent item; waits and extraction are explicitly bounded.

Handlers acquire the declared `page` resource slot and read declared locator slots from `INodeExecutionContext.Locators`.
