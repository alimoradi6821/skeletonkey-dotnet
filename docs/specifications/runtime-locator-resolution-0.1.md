# Runtime Locator Resolution 0.1

Locator resolution loads an exact locator catalog ID and exact version through an explicit `ILocatorDocumentRepository`. No latest fallback, network lookup, filesystem lookup, or global registry is used.

`LocatorPlanResolver` resolves the requested locator, resolves `within` parents, detects cycles, preserves outer-to-inner scope order, preserves strategy order, and returns a browser-free `ResolvedLocatorPlan`.
