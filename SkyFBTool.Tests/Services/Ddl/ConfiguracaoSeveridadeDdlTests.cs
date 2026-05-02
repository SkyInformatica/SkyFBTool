using SkyFBTool.Services.Ddl;
using SkyFBTool.Core;
using Xunit;

namespace SkyFBTool.Tests.Services.Ddl;

public class ConfiguracaoSeveridadeDdlTests
{
    [Fact]
    public async Task CarregarAsync_DeveLerOverridesValidos()
    {
        string pasta = CriarPastaTemporaria();
        string arquivo = Path.Combine(pasta, "sev.json");
        string json = """
                      {
                        "overrides": [
                          { "code": "TABLE_WITHOUT_PK", "severity": "critical" },
                          { "code": "DUPLICATED_INDEX", "severity": "medium" }
                        ]
                      }
                      """;
        await File.WriteAllTextAsync(arquivo, json);

        var overrides = await ConfiguracaoSeveridadeDdl.CarregarAsync(arquivo);

        Assert.Equal("critical", overrides["TABELA_SEM_PK"]);
        Assert.Equal("medium", overrides["INDICE_DUPLICADO"]);
    }

    [Fact]
    public async Task CarregarAsync_ComSeveridadeInvalida_DeveFalhar()
    {
        string pasta = CriarPastaTemporaria();
        string arquivo = Path.Combine(pasta, "sev_invalido.json");
        string json = """
                      {
                        "overrides": [
                          { "code": "TABLE_WITHOUT_PK", "severity": "urgent" }
                        ]
                      }
                      """;
        await File.WriteAllTextAsync(arquivo, json);

        await Assert.ThrowsAsync<ArgumentException>(() => ConfiguracaoSeveridadeDdl.CarregarAsync(arquivo));
    }

    [Fact]
    public async Task CarregarAsync_ComCodeInternoEmPortugues_DeveFalhar()
    {
        string pasta = CriarPastaTemporaria();
        string arquivo = Path.Combine(pasta, "code_portugues.json");
        string json = """
                      {
                        "overrides": [
                          { "code": "TABELA_SEM_PK", "severity": "critical" }
                        ]
                      }
                      """;
        await File.WriteAllTextAsync(arquivo, json);

        await Assert.ThrowsAsync<ArgumentException>(() => ConfiguracaoSeveridadeDdl.CarregarAsync(arquivo));
    }

    [Fact]
    public async Task CarregarAsync_ComFormatoLegadoCodigoSeveridade_DeveFalhar()
    {
        string pasta = CriarPastaTemporaria();
        string arquivo = Path.Combine(pasta, "formato_legado.json");
        string json = """
                      {
                        "overrides": [
                          { "codigo": "TABLE_WITHOUT_PK", "severidade": "critical" }
                        ]
                      }
                      """;
        await File.WriteAllTextAsync(arquivo, json);

        await Assert.ThrowsAsync<ArgumentException>(() => ConfiguracaoSeveridadeDdl.CarregarAsync(arquivo));
    }

    private static string CriarPastaTemporaria()
    {
        string pasta = Path.Combine(
            Path.GetTempPath(),
            "SkyFBTool.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);
        return pasta;
    }
}
