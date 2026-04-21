namespace SkyFBTool.Core;

public class OpcoesExportacao
{
    public string Host { get; set; } = "localhost";
    public int Porta { get; set; } = 3050;
    public string Database { get; set; } = string.Empty;
    public string Usuario { get; set; } = "sysdba";
    public string Senha { get; set; } = "masterkey";

    public string Tabela { get; set; } = string.Empty;
    public string? AliasTabela { get; set; }
    public string ArquivoSaida { get; set; } = string.Empty;

    public string? Charset { get; set; }
    public string? CondicaoWhere { get; set; }
    public string? ConsultaSqlCompleta { get; set; }

    public FormatoBlob FormatoBlob { get; set; } = FormatoBlob.Hex;
    public bool ForcarWin1252 { get; set; }
    public bool SanitizarTexto { get; set; }
    public bool EscaparQuebrasDeLinha { get; set; }

    public int CommitACada { get; set; } = 0;
    public bool ContinuarEmCasoDeErro { get; set; }
    public int ProgressoACada { get; set; } = 10000;
    public int TamanhoMaximoArquivoMb { get; set; } = 100;
}
