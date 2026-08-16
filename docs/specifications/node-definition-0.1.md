# Node Definition 0.1

Node definitions are immutable catalog contracts. They describe one exact node type and type version, including ports, parameter schema metadata, dynamic port rules, resource slots, capabilities, behavior metadata, stability, and deprecation metadata.

A node definition is not a node handler. It contains no executable delegate, runtime callback, dependency-injection service, host object, browser object, or transport object.

Handlers bind to node definitions through exact `WorkflowNodeDefinitionKey` identity. There is no implicit latest-version selection.

Catalog validation may validate node definition metadata. A future runtime validates handler inputs and outputs against the exact node definition before and after handler execution.

No handler implementation, runtime implementation, plugin discovery, assembly scanning, or mutable handler registration is defined by this document.
