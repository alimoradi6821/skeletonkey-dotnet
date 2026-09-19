# Secrets, Hashing, and Workflow-Visible Time 0.1

## Status

Implemented Phase 4 capability contract.

## Secrets

A workflow may reference a host-owned secret with:

```json
{ "$secret": "service-api-token" }
```

`SkeletonKey.Secrets.Abstractions` defines `IWorkflowSecretProvider`, `WorkflowSecretValue`, and the strict reference reader.

Secret resolution is intentionally outside deterministic static materialization. The default runtime resolves `$secret` recursively immediately before node execution. A `$literal` wrapper prevents interpretation beneath that wrapper.

Stable failures are:

- `SKR1027`: the workflow requires a secret but no provider is configured;
- `SKR1028`: the provider has no value for the requested name;
- `SKR1029`: the provider failed while resolving the value.

The ordinary materializer returns `SKV1023` when asked to materialize a `$secret` directly, because host access is forbidden at that layer.

The runner supplies an environment-backed provider by default. Its default variable name is:

```text
SKELETONKEY_SECRET_<logical-name>
```

The prefix may be replaced with `--secret-env-prefix`. Hosts may inject a completely different provider without changing workflow JSON.

Resolved plaintext is not added by the runtime to events, checkpoints, error messages, or ordinary outputs. A custom handler that deliberately emits or logs a sensitive input remains responsible for its own behavior; the secret contract cannot make arbitrary consumer code safe after plaintext has been handed to it.

Child workflows invoked through `workflow.invoke` receive the same host secret provider.

## Deterministic Hashing

`data.hash` version 1 accepts `value` and currently supports SHA-256.

Canonicalization rules are:

- object properties are written in ordinal key order;
- array order is preserved;
- JSON scalar representation is emitted by `System.Text.Json`;
- the digest is lowercase hexadecimal.

This is SkeletonKey canonical JSON for stable fingerprints and idempotency keys. It is not specified as RFC 8785 and it is not a password-hashing primitive.

Outputs:

```text
continue
hash
algorithm
```

## Workflow-Visible Time

`time.now` version 1 is runtime-owned rather than implemented by a normal handler. It reads the exact `IWorkflowClock` configured for that runtime.

Outputs:

```text
continue
utc
unixTimeMilliseconds
```

`utc` uses the invariant round-trip ISO-8601 format and UTC offset zero.

This design keeps tests deterministic and prevents a second clock source from appearing inside built-in handlers.

## Verification

Phase 4 verification covers:

- strict `$secret` wrapper parsing and materializer rejection outside the runtime boundary;
- successful just-in-time resolution into handler parameters;
- stable missing-provider and missing-secret failures;
- no plaintext retention in framework-generated checkpoint/event serialization;
- secret-provider propagation into invoked child workflows;
- runner environment-provider composition;
- canonical hash equality for reordered object properties and inequality for reordered arrays;
- exact `time.now` output from an injected fixed clock;
- contract-safety checks that the secret abstraction remains provider-neutral and domain-agnostic.
