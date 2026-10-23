using WebApp.Application.Services;

namespace WebApp.Api.Billing;

/// <summary>
/// Store billing checks (025), every hour: issue the commission invoices of past months, suspend stores with something
/// overdue and reactivate the ones that paid. Each run is idempotent, so a missed run — the free host sleeps when
/// idle — is caught up by the next one. Off with <c>Billing:JobEnabled=false</c> (the integration tests run it per store).
/// </summary>
internal sealed partial class BillingJob(IServiceScopeFactory scopes, IConfiguration config, ILogger<BillingJob> logger) : BackgroundService
{
    private static readonly TimeSpan Every = TimeSpan.FromHours(1);
    /// <summary>Lets the startup migrations and the demo seed finish first.</summary>
    private static readonly TimeSpan FirstRunAfter = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.GetValue("Billing:JobEnabled", true)) return;
        try
        {
            await Task.Delay(FirstRunAfter, stoppingToken);
            using var timer = new PeriodicTimer(Every);
            do
            {
                await RunOnceAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<BillingService>().RunAsync(ct);
            if (result.InvoicesIssued + result.StoresSuspended + result.StoresReactivated > 0)
                LogRun(result.InvoicesIssued, result.StoresSuspended, result.StoresReactivated);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A failed run must not stop the next ones.
            LogFailed(ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Billing: {Invoices} invoice(s) issued, {Suspended} store(s) suspended, {Reactivated} reactivated")]
    private partial void LogRun(int invoices, int suspended, int reactivated);

    [LoggerMessage(Level = LogLevel.Error, Message = "Billing run failed")]
    private partial void LogFailed(Exception ex);
}
