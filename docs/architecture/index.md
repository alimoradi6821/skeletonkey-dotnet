# Architecture Decision Records

SkeletonKey keeps architecturally significant decisions in `docs/architecture` as Markdown Architecture Decision Records (ADRs).

The repository already contains the historical ADR series beginning at 0004. Those accepted records stay at their existing paths so historical links remain stable. New decisions continue the same numeric sequence and use the maintained [ADR template](adr-template.md), which is based on the MADR style while preserving SkeletonKey's existing directory and numbering convention.

## Rules

- Use the next four-digit number: `NNNN-short-title.md`.
- One ADR records one architecturally significant decision.
- Start with `Proposed`; move to `Accepted`, `Rejected`, `Deprecated`, or `Superseded` when appropriate.
- Do not rewrite the rationale of an accepted decision after the fact. Add a new ADR and mark the old one superseded when the architecture changes.
- Link specifications, tests, and implementation locations that confirm the decision when practical.
- Never include secrets or machine-specific credentials in an ADR.

## Create an ADR

```powershell
Copy-Item .\docs\architecture\adr-template.md .\docs\architecture\0030-short-title.md
```

Then fill in the context, drivers, considered options, decision outcome, consequences, and confirmation.

ADR 0028 records the decision to use DocFX for the documentation site and the MADR-style template for future decisions.

ADR 0029 records the accepted sealed standalone workflow application export decision: one normal SkeletonKey workflow plus host-owned execution settings are packaged into one scenario-specific executable without moving scheduling semantics into `WorkflowDocument`.
