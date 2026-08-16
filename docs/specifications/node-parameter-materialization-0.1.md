# Node Parameter Materialization 0.1

`NodeParameterMaterializer` materializes node parameter objects for future `NodeExecutionRequest.Parameters`.

The result must be a JSON object or materialization fails. Successful output is plain JSON with `$binding`, `$expression`, and `$literal` processing complete.

`$resource` and `$locator` fail explicitly in generic node parameter materialization. A future specialized runtime preparation phase may handle resource slots and locator-aware provider preparation separately.

The helper does not construct `NodeExecutionRequest`, execute nodes, execute handlers, traverse plans, mutate runtime state, resolve resources, resolve locators, launch browsers, or dispatch events.

Source parameters are never mutated and result JSON is defensively owned.
