using System;
using System.ServiceModel;

[ServiceContract]
public interface ITermPTYService
{
    [OperationContract]
    void StartCmd(Guid instanceId, string command, int width, int height);

    [OperationContract]
    void Start(Guid instanceId);

    [OperationContract]
    void WriteInput(Guid instanceId, string data);
    
    [OperationContract]
    void Resize(Guid instanceId, int width, int height);
    
    [OperationContract]
    void Close(Guid instanceId);
    
    [OperationContract]
    Guid CreateInstance();
}

[ServiceContract(CallbackContract = typeof(ITermPTYCallback))]
public interface ITermPTYCallback
{
    [OperationContract(IsOneWay = true)]
    void OnTerminalOutput(Guid instanceId, string output);
    
    [OperationContract(IsOneWay = true)]
    void OnTermReady(Guid instanceId);
}
