using Console;
using System.Collections.Concurrent;
using System.ServiceModel;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Globalization;

[ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple)]
public class TermPTYServer : ITermPTYService
{
    private readonly ConcurrentDictionary<Guid, TermPTY> _instances = new ConcurrentDictionary<Guid, TermPTY>();
    private readonly ConcurrentDictionary<Guid, ITermPTYCallback> _callbacks = new ConcurrentDictionary<Guid, ITermPTYCallback>();

    public Guid CreateInstance()
    {
        var id = Guid.NewGuid();
        var callback = OperationContext.Current.GetCallbackChannel<ITermPTYCallback>();

        var term = new TermPTY();
        term.TerminalOutput += (s, e) => TryCallback(() => callback.OnTerminalOutput(id, e.Data), id);
        term.TermReady += (s, e) => TryCallback(() => callback.OnTermReady(id), id);

        _instances[id] = term;
        _callbacks[id] = callback;
        return id;
    }

    public void Start(Guid instanceId)
    {
        if (_instances.TryGetValue(instanceId, out var term))
            term.Start();
    }

    public void StartCmd(Guid instanceId, string command, int width, int height, string workingDirectory)
    {
        if (_instances.TryGetValue(instanceId, out var term))
            Task.Run(() =>
            {
                try { term.Start(command, width, height, workingDirectory: workingDirectory); }
                catch (Exception exception) { Debug.WriteLine(exception); }
                finally
                {
                    if (_callbacks.TryRemove(instanceId, out var callback))
                        TryCallback(() => callback.OnTermExited(instanceId), instanceId);
                    _instances.TryRemove(instanceId, out _);
                }
            });
    }

    public void WriteInput(Guid instanceId, string data)
    {
        if (_instances.TryGetValue(instanceId, out var term))
            term.WriteToTerm(data);
    }

    public void Resize(Guid instanceId, int width, int height)
    {
        if (_instances.TryGetValue(instanceId, out var term))
            term.Resize(width, height);
    }

    public void Close(Guid instanceId)
    {
        if (_instances.TryGetValue(instanceId, out var term))
        {
            term.StopExternalTermOnly();
            _instances.TryRemove(instanceId, out _);
            _callbacks.TryRemove(instanceId, out _);
        }
    }

    private void TryCallback(Action callback, Guid instanceId)
    {
        try { callback(); }
        catch (CommunicationException) { Close(instanceId); }
        catch (TimeoutException) { Close(instanceId); }
    }

}

class Program
{
    static int Main(string[] args)
    {
        if (args.Length != 1 || !int.TryParse(args[0], NumberStyles.None, CultureInfo.InvariantCulture, out int parentProcessId) || parentProcessId <= 0)
            return 2;

        Process parentProcess;
        try { parentProcess = Process.GetProcessById(parentProcessId); }
        catch (ArgumentException) { return 3; }

        using (var host = new ServiceHost(typeof(TermPTYServer)))
        using (parentProcess)
        {
            host.AddServiceEndpoint(typeof(ITermPTYService),
                new NetNamedPipeBinding()
                {
                    MaxReceivedMessageSize = 1024 * 1024,
                    ReceiveTimeout = TimeSpan.MaxValue
                },
                "net.pipe://localhost/Bend/TermPTYService/" + parentProcessId.ToString(CultureInfo.InvariantCulture));

            host.Open();

            // Wait for parent process
            parentProcess.WaitForExit();
        }
        return 0;
    }
}
