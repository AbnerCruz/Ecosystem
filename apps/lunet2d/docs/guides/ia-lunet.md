# IA do Lunet

A IA trabalha junto com você dentro do projeto: constrói, corrige, explica e cria arte, sem sair do editor.

## Configurar (uma vez)

Toque em **✦ IA** e escolha o provedor (OpenRouter, Anthropic, OpenAI, Google Gemini, DeepSeek, Groq ou um endpoint compatível), o modelo e a chave da API. A chave fica cifrada no Android Keystore deste aparelho; nunca vai para o projeto, o Git ou o APK do jogo. O provedor pode cobrar pelo uso.

## Usar

- **✦ IA** na barra superior abre o painel ao lado do código (folha inferior no celular, painel lateral em telas largas).
- Selecione um trecho e toque em **✦ Pedir à IA**.
- Em **Erros**, toque em **✦ Corrigir com a IA**; no **Console**, pergunte o que está acontecendo.
- **📎** anexa imagens e arquivos (do aparelho ou das suas Notas). Anexos são copiados para `Notes/`.

A IA recebe automaticamente o arquivo aberto, a linha do cursor, a seleção, os erros e o console.

## O que ela sabe

- **O próprio Lunet:** pesquisa a referência da API do Framework, os guias offline e a lista do que existe e do que ainda não existe. Se você pedir algo que o Lunet não tem, ela diz e propõe a melhor alternativa.
- **O seu projeto:** lê e busca arquivos, conhece a paleta, o tamanho predominante dos sprites, os nomes, as classes e as suas notas de estilo.

## Coerência artística e contextual

Antes de criar arte ou mecânicas, a IA lê o estilo do projeto e segue a paleta, os tamanhos, o contorno, os nomes e o estilo de código já usados. Ao criar um sprite, ela é avisada se uma cor ou tamanho foge do padrão. Escreva suas preferências em uma nota com "estilo" no nome (por exemplo `Notes/estilo-visual.md`): a IA segue essa nota.

## Desenho → sprite

1. Em **Notas e esboços**, importe a foto do desenho (⇩) ou desenhe um esboço.
2. Abra a imagem e toque em **✦ Virar sprite** (ou anexe a imagem na conversa).
3. A IA enxerga a imagem como uma grade de pixels (fundo do papel removido), redesenha limpa no estilo do projeto e salva `Assets/<nome>.sprite.json` (editável no Sprite Studio) e `Assets/<nome>.png` (para o jogo), com o código de uso.

## Segurança e revisão

- Escrever no projeto é opcional (configurações da IA).
- Toda alteração entra num checkpoint: o cartão de mudanças mostra os arquivos, o diff, **Manter** ou **Desfazer tudo**.
- Quando altera código, a IA compila e só conclui quando o projeto compila sem erros.
- A IA só acessa arquivos dentro do projeto aberto.

## Limites atuais

- A IA vê imagens pela grade de pixels (até 64×64 células), não como uma foto em alta resolução.
- Arquivos binários que não são imagem (PDF, por exemplo) ficam guardados em `Notes/`, mas a IA não os lê.
