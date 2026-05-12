namespace SkyFBTool.Core;

public sealed class OpcoesCriacaoBanco
{
    public string Database { get; set; } = string.Empty;
    public string Host { get; set; } = "localhost";
    public int Porta { get; set; } = 3050;
    public string Usuario { get; set; } = "sysdba";
    public string Senha { get; set; } = "masterkey";
    public string Charset { get; set; } = "UTF8";
    public int PageSize { get; set; } = 8192;
    public bool ForcedWrites { get; set; } = true;
    public bool Overwrite { get; set; }
    public string? ArquivoDdl { get; set; }
}
