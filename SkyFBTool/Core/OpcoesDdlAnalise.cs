namespace SkyFBTool.Core;

public class OpcoesDdlAnalise
{
    public string Entrada { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public string DatabasesBatch { get; set; } = string.Empty;
    public string Host { get; set; } = "localhost";
    public int Porta { get; set; } = 3050;
    public string Usuario { get; set; } = "sysdba";
    public string Senha { get; set; } = "masterkey";
    public string? Charset { get; set; }
    public string? Saida { get; set; }
    public List<string> PrefixosTabelaIgnorados { get; set; } = [];
    public string? ArquivoConfiguracaoSeveridade { get; set; }
}
