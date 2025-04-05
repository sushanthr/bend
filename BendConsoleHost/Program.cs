using Console;
using System.Collections.Concurrent;
using System.ServiceModel;
using System;
using System.Diagnostics;

public class TermPTYServer : ITermPTYService
{
    private readonly ConcurrentDictionary<Guid, TermPTY> _instances = new ConcurrentDictionary<Guid, TermPTY>();

    public Guid CreateInstance()
    {
        var id = Guid.NewGuid();
        var callback = OperationContext.Current.GetCallbackChannel<ITermPTYCallback>();

        var term = new TermPTY();
        term.TerminalOutput += (s, e) => callback.OnTerminalOutput(id, e.Data);
        term.TermReady += (s, e) => callback.OnTermReady(id);

        _instances[id] = term;
        return id;
    }

    public void Start(Guid instanceId)
    {
        if (_instances.TryGetValue(instanceId, out var term))
            term.Start();
    }

    public void StartCmd(Guid instanceId, string command, int width, int height)
    {
        if (_instances.TryGetValue(instanceId, out var term))
            term.Start(command, width, height);
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
        }
    }
}

class Program
{
    static void Main()
    {
        using (var host = new ServiceHost(typeof(TermPTYServer)))
        {
            host.AddServiceEndpoint(typeof(ITermPTYService),
                new NetNamedPipeBinding() { MaxReceivedMessageSize = 1024 * 1024 },
                "net.pipe://localhost/TermPTYService");

            host.Open();

            // Wait for parent process
            var parentProcess = Process.GetProcessById(int.Parse(Environment.GetCommandLineArgs()[1]));
            parentProcess.WaitForExit();
        }
    }
}