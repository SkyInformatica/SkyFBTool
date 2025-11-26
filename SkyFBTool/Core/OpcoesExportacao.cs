namespace SkyFBTool.Core;

public class OpcoesExportacao
{
    public string Host { get; set; } = "localhost";
    public int Porta { get; set; } = 3050;
    public string Database { get; set; } = string.Empty;
    public string Usuario { get; set; } = "sysdba";
    public string Senha { get; set; } = "masterkey";

    public string Tabela { get; set; } = string.Empty;
    public string? AliasTabela { get; set; }       // --alias
    public string ArquivoSaida { get; set; } = string.Empty;

    public string? Charset { get; set; }  // usado no SET NAMES do arquivo
    public string? CondicaoWhere { get; set; }     // --where

    public FormatoBlob FormatoBlob { get; set; } = FormatoBlob.Hex;
    public bool ForcarWin1252 { get; set; }        // --force-win1252
    public bool SanitizarTexto { get; set; }       // --sanitize-text
    public bool EscaparQuebrasDeLinha { get; set; }// --escape-newlines

    public int CommitACada { get; set; } = 0;     // --commit-every
    public bool ContinuarEmCasoDeErro { get; set; }   // --continue-on-error
    public int ProgressoACada { get; set; } = 10000;   // --progresso-cada
}
