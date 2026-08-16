# Node Catalog 0.1

Node catalogs expose host-neutral node definition metadata.

`WorkflowNodeDefinition` identifies one exact node type and type version. Type and version form the identity; there is no implicit latest-version lookup.

Definitions may declare display metadata, parameter schema fragments, immutable example parameter objects, static input ports, static output ports, dynamic port rules, resource slots, node capabilities, behavior metadata, stability metadata, and deprecation metadata.

`WorkflowPortDefinition` declares a port name, direction, multiplicity hint, optional value type hint, optional schema fragment, description, and deterministic role identifiers used by catalog-aware connection analysis.

`WorkflowDynamicPortRule` declares deterministic dynamic port derivation metadata. Version 0.1 supports switch-case output ports derived from `/cases` item `/id` values.

`WorkflowNodeResourceRequirement` declares a node-local resource slot, required resource kind, required capabilities, and whether the slot is mandatory.

`IWorkflowNodeDefinitionCatalog` resolves exact `(type, version)` pairs and lists known versions for a type.

`WorkflowNodeDefinitionCatalog` is immutable, deterministic, case-sensitive, rejects duplicate exact definitions, preserves catalog enumeration order, and returns known versions ordered by numeric version. It does not select a latest version implicitly.

`SkeletonKey.Catalog.Json` provides strict canonical JSON serialization with LF line endings, UTF-8 without BOM, and exactly one trailing LF.

`SkeletonKey.Catalog.Validation` provides semantic validation for node catalog artifacts. Semantic validation reports duplicate definitions, invalid type and port identities, invalid resource slots, invalid capability IDs, invalid dynamic port rules, and invalid deprecation metadata.

Catalogs are explicit immutable inputs. The default analyzer consumes catalogs for exact node-definition, effective-port, resource-slot, and capability analysis. The contract does not define plugin discovery, package loading, handler registration, runtime dependency injection, assembly scanning, mutable registration APIs, or global registries.

Node definitions are not node handlers. A future handler advertises the exact `WorkflowNodeDefinitionKey` it executes. No implicit latest-version handler selection exists.
