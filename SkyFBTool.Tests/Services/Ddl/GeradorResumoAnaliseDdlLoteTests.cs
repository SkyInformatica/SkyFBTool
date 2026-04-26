using System.Text.Json;
using SkyFBTool.Core;
using SkyFBTool.Services.Ddl;
using Xunit;

namespace SkyFBTool.Tests.Services.Ddl;

public class GeradorResumoAnaliseDdlLoteTests
{
    [Fact]
    public async Task GerarAsync_ComDoisResultados_DeveConsolidarTotais()
    {
        string pastaTemp = Path.Combine(Path.GetTempPath(), "SkyFBTool.Tests.BatchSummary", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pastaTemp);

        try
        {
            string json1 = Path.Combine(pastaTemp, "db1_analysis.json");
            string json2 = Path.Combine(pastaTemp, "db2_analysis.json");
            string html1 = Path.Combine(pastaTemp, "db1_analysis.html");
            string html2 = Path.Combine(pastaTemp, "db2_analysis.html");

            await File.WriteAllTextAsync(json1, JsonSerializer.Serialize(new ResultadoAnaliseDdl
            {
                Origem = @"C:\dados\db1.fdb",
                TotalTabelas = 10,
                TotalAchados = 3,
                TotalCriticos = 1,
                TotalAltos = 1,
                TotalMedios = 1,
                TotalBaixos = 0,
                ResumoPorCodigo =
                [
                    new ItemResumoAnaliseDdl { Chave = "TABELA_SEM_PK", Quantidade = 2, Percentual = 66.67m },
                    new ItemResumoAnaliseDdl { Chave = "FK_SEM_INDICE_COBERTURA", Quantidade = 1, Percentual = 33.33m }
                ]
            }));

            await File.WriteAllTextAsync(json2, JsonSerializer.Serialize(new ResultadoAnaliseDdl
            {
                Origem = @"C:\dados\db2.fdb",
                TotalTabelas = 5,
                TotalAchados = 2,
                TotalCriticos = 0,
                TotalAltos = 1,
                TotalMedios = 0,
                TotalBaixos = 1,
                ResumoPorCodigo =
                [
                    new ItemResumoAnaliseDdl { Chave = "TABELA_SEM_PK", Quantidade = 1, Percentual = 50m },
                    new ItemResumoAnaliseDdl { Chave = "INDICE_DUPLICADO", Quantidade = 1, Percentual = 50m }
                ]
            }));

            await File.WriteAllTextAsync(html1, "<html></html>");
            await File.WriteAllTextAsync(html2, "<html></html>");

            var entradas = new List<EntradaResumoAnaliseDdlLote>
            {
                new() { Banco = @"C:\dados\db1.fdb", ArquivoJson = json1, ArquivoHtml = html1 },
                new() { Banco = @"C:\dados\db2.fdb", ArquivoJson = json2, ArquivoHtml = html2 }
            };

            var (arquivoResumoJson, arquivoResumoHtml) =
                await GeradorResumoAnaliseDdlLote.GerarAsync(entradas, pastaTemp + Path.DirectorySeparatorChar, IdiomaSaida.English);

            Assert.True(File.Exists(arquivoResumoJson));
            Assert.True(File.Exists(arquivoResumoHtml));

            string jsonResumo = await File.ReadAllTextAsync(arquivoResumoJson);
            var resumo = JsonSerializer.Deserialize<ResultadoResumoLoteDdl>(jsonResumo);

            Assert.NotNull(resumo);
            Assert.Equal(2, resumo!.TotalBases);
            Assert.Equal(2, resumo.BasesComAchados);
            Assert.Equal(1, resumo.BasesComCriticos);
            Assert.Equal(15, resumo.TotalTabelas);
            Assert.Equal(5, resumo.TotalAchados);
            Assert.Equal(1, resumo.TotalCriticos);
            Assert.Equal(2, resumo.TotalAltos);
            Assert.Equal(1, resumo.TotalMedios);
            Assert.Equal(1, resumo.TotalBaixos);
            Assert.Equal(2, resumo.Bases.Count);

            var codigoTabelaSemPk = resumo.ResumoPorCodigo.FirstOrDefault(i => i.Chave == "TABELA_SEM_PK");
            Assert.NotNull(codigoTabelaSemPk);
            Assert.Equal(3, codigoTabelaSemPk!.Quantidade);
        }
        finally
        {
            if (Directory.Exists(pastaTemp))
                Directory.Delete(pastaTemp, recursive: true);
        }
    }
}
