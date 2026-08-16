# Node Ports 0.1

Catalog port definitions declare ordinal, case-sensitive port IDs, direction, required state, multiplicity, optional value hints, optional schema fragments, descriptions, and role identifiers.

The default role is `control`. Catalogs may declare `data` or multi-role ports such as `control` plus `data` when a port can both advance graph flow and expose readable output metadata.

Catalog-aware analysis resolves effective ports by combining static catalog ports with deterministic dynamic ports. Duplicate effective port IDs are errors. Multiplicity is interpreted as a static connection constraint; a non-multiple input may receive only one compatible incoming connection.

Port names alone do not define compatibility. Compatibility requires at least one shared role between the resolved source output and target input.
