SkeletonKey Real Pilot 01 - Fixed

Reason for the previous failure:
The pilot incorrectly included a core.end node.
In the current SkeletonKey runtime contract, core.end is catalogued/valid structurally
but has no exact runtime handler. The workflow should terminate naturally after the
last executable node.

Place this folder directly inside the SkeletonKey repository root.

Run:
powershell -NoProfile -ExecutionPolicy Bypass -File .\skeletonkey-real-pilot-01-fixed\run.ps1

Expected:
1/4 VALIDATE -> valid
2/4 ANALYZE  -> ready, 8 nodes, 7 connections
3/4 PLAN     -> ready, 8 steps, 7 dependencies
4/4 REAL RUN -> Succeeded

Expected outputs:
initialHeading = Example Domain
linkHref = https://iana.org/domains/example
destinationHeading = Example Domains
