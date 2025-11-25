namespace SkyFBTool.Core;

public class OpcoesImportacao
{
    public string Host { get; set; } = "localhost";
    public int Porta { get; set; } = 3050;

    public string Database { get; set; } = "";
    public string Usuario { get; set; } = "sysdba";
    public string Senha { get; set; } = "masterkey";

    public string ArquivoEntrada { get; set; } = "";

    // Ex.: 1000 = mostra progresso a cada 1000 linhas
    public int ProgressoACada { get; set; } = 1000;
    
    public bool ContinuarEmCasoDeErro { get; set; } = true;
}