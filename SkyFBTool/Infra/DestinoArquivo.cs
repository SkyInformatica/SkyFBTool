using System.Text;
using SkyFBTool.Core;

namespace SkyFBTool.Infra;

public class DestinoArquivo : IDestinoArquivo
{
    private readonly Encoding _encoding;
    private readonly string _caminhoBase;
    private readonly long _tamanhoMaximoBytes;
    private readonly List<string> _cabecalhoRotacao = new();
    private readonly List<(string Caminho, long TamanhoBytes)> _arquivosGerados = new();

    private StreamWriter _escritor;
    private string _caminhoArquivoAtual;
    private int _indiceParte = 1;
    private long _bytesEscritosArquivoAtual;
    private bool _capturandoCabecalho = true;

    public DestinoArquivo(
        string caminhoArquivo,
        int tamanhoMaximoArquivoMb = 100,
        Encoding? encoding = null)
    {
        if (string.IsNullOrWhiteSpace(caminhoArquivo))
            throw new ArgumentException("Caminho do arquivo de saida nao pode ser vazio.", nameof(caminhoArquivo));

        _encoding = encoding ?? new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        _caminhoBase = Path.GetFullPath(caminhoArquivo);
        _tamanhoMaximoBytes = tamanhoMaximoArquivoMb <= 0
            ? 0
            : tamanhoMaximoArquivoMb * 1024L * 1024L;

        var diretorio = Path.GetDirectoryName(_caminhoBase);
        if (!string.IsNullOrWhiteSpace(diretorio))
            Directory.CreateDirectory(diretorio);

        _caminhoArquivoAtual = MontarCaminhoParte(_indiceParte);
        _escritor = CriarEscritor(_caminhoArquivoAtual);
        _arquivosGerados.Add((_caminhoArquivoAtual, 0));
        Console.WriteLine($"Arquivo de exportacao: {_caminhoArquivoAtual}");
    }

    public async Task EscreverLinhaAsync(string linha)
    {
        AtualizarCabecalho(linha);

        long bytesLinha = _encoding.GetByteCount(linha) + _encoding.GetByteCount(Environment.NewLine);

        if (DeveRotacionar(bytesLinha))
            await RotacionarAsync();

        await EscreverLinhaInternaAsync(linha);
    }

    public async ValueTask DisposeAsync()
    {
        await _escritor.FlushAsync();
        await _escritor.DisposeAsync();
    }

    private bool DeveRotacionar(long bytesLinha)
    {
        if (_tamanhoMaximoBytes <= 0)
            return false;

        if (_bytesEscritosArquivoAtual == 0)
            return false;

        return (_bytesEscritosArquivoAtual + bytesLinha) > _tamanhoMaximoBytes;
    }

    private async Task RotacionarAsync()
    {
        await _escritor.FlushAsync();
        await _escritor.DisposeAsync();

        _indiceParte++;
        _caminhoArquivoAtual = MontarCaminhoParte(_indiceParte);
        _escritor = CriarEscritor(_caminhoArquivoAtual);
        _bytesEscritosArquivoAtual = 0;
        _arquivosGerados.Add((_caminhoArquivoAtual, 0));

        Console.WriteLine($"Arquivo de exportacao (parte {_indiceParte}): {_caminhoArquivoAtual}");

        foreach (var linhaCabecalho in _cabecalhoRotacao)
            await EscreverLinhaInternaAsync(linhaCabecalho);
    }

    private Task EscreverLinhaInternaAsync(string linha)
    {
        long bytesLinha = _encoding.GetByteCount(linha) + _encoding.GetByteCount(Environment.NewLine);
        _bytesEscritosArquivoAtual += bytesLinha;

        var arquivoAtual = _arquivosGerados[^1];
        _arquivosGerados[^1] = (arquivoAtual.Caminho, arquivoAtual.TamanhoBytes + bytesLinha);

        return _escritor.WriteLineAsync(linha);
    }

    public IReadOnlyList<(string Caminho, long TamanhoBytes)> ObterArquivosGerados()
    {
        return _arquivosGerados.ToList();
    }

    private string MontarCaminhoParte(int indiceParte)
    {
        if (indiceParte <= 1)
            return _caminhoBase;

        var diretorio = Path.GetDirectoryName(_caminhoBase) ?? Directory.GetCurrentDirectory();
        var nomeSemExtensao = Path.GetFileNameWithoutExtension(_caminhoBase);
        var extensao = Path.GetExtension(_caminhoBase);
        if (string.IsNullOrWhiteSpace(extensao))
            extensao = ".sql";

        return Path.Combine(diretorio, $"{nomeSemExtensao}_part{indiceParte:000}{extensao}");
    }

    private StreamWriter CriarEscritor(string caminhoArquivo)
    {
        return new StreamWriter(
            new FileStream(caminhoArquivo, FileMode.Create, FileAccess.Write, FileShare.None),
            _encoding
        );
    }

    private void AtualizarCabecalho(string linha)
    {
        if (!_capturandoCabecalho)
            return;

        string texto = linha.TrimStart();

        if (texto.StartsWith("INSERT ", StringComparison.OrdinalIgnoreCase) ||
            texto.StartsWith("UPDATE ", StringComparison.OrdinalIgnoreCase) ||
            texto.StartsWith("DELETE ", StringComparison.OrdinalIgnoreCase) ||
            texto.StartsWith("MERGE ", StringComparison.OrdinalIgnoreCase))
        {
            _capturandoCabecalho = false;
            return;
        }

        _cabecalhoRotacao.Add(linha);
    }
}
