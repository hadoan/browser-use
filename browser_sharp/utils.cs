using System;
using System.Diagnostics;
using System.Threading.Tasks;

public class TimeExecutionSyncAttribute : Attribute
{
    private readonly string _additionalText;

    public TimeExecutionSyncAttribute(string additionalText = "")
    {
        _additionalText = additionalText;
    }

    public T Execute<T>(Func<T> func)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = func();
        stopwatch.Stop();
        Console.WriteLine($"{_additionalText} Execution time: {stopwatch.ElapsedMilliseconds} ms");
        return result;
    }
}

public class TimeExecutionAsyncAttribute : Attribute
{
    private readonly string _additionalText;

    public TimeExecutionAsyncAttribute(string additionalText = "")
    {
        _additionalText = additionalText;
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> func)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = await func();
        stopwatch.Stop();
        Console.WriteLine($"{_additionalText} Execution time: {stopwatch.ElapsedMilliseconds} ms");
        return result;
    }
}

// Generic Singleton Implementation
public sealed class Singleton<T> where T : class, new()
{
    private static readonly Lazy<T> _instance = new(() => new T());

    public static T Instance => _instance.Value;
}

// Usage example
public class TestClass
{
    private readonly TimeExecutionSyncAttribute _syncLogger = new("Sync Test");
    private readonly TimeExecutionAsyncAttribute _asyncLogger = new("Async Test");

    public void RunSync()
    {
        _syncLogger.Execute(() =>
        {
            System.Threading.Thread.Sleep(500); // Simulating work
            return "Completed Sync";
        });
    }

    public async Task RunAsync()
    {
        await _asyncLogger.ExecuteAsync(async () =>
        {
            await Task.Delay(500); // Simulating async work
            return "Completed Async";
        });
    }
}

class Program
{
    static async Task Main()
    {
        var test = new TestClass();
        test.RunSync();
        await test.RunAsync();

        var singletonInstance = Singleton<TestClass>.Instance;
    }
}
