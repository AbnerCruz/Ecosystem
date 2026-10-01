# Plugins

Um **plugin** é um pequeno programa em JavaScript que ensina coisas novas ao Urbe: um comando na paleta, um botão na cidade, um desenho sobre o mapa, uma ferramenta nova para o Assistente…

## Segurança em primeiro lugar

> [!warning] Plugins são programas de verdade
> Um plugin pode ler e mudar as suas notas. Só ligue código que você entende ou que veio de alguém de confiança.

Por isso o Urbe tem algumas proteções:

- **Nada roda sozinho.** Todo plugin começa **desligado**, e só você liga, na tela de Plugins, neste aparelho.
- **Se o código mudar, o plugin para.** Ao ligar, o Urbe guarda uma "impressão digital" do código. Se o arquivo mudar depois (por você, por sincronização ou pelo Assistente), o plugin fica com o aviso **Código mudou** até você revisar e ligar de novo.
- **Erros não derrubam o app.** Se um plugin der erro, ele fica marcado com **Erro** e a mensagem aparece na tela.
- **Modo seguro.** Desliga todos os plugins e estilos de uma vez, sem apagar nada. Fica em **Personalização → Plugins**. Também dá para abrir o Urbe com `?seguro=1` no fim do endereço.
- Plugins não podem escrever na própria pasta de plugins.

## Criar o primeiro plugin

1. **Personalização → Plugins → Novo plugin**.
2. Escolha um modelo:
   - **Básico**: um comando na paleta que mostra quantas notas existem.
   - **Diário**: um botão na cidade que abre (ou cria) a nota do dia.
   - **Desenho no mapa**: uma estrela sobre as casas das notas com a tag `#importante`.
   - **Ferramenta do Assistente**: o Assistente aprende a dizer a data e a hora.
3. Dê um nome. O arquivo é criado em `Personalização/plugins/`, **desligado**.
4. Toque em **Ver código** para ler (e mudar, se quiser).
5. Toque em **Ligar** e confirme.

Para testar o modelo **Básico**: aperte **Ctrl+K**, digite *contar* e escolha **Contar notas**.

## Como é um plugin

```js
urbe.plugin({
  nome: 'Olá',
  versao: '1.0',
  descricao: 'Diz olá',

  ligar(api) {
    api.comando({
      titulo: 'Dizer olá',
      executar() {
        api.aviso('Olá! Você tem ' + api.notas.listar().length + ' notas.');
      }
    });
  }
});
```

- `ligar(api)` roda quando o plugin é ligado.
- A `api` tem tudo o que o plugin pode usar. Veja [[Tutorial/Personalização/Referência de plugins|Referência de plugins]].
- Tudo o que o plugin cria pela `api` (comandos, botões, estilos, desenhos) **some sozinho** quando ele é desligado.

## Pedir ao Assistente

*"Crie um plugin que adiciona um botão para abrir uma nota aleatória."* O Assistente conhece a API, escreve o arquivo e avisa para você revisar e ligar.

## Recarregar

Mudou o código de um plugin ligado? Ele para e pede nova aprovação. O comando **Recarregar plugins** reinicia os que estão ligados.

Próximo: [[Tutorial/Personalização/Referência de plugins|Referência de plugins]]
