namespace SkyFBTool.Services.Ddl.Rules;

internal static class MotorRegrasAnaliseDdl
{
    public static void Executar(ContextoAnaliseDdl contexto, IEnumerable<IRegraAnaliseDdl> regras)
    {
        foreach (var regra in regras)
            regra.Avaliar(contexto);
    }
}
