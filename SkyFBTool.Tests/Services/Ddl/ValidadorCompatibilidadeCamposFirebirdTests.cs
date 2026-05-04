using SkyFBTool.Services.Ddl;
using Xunit;

namespace SkyFBTool.Tests.Services.Ddl;

public class ValidadorCompatibilidadeCamposFirebirdTests
{
    [Fact]
    public void Validar_QuandoBooleanNoFirebird2_DeveGerarAchado()
    {
        var snapshot = new SnapshotSchema
        {
            VersaoMajor = 2,
            Tabelas =
            [
                new TabelaSchema
                {
                    Nome = "CLIENTES",
                    Colunas =
                    [
                        new ColunaSchema { Nome = "ATIVO", TipoSql = "BOOLEAN" }
                    ]
                }
            ]
        };

        var achados = ValidadorCompatibilidadeCamposFirebird.Validar(snapshot, snapshot.VersaoMajor);

        var achado = Assert.Single(achados, a => a.Codigo == "CAMPO_TIPO_INCOMPATIVEL_VERSAO");
        Assert.Equal("critical", achado.Severidade);
        Assert.Equal("CLIENTES.ATIVO", achado.Escopo);
        Assert.Equal("BOOLEAN", achado.TipoSql);
        Assert.Equal(3, achado.VersaoMinimaMajor);
    }

    [Fact]
    public void Validar_QuandoVarcharExcedeLimite_DeveGerarAchado()
    {
        var snapshot = new SnapshotSchema
        {
            Tabelas =
            [
                new TabelaSchema
                {
                    Nome = "DOCUMENTOS",
                    Colunas =
                    [
                        new ColunaSchema { Nome = "DESCRICAO", TipoSql = "VARCHAR(40000)" }
                    ]
                }
            ]
        };

        var achados = ValidadorCompatibilidadeCamposFirebird.Validar(snapshot);

        var achado = Assert.Single(achados, a => a.Codigo == "CAMPO_TAMANHO_EFETIVO_EXCEDIDO");
        Assert.Equal(32_765, achado.Limite);
        Assert.Equal(40_000, achado.TamanhoDeclarado);
        Assert.Equal(40_000, achado.TamanhoEfetivoBytes);
    }

    [Fact]
    public void Validar_QuandoVarcharUtf8EstouraBytes_DeveGerarAchadoEfetivo()
    {
        var snapshot = new SnapshotSchema
        {
            Tabelas =
            [
                new TabelaSchema
                {
                    Nome = "DOCUMENTOS",
                    Colunas =
                    [
                        new ColunaSchema
                        {
                            Nome = "DESCRICAO",
                            TipoSql = "VARCHAR(32765)",
                            CharsetNome = "UTF8",
                            BytesPorCaracter = 4
                        }
                    ]
                }
            ]
        };

        var achados = ValidadorCompatibilidadeCamposFirebird.Validar(snapshot);

        var achado = Assert.Single(achados, a => a.Codigo == "CAMPO_TAMANHO_EFETIVO_EXCEDIDO");
        Assert.Equal(131060, achado.Valor);
        Assert.Equal(32_765, achado.Limite);
        Assert.Equal("UTF8", achado.CharsetNome);
    }
}
