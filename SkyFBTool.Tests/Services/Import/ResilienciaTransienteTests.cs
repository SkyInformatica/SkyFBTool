using SkyFBTool.Services.Export;
using SkyFBTool.Services.Import;
using Xunit;

namespace SkyFBTool.Tests.Services.Import;

public class ResilienciaTransienteTests
{
    [Fact]
    public void EhFalhaTransienteEscrita_ComIOException_DeveRetornarTrue()
    {
        Assert.True(ExportadorTabelaFirebird.EhFalhaTransienteEscrita(new IOException("disk busy")));
    }

    [Fact]
    public void EhFalhaTransienteEscrita_ComErroNaoTransiente_DeveRetornarFalse()
    {
        Assert.False(ExportadorTabelaFirebird.EhFalhaTransienteEscrita(new InvalidOperationException("invalid operation")));
    }

    [Fact]
    public void EhFalhaTransienteExecucao_ComTimeout_DeveRetornarTrue()
    {
        Assert.True(ExecutorSql.EhFalhaTransienteExecucao(new TimeoutException("timeout")));
    }

    [Fact]
    public void EhFalhaTransienteExecucao_ComMensagemDeDeadlock_DeveRetornarTrue()
    {
        Assert.True(ExecutorSql.EhFalhaTransienteExecucao(new Exception("deadlock detected")));
    }

    [Fact]
    public void EhFalhaTransienteExecucao_ComErroNaoTransiente_DeveRetornarFalse()
    {
        Assert.False(ExecutorSql.EhFalhaTransienteExecucao(new Exception("syntax error near FROM")));
    }
}
