namespace ShiftPlanner.Web.Infrastructure;

/// <summary>
/// Blazor Server 的 circuit 可能存活很久；若整個 circuit 共用同一個 DbContext，
/// 一次失敗的操作留下的半套變更會在下一次存檔時被寫入。因此每個畫面操作都在新的 scope 中執行。
/// </summary>
public sealed class ScopedRunner(IServiceScopeFactory scopes)
{
    public async Task<T> RunAsync<TService, T>(Func<TService, Task<T>> action)
        where TService : notnull
    {
        await using var scope = scopes.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<TService>());
    }

    public async Task RunAsync<TService>(Func<TService, Task> action)
        where TService : notnull
    {
        await using var scope = scopes.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<TService>());
    }
}
