using System.Reflection;
using SkeletonKey.Analysis;
using SkeletonKey.Analysis.Default;
using SkeletonKey.BuiltIns.Runtime;
using SkeletonKey.Catalog;
using SkeletonKey.Evaluation;
using SkeletonKey.Execution;
using SkeletonKey.Handlers;
using SkeletonKey.Materialization;
using SkeletonKey.Planning;
using SkeletonKey.Planning.Default;
using SkeletonKey.Runtime;
using SkeletonKey.Runtime.Default;

namespace SkeletonKey.ContractSafety.Tests;

/// <summary>
/// Verifies public contract surfaces avoid runtime and host-specific dependencies.
/// </summary>
public sealed class PublicContractSafetyTests
{
    private static readonly Assembly[] _contractAssemblies =
    [
        typeof(WorkflowNodeDefinition).Assembly,
        typeof(WorkflowAnalysisResult).Assembly,
        typeof(WorkflowExecutionPlan).Assembly,
        typeof(NodeExecutionIdentity).Assembly,
        typeof(INodeHandler).Assembly,
        typeof(WorkflowValueResolutionContext).Assembly,
        typeof(IWorkflowValueMaterializer).Assembly,
        typeof(IWorkflowRuntime).Assembly,
        typeof(DefaultWorkflowAnalyzer).Assembly,
        typeof(DefaultWorkflowExecutionPlanner).Assembly,
    ];

    /// <summary>
    /// Verifies public contracts do not expose mutable collections, delegates, mutable runtimes, or host-specific types.
    /// </summary>
    [Fact]
    public void PublicContractsDoNotExposeProhibitedDependencyCategories()
    {
        foreach (Assembly assembly in _contractAssemblies)
        {
            foreach (Type type in assembly.ExportedTypes.Where(static exported => exported.Namespace?.StartsWith("SkeletonKey.", StringComparison.Ordinal) == true))
            {
                foreach (MemberInfo member in PublicMembers(type))
                {
                    Type? memberType = GetMemberType(member);
                    if (memberType is null)
                    {
                        continue;
                    }

                    Assert.False(IsProhibited(memberType), $"{type.FullName}.{member.Name} exposes prohibited type {memberType.FullName}.");
                }
            }
        }
    }

