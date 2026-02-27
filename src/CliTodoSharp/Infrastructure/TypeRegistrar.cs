using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace CliTodoSharp.Infrastructure;

/// <summary>
/// Bridges Microsoft.Extensions.DependencyInjection with the Spectre.Console.Cli
/// command resolution pipeline.
///
/// How it works:
///   1. Program.cs builds a normal IServiceCollection and registers all services.
///   2. TypeRegistrar wraps that collection and is passed to CommandApp.
///   3. When Spectre needs to instantiate a command class, it calls
///      TypeResolver.Resolve(type), which delegates to the built ServiceProvider.
///
/// This pattern is recommended in the official Spectre.Console documentation and
/// lets commands use standard constructor-injection without any framework glue code.
/// </summary>
public sealed class TypeRegistrar(IServiceCollection services) : ITypeRegistrar
{
    private ITypeResolver? _resolver;

    /// <summary>
    /// Called by Spectre when it needs to register additional types discovered
    /// from command attributes (e.g. Settings classes).
    /// </summary>
    public void Register(Type service, Type implementation)
        => services.AddSingleton(service, implementation);

    /// <summary>
    /// Called by Spectre when it has an already-constructed instance to register
    /// (e.g. the default Settings instance).
    /// </summary>
    public void RegisterInstance(Type service, object implementation)
        => services.AddSingleton(service, implementation);

    /// <summary>
    /// Called by Spectre when it needs a lazy-initialised registration
    /// (factory pattern).
    /// </summary>
    public void RegisterLazy(Type service, Func<object> factory)
        => services.AddSingleton(service, _ => factory());

    /// <summary>
    /// Finalise the DI container and return the resolver.
    /// Spectre calls this once, just before the first command is resolved.
    /// </summary>
    public ITypeResolver Build()
        => _resolver ??= new TypeResolver(services.BuildServiceProvider());
}

/// <summary>
/// Thin wrapper around <see cref="ServiceProvider"/> that satisfies
/// Spectre's <see cref="ITypeResolver"/> contract.
/// </summary>
public sealed class TypeResolver(ServiceProvider provider) : ITypeResolver, IDisposable
{
    public object? Resolve(Type? type)
        => type is null ? null : provider.GetService(type);

    public void Dispose()
        => provider.Dispose();
}
