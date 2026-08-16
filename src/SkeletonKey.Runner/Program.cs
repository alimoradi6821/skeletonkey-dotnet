using SkeletonKey.Runner.Core;

return await new SkeletonKeyRunner(Console.In, Console.Out, Console.Error).ExecuteAsync(args).ConfigureAwait(false);
