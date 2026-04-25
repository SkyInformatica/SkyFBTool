namespace SkyFBTool.Core;

public class OpcoesDdlAnalise
{
    public string Entrada { get; set; } = string.Empty;
    public string? Saida { get; set; }
    public List<string> PrefixosTabelaIgnorados { get; set; } = [];
    public string? ArquivoConfiguracaoSeveridade { get; set; }
}
