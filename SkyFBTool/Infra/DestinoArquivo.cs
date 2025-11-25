using System.Text;
using SkyFBTool.Core;

namespace SkyFBTool.Infra;

public class DestinoArquivo : IDestinoArquivo
{
    private readonly StreamWriter _escritor;

    public DestinoArquivo(string caminhoArquivo)
    {
        _escritor = new StreamWriter(
            new FileStream(caminhoArquivo, FileMode.Create, FileAccess.Write, FileShare.None),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
        );
    }

    public Task EscreverLinhaAsync(string linha) =>
        _escritor.WriteLineAsync(linha);

    public ValueTask DisposeAsync() =>
        _escritor.DisposeAsync();
}