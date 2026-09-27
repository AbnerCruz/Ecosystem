# Onde ficam seus arquivos

O Urbe é **local**: as suas notas ficam no seu aparelho, não num servidor. Ninguém além de você as vê. A única exceção é o Assistente, que envia ao provedor de IA **só o que ele precisar ler para responder**.

## Dois lugares possíveis

### Armazenamento do aplicativo (padrão)

Funciona em qualquer navegador e no celular. As notas ficam guardadas dentro do navegador, neste aparelho.

> [!warning] Cuidado ao limpar o navegador
> "Limpar dados de navegação" ou desinstalar o app pode apagar esse armazenamento. Faça cópias de segurança de vez em quando com **Exportar tudo (.zip)** (veja [[Tutorial/Arquivos e segurança/Importar e exportar|Importar e exportar]]).

### Pasta no dispositivo

Em navegadores de computador que permitem (como **Chrome** e **Edge**), você pode escolher uma **pasta de verdade** no computador. Cada nota vira um arquivo `.md` que você vê no explorador de arquivos, pode sincronizar com Dropbox, OneDrive ou Google Drive, abrir em outros programas e guardar num backup normal.

- **Configurações → Pasta no dispositivo**, ou **Notas → ⋯ → Pasta do Urbe no dispositivo**.
- Escolha a pasta e permita o acesso. O Urbe copia as notas para lá.
- Se o navegador não permitir, aparece o aviso "Este navegador usa o armazenamento do aplicativo".

### Quando a pasta some ou pede permissão de novo

Ao abrir, o Urbe confere a pasta. Se algo mudou, ele **pergunta** o que fazer, em vez de seguir sozinho:

- **O aparelho pediu permissão de novo** (comum no Android depois de reiniciar): toque em **Permitir acesso à pasta**. Suas notas continuam lá.
- **A pasta foi apagada, movida ou renomeada**: escolha **outra pasta** ou passe a usar o **armazenamento do aplicativo**.

Uma pasta **nova e vazia** começa do zero, com a pasta **Tutorial** criada nela, mesmo que você já tenha usado o Urbe antes neste aparelho.

## O que tem na pasta

```
Urbe/
  Tutorial/              ← esta pasta
  Personalização/        ← aparência, estilos, texturas, plugins
  Páginas/               ← suas páginas
  (suas pastas e notas)
  .urbe/                 ← arquivos internos: mapa da cidade, lixeira, histórico
```

A pasta `.urbe` guarda onde cada casa fica no mapa, a lixeira, as versões anteriores e as composições. Não precisa mexer nela.

Próximo: [[Tutorial/Arquivos e segurança/Importar e exportar|Importar e exportar]]
