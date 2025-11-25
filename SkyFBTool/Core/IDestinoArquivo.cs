namespace SkyFBTool.Core;

public interface IDestinoArquivo : IAsyncDisposable
{
    Task EscreverLinhaAsync(string linha);
}