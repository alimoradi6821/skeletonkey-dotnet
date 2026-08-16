# Web Navigation Policy 0.1

`IWebNavigationPolicy` validates navigation before provider execution. `DefaultWebNavigationPolicy` allows `http`, `https`, `data`, and `about`, and rejects `javascript` and `file`.

Hosts may provide stricter network policies. SkeletonKey does not implement universal internal-network blocking heuristics in Phase 0-15.
