# Node Locator Slots 0.1

`NodeLocatorSlotDefinition` declares a case-sensitive slot name, RFC 6901 parameter pointer, required flag, usage mode, accepted cardinalities, and optional description.

Only catalog-declared locator slots may consume `$locator` wrappers. Parameter pointers may traverse object properties and bounded array elements, for example `/fields/0/locator`. Empty path segments are rejected by semantic validation.
