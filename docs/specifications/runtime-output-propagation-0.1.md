# Runtime Output Propagation 0.1

Handlers return lightweight `NodeHandlerOutputs`. The runtime validates every returned output against analyzed effective ports.

Control outputs must name existing output ports with the `control` role. The runtime matches control dependencies by source step and source port, then activates matching target control inputs in deterministic order.

Data outputs must name existing output ports with the `data` role. The runtime preserves explicit JSON null, multi-value order, and statically enforceable multiplicity. Data outputs are stored as `NodePortValueSet` values and projected through existing workflow value-resolution rules for later bindings and expressions.

Unexpected control or data ports are runtime contract violations. The runtime does not silently discard unexpected outputs.
