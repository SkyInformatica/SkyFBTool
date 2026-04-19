using SkyFBTool.Infra;
using Xunit;

namespace SkyFBTool.Tests.Infra;

public class DestinoArquivoTests
{
    [Fact]
    public async Task DestinoArquivo_GeraUtf8SemBom()
    {
        string pastaTemp = Path.Combine(Path.GetTempPath(), "SkyFBTool.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pastaTemp);
        string arquivoBase = Path.Combine(pastaTemp, "export.sql");

        try
        {
            await using (var destino = new DestinoArquivo(arquivoBase, tamanhoMaximoArquivoMb: 100))
            {
                await destino.EscreverLinhaAsync("INSERT INTO T (NOME) VALUES ('ação');");
            }

            byte[] bytes = await File.ReadAllBytesAsync(arquivoBase);
            Assert.False(bytes.Length >= 3 &&
                         bytes[0] == 0xEF &&
                         bytes[1] == 0xBB &&
                         bytes[2] == 0xBF);

            string texto = File.ReadAllText(arquivoBase, new System.Text.UTF8Encoding(false));
            Assert.Contains("ação", texto);
        }
        finally
        {
            if (Directory.Exists(pastaTemp))
                Directory.Delete(pastaTemp, recursive: true);
        }
    }

    [Fact]
    public async Task EscreverLinhaAsync_QuandoExcedeLimite_GeraArquivosParticionadosComCabecalho()
    {
        string pastaTemp = Path.Combine(Path.GetTempPath(), "SkyFBTool.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pastaTemp);
        string arquivoBase = Path.Combine(pastaTemp, "export.sql");

        try
        {
            await using (var destino = new DestinoArquivo(arquivoBase, tamanhoMaximoArquivoMb: 1))
            {
                await destino.EscreverLinhaAsync("SET SQL DIALECT 3;");
                await destino.EscreverLinhaAsync("SET NAMES UTF8;");
                await destino.EscreverLinhaAsync(string.Empty);
                await destino.EscreverLinhaAsync("INSERT INTO T (ID, NOME) VALUES (1, 'A');");

                string payload = new('X', 4096);
                for (int i = 2; i <= 2000; i++)
                {
                    await destino.EscreverLinhaAsync($"INSERT INTO T (ID, NOME) VALUES ({i}, '{payload}');");
                }

                var arquivosGerados = destino.ObterArquivosGerados();
                Assert.True(arquivosGerados.Count > 1);
                Assert.Equal(arquivoBase, arquivosGerados[0].Caminho);
                Assert.EndsWith("_part002.sql", arquivosGerados[1].Caminho, StringComparison.OrdinalIgnoreCase);
            }

            var segundaParte = Path.Combine(pastaTemp, "export_part002.sql");
            Assert.True(File.Exists(segundaParte));

            var linhasIniciais = File.ReadLines(segundaParte).Take(3).ToArray();
            Assert.Equal("SET SQL DIALECT 3;", linhasIniciais[0]);
            Assert.Equal("SET NAMES UTF8;", linhasIniciais[1]);
            Assert.Equal(string.Empty, linhasIniciais[2]);
        }
        finally
        {
            if (Directory.Exists(pastaTemp))
                Directory.Delete(pastaTemp, recursive: true);
        }
    }
}
