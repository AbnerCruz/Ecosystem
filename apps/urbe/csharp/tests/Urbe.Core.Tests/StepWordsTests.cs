using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

public sealed class StepWordsTests
{
    [Theory]
    [InlineData("Lendo Fotossíntese", "Leu Fotossíntese")]
    [InlineData("Buscando “luz” nas notas", "Buscou “luz” nas notas")]
    [InlineData("Editar Comece", "Editou Comece")]
    [InlineData("Criar Diário", "Criou Diário")]
    [InlineData("Ler 3 notas", "Leu 3 notas")]
    [InlineData("Vendo a página", "Viu a página")]
    [InlineData("Excluir Rascunho", "Excluiu Rascunho")]
    [InlineData("Reescrever Plano", "Reescreveu Plano")]
    [InlineData("Formulário de cadastro", "Formulário de cadastro")]
    [InlineData("Pesquisador: pergunta", "Pesquisador: pergunta")]
    [InlineData("Tempo: clima", "Tempo: clima")]
    public void AFinishedStepSaysWhatWasDone(string label, string past) => Assert.Equal(past, StepWords.Past(label));
}
