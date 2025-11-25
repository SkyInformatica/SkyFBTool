namespace SkyFBTool.Core;

public class OpcoesExportacao
{
    public string Host { get; set; } = "localhost";
    public int Porta { get; set; } = 3050;
    public string Database { get; set; } = "";
    public string Usuario { get; set; } = "sysdba";
    public string Senha { get; set; } = "masterkey";
    public string Tabela { get; set; } = "";
    public string? CondicaoWhere { get; set; }
    public FormatoBlob FormatoBlob { get; set; } = FormatoBlob.Hex;
    public string ArquivoSaida { get; set; } = "dump.sql";
    public int ProgressoACada { get; set; } = 1000;
    public string Charset { get; set; } = "NONE";
    public bool ForcarWin1252 { get; set; } = false;
    public bool ContinuarEmCasoDeErro { get; set; } = false;
    public bool SanitizarTexto { get; set; } = false;
    public bool EscaparQuebrasDeLinha { get; set; } = false;
    public int CommitACada { get; set; } = 50000; 
    public string? AliasTabela { get; set; } = null;

}