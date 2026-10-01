# Salvamento e recuperação

## Salvamento automático

Você nunca precisa apertar "salvar". O Urbe grava sozinho, logo depois de cada mudança, e também quando você sai do app ou troca de aba.

O **pontinho** ao lado do nome **Urbe**, no topo da Cidade, mostra o estado:

| Cor | Significa |
|---|---|
| 🟢 Verde | Tudo salvo |
| 🟡 Amarelo | Salvando agora |
| 🔴 Vermelho | Deu erro ao salvar (veja [[Tutorial/Solução de problemas|Solução de problemas]]) |

No editor, o rodapé mostra **Salvo**.

## Se o app fechar no meio

Antes de gravar, o Urbe anota tudo num "diário" (journal). Se o aparelho desligar, a bateria acabar ou o navegador fechar no meio de uma gravação, na próxima vez o Urbe **recupera o que faltava** sozinho.

## Funciona sem internet

Depois de aberto uma vez, o Urbe funciona **offline**: dá para escrever no avião, no metrô ou no sítio. Só o Assistente precisa de internet, a não ser que você use um modelo local (veja [[Tutorial/Assistente/Conectar um modelo|Conectar um modelo]]).

## Três redes de proteção

1. **Desfazer** (Ctrl+Z) para o que você acabou de fazer.
2. **Versões anteriores**: até 40 por nota.
3. **Lixeira**: para o que foi excluído.

Tudo explicado em [[Tutorial/Notas/Lixeira, versões e desfazer|Lixeira, versões e desfazer]].

## Diagnóstico

O comando **Diagnóstico do workspace** (Ctrl+K) confere se as notas, os links e o mapa estão coerentes e mostra o que encontrou.

## Renomear e mover por fora do app

Pode renomear ou mover notas e pastas pelo gerenciador de arquivos, pelo Obsidian ou pelo sincronizador. O Urbe reconhece a nota pelo conteúdo e ela continua sendo **a mesma**: mantém as versões anteriores, a casa na Cidade e os anexos. Nada é escrito dentro das suas notas para isso; o Urbe guarda essa identificação em `.urbe/identity.json`.

Se duas notas idênticas aparecerem ao mesmo tempo, o Urbe não tenta adivinhar qual é a original: as duas entram como notas novas.

## Limpar referências órfãs

Com o tempo, o histórico e as composições podem guardar referências a notas que não existem mais (apagadas da lixeira ou removidas por fora). O comando **Limpar referências órfãs** (Ctrl+K) mostra primeiro o que encontrou e só apaga quando você confirma:

- as versões de uma nota que não existe mais, depois de 30 dias;
- nas composições, só as fontes que sumiram (a composição continua).

Nada que ainda existe é tocado: notas, notas na lixeira e o histórico delas ficam como estão. Cada limpeza fica registrada em `.urbe/vault.json`.
