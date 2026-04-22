namespace SkyFBTool.Core;

public class OpcoesDdlExtracao
{
    public string Host { get; set; } = "localhost";
    public int Porta { get; set; } = 3050;
    public string Database { get; set; } = string.Empty;
    public string Usuario { get; set; } = "sysdba";
    public string Senha { get; set; } = "masterkey";
    public string? Charset { get; set; }
    public string? Saida { get; set; }
}
