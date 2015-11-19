namespace RioSharp.Aspnet.Host
{
    internal interface IRioSharpServerInformation
    {
        int Connections { get; set; }
        uint PipeLineDepth { get; set; }
    }
}