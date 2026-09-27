# Solução de problemas

## Um botão não responde no celular

Use o [[Tutorial/Celular e computador/Diagnóstico de toques|Diagnóstico de toques]] e mande o relatório para quem cuida do Urbe.

## A tela ficou estranha depois de mudar a aparência

1. **Personalização → Arquivo → Restaurar padrão**, ou o comando **Restaurar aparência padrão** (Ctrl+K).
2. Se nem isso aparece direito, abra o Urbe com `?seguro=1` no fim do endereço. É o **modo seguro**: desliga todos os estilos e plugins, sem apagar nada. Depois, em **Personalização → Plugins**, desligue o que causou o problema e desligue o modo seguro.

## Um plugin parou de funcionar

- **Código mudou**: o arquivo foi alterado. Leia o código e toque em **Revisei, ligar**.
- **Erro**: a mensagem aparece embaixo do plugin. Corrija o código ou peça ajuda ao Assistente.
- O comando **Recarregar plugins** reinicia todos.

## Sumiu uma nota

- Veja a **Lixeira**: Notas → ⋯ → Lixeira.
- Use a **busca** (Ctrl+P), que procura também no conteúdo.
- O texto mudou e você quer o antigo? **Versões anteriores**, no menu ⋯ da nota.

## O pontinho do topo ficou vermelho

O Urbe não conseguiu gravar.

- Se você usa uma **pasta no dispositivo**, o navegador pode ter perdido a permissão: vá em **Configurações → Pasta no dispositivo** e escolha a pasta de novo.
- Veja se o aparelho não está sem espaço.
- Faça uma cópia de segurança com **Exportar tudo (.zip)** assim que puder.

## O Assistente não responde

- Confira se há um **modelo conectado** (⚙️ do Assistente). Use **Testar**.
- Erro **401**: a chave está errada ou foi apagada. Cole de novo.
- Erro **402**: acabaram os créditos na conta do provedor.
- Erro **429**: muitos pedidos seguidos. Espere um pouco; o Urbe tenta de novo sozinho.
- **Ollama**: o programa precisa estar aberto, com `OLLAMA_ORIGINS=*`.

## A cidade está lenta

Em **Personalização**:

- desligue o **efeito de vidro** (Aparência);
- ponha as **animações** em Reduzidas;
- desligue **moradores** e **animais** (Cidade).

## A cidade não mostra as minhas notas

- Use o comando **Enquadrar todas as notas**.
- Abra o [[Tutorial/Cidade/O mapa|mapa]] e toque no seu bairro.

## Quero o Tutorial de volta

**Configurações → Tutorial → Restaurar o Tutorial**. Ele recria as notas que faltam e pergunta antes de substituir as que você mudou.

Voltar: [[Tutorial/Comece aqui|Comece aqui]]
