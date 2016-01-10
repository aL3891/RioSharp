namespace RioSharp.Aspnet.Host
{
    internal interface IRioSharpServerInformation
    {
        int Connections { get; set; }
        int PipeLineDepth { get; set; }
    }
}