    /// <summary>
    /// Verifies expression evaluation exposes no arbitrary function registration API.
    /// </summary>
    [Fact]
    public void EvaluationContractsExposeNoFunctionRegistrationApi()
    {
        Type[] evaluationTypes = typeof(WorkflowExpressionEvaluator).Assembly.ExportedTypes.ToArray();

        Assert.DoesNotContain(evaluationTypes.SelectMany(PublicMembers), member => member.Name.Contains("Register", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(evaluationTypes.SelectMany(PublicMembers), member => member.Name.Contains("FunctionRegistry", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Verifies default analyzer and planner implementations expose no service-container, handler, or runtime registration API.
    /// </summary>
    [Fact]
    public void DefaultAnalysisAndPlanningExposeNoRuntimeRegistrationApi()
    {
        Type[] implementationTypes =
        [
            .. typeof(DefaultWorkflowAnalyzer).Assembly.ExportedTypes,
            .. typeof(DefaultWorkflowExecutionPlanner).Assembly.ExportedTypes,
        ];

        Assert.DoesNotContain(implementationTypes.SelectMany(PublicMembers), member => member.Name.Contains("Register", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(implementationTypes.SelectMany(PublicMembers), member => member.Name.Contains("ServiceProvider", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(implementationTypes.SelectMany(PublicMembers), member => member.Name.Contains("Handler", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Verifies runtime production assemblies avoid browser, dependency-injection, plugin, transport, AI, and legacy dependencies.
    /// </summary>
    [Fact]
    public void RuntimeProductionProjectsAvoidProhibitedDependencies()
    {
        Assembly[] runtimeAssemblies =
        [
            typeof(IWorkflowRuntime).Assembly,
            typeof(DefaultWorkflowRuntime).Assembly,
            typeof(CoreStartHandler).Assembly,
        ];

        string[] prohibited =
        [
            "Playwright",
            "Selenium",
            "Puppeteer",
            "FlaUI",
            "AspNetCore",
            "DependencyInjection",
            "OpenAI",
            "Azure.AI",
            "Backend",
            "Transport",
            "Plugin",
            "LegacyPython",
        ];

        foreach (Assembly assembly in runtimeAssemblies)
        {
            string[] referenced = assembly.GetReferencedAssemblies().Select(static name => name.Name ?? string.Empty).ToArray();
            foreach (string term in prohibited)
            {
                Assert.DoesNotContain(referenced, name => name.Contains(term, StringComparison.OrdinalIgnoreCase));
            }
        }

        Type[] exported = runtimeAssemblies.SelectMany(static assembly => assembly.ExportedTypes).ToArray();
        Assert.DoesNotContain(exported.SelectMany(PublicMembers), member => member.Name.Contains("Register", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(exported.SelectMany(PublicMembers), member => member.Name.Contains("ServiceProvider", StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<MemberInfo> PublicMembers(Type type)
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly;
        return type.GetProperties(Flags).Cast<MemberInfo>().Concat(type.GetMethods(Flags).Where(static method => !method.IsSpecialName));
    }

    private static Type? GetMemberType(MemberInfo member)
    {
        return member switch
        {
            PropertyInfo property => property.PropertyType,
            MethodInfo method => method.ReturnType == typeof(void) ? null : method.ReturnType,
            _ => null,
        };
    }

    private static bool IsProhibited(Type type)
    {
        Type inspected = Nullable.GetUnderlyingType(type) ?? type;
        if (typeof(Delegate).IsAssignableFrom(inspected) ||
            inspected == typeof(IServiceProvider) ||
            inspected == typeof(System.Collections.IList) ||
            inspected == typeof(System.Collections.IDictionary))
        {
            return true;
        }

        if (inspected.IsGenericType)
        {
            Type generic = inspected.GetGenericTypeDefinition();
            if (generic == typeof(List<>) ||
                generic == typeof(Dictionary<,>) ||
                generic == typeof(IList<>) ||
                generic == typeof(IDictionary<,>))
            {
                return true;
            }

            return inspected.GetGenericArguments().Any(IsProhibited);
        }

        string fullName = inspected.FullName ?? string.Empty;
        string assemblyName = inspected.Assembly.GetName().Name ?? string.Empty;
        return fullName.Contains("Playwright", StringComparison.OrdinalIgnoreCase) ||
            fullName.Contains("Selenium", StringComparison.OrdinalIgnoreCase) ||
            fullName.Contains("Puppeteer", StringComparison.OrdinalIgnoreCase) ||
            fullName.Contains("FlaUI", StringComparison.OrdinalIgnoreCase) ||
            fullName.Contains("AspNetCore", StringComparison.OrdinalIgnoreCase) ||
            fullName.Contains("IWorkflowInteractionHandler", StringComparison.Ordinal) ||
            fullName.Contains("Microsoft.Extensions.DependencyInjection", StringComparison.OrdinalIgnoreCase) ||
            fullName.Contains("FileSystem", StringComparison.OrdinalIgnoreCase) ||
            fullName.Contains("HttpClient", StringComparison.OrdinalIgnoreCase) ||
            fullName.Contains("Random", StringComparison.OrdinalIgnoreCase) ||
            fullName.Contains("Backend", StringComparison.OrdinalIgnoreCase) ||
            fullName.Contains("Transport", StringComparison.OrdinalIgnoreCase) ||
            fullName.Contains("LegacyPython", StringComparison.OrdinalIgnoreCase) ||
            assemblyName.Contains("Playwright", StringComparison.OrdinalIgnoreCase) ||
            assemblyName.Contains("Selenium", StringComparison.OrdinalIgnoreCase) ||
            assemblyName.Contains("Puppeteer", StringComparison.OrdinalIgnoreCase) ||
            assemblyName.Contains("FlaUI", StringComparison.OrdinalIgnoreCase) ||
            assemblyName.Contains("AspNetCore", StringComparison.OrdinalIgnoreCase) ||
            assemblyName.Contains("Backend", StringComparison.OrdinalIgnoreCase) ||
            assemblyName.Contains("Transport", StringComparison.OrdinalIgnoreCase);
    }
}
