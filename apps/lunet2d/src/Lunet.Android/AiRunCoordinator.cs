using Android.Content;
using Lunet.Ai;
using Lunet.Core;

namespace Lunet.Android;

/// <summary>In-process job owned by the foreground service, never by an Activity or View.</summary>
internal sealed class LunetAiWork
{
    private readonly object _gate = new();
    private readonly CancellationTokenSource _cancel = new();
    private readonly Dictionary<string, LunetAiStep> _steps = new(StringComparer.Ordinal);
    private readonly LunetAiRequest _request;
    private Task? _task;
    private bool _running = true;
    private LunetAiReply? _reply;
    private string? _error;

    public LunetAiWork(LunetProject project, LunetAiSession session, string prompt, LunetAiRequest request)
    {
        ProjectId = project.Manifest.GameId;
        ProjectName = project.Name;
        Session = session;
        Prompt = prompt;
        _request = request;
    }

    public string ProjectId { get; }
    public string ProjectName { get; }
    public string Prompt { get; }
    public LunetAiSession Session { get; }
    public LunetAiRequest Request => _request;
    public bool IsRunning { get { lock (_gate) return _running; } }
    public LunetAiReply? Reply { get { lock (_gate) return _reply; } }
    public string? Error { get { lock (_gate) return _error; } }
    public IReadOnlyList<LunetAiStep> Steps { get { lock (_gate) return _steps.Values.ToArray(); } }

    public void Cancel() => _cancel.Cancel();

    internal Task Start()
    {
        lock (_gate) return _task ??= Task.Run(ExecuteAsync);
    }

    internal void FailBeforeStart(string message)
    {
        lock (_gate)
        {
            _error = message;
            _running = false;
        }
        LunetAiRuns.Signal();
    }

    private async Task ExecuteAsync()
    {
        try
        {
            var result = await Session.AskAsync(Prompt, _request with { Steps = new StepObserver(this) },
                _cancel.Token);
            lock (_gate) _reply = result;
        }
        catch (OperationCanceledException)
        {
            lock (_gate) _error = "Execução cancelada. Alterações já gravadas continuam no projeto.";
        }
        catch (Exception ex)
        {
            lock (_gate) _error = ex is HttpRequestException or TaskCanceledException
                ? "Não foi possível concluir a execução por falha de rede ou tempo limite."
                : "A IA falhou: " + ex.Message;
        }
        finally
        {
            lock (_gate) _running = false;
            LunetAiRuns.Signal();
        }
    }

    private sealed class StepObserver(LunetAiWork work) : IProgress<LunetAiStep>
    {
        public void Report(LunetAiStep step)
        {
            lock (work._gate) work._steps[step.Id] = step;
            LunetAiRuns.Signal();
        }
    }
}

/// <summary>Only one mutating agent at once; keeps running when the user changes screens/projects.</summary>
internal static class LunetAiRuns
{
    private static readonly object Gate = new();
    private static LunetAiWork? _current;
    public static event Action? Changed;

    public static LunetAiWork? Current { get { lock (Gate) return _current; } }

    public static bool TryStart(Context context, LunetAiWork work, out string? failure)
    {
        failure = null;
        lock (Gate)
        {
            if (_current is { IsRunning: true })
            {
                failure = "A IA já está executando uma tarefa.";
                return false;
            }
            _current = work;
        }
        try
        {
            // User-initiated while the Activity is visible. Service promotes itself immediately in OnStartCommand.
            context.StartForegroundService(new Intent(context, typeof(LunetAiForegroundService)));
        }
        catch (Exception ex) when (ex is Java.Lang.Exception or InvalidOperationException)
        {
            lock (Gate) if (ReferenceEquals(_current, work)) _current = null;
            failure = "O Android impediu a execução em segundo plano: " + ex.Message;
            return false;
        }
        Signal();
        return true;
    }

    internal static Task? RunPending() => Current?.Start();

    internal static void Signal()
    {
        try { Changed?.Invoke(); }
        catch (Exception) { /* A UI is optional; agent work must not depend on its observers. */ }
    }
}
