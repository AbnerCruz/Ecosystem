# Onde ficam seus arquivos

O Urbe é **local**: as suas notas ficam no seu aparelho, não num servidor. Ninguém além de você as vê. A única exceção é o Assistente, que envia ao provedor de IA **só o que ele precisar ler para responder**.

## No app instalado (Windows e Android)

Cada nota é um arquivo **`.md` de verdade** numa pasta do aparelho:

- **Windows**: **Documentos\Urbe** (dá para trocar em **Configurações → Pasta no dispositivo**).
- **Android**: **Documentos/Urbe**, no armazenamento interno.

Você vê esses arquivos no explorador ou gerenciador de arquivos, pode sincronizar com OneDrive, Dropbox ou Google Drive, abrir no Obsidian ou em outro editor e guardar num backup normal.

> [!tip] Mudanças feitas fora do Urbe
> Se você editar, criar ou apagar uma nota por fora (no Explorer, no Obsidian, pelo OneDrive…), o Urbe percebe. No Windows é na hora; no Android, quando você volta para o app. Se a mesma nota tiver uma edição sua no Urbe que ainda não foi salva, a sua edição vale.

Veja como instalar em [[Tutorial/Celular e computador/Instalar como aplicativo|Instalar como aplicativo]].

## No navegador

### Armazenamento do aplicativo (padrão)

Funciona em qualquer navegador. As notas ficam guardadas **dentro do navegador**, neste aparelho, e não aparecem como arquivos.

> [!warning] Cuidado ao limpar o navegador
> "Limpar dados de navegação" pode apagar esse armazenamento. Faça cópias de segurança de vez em quando com **Exportar tudo (.zip)** (veja [[Tutorial/Arquivos e segurança/Importar e exportar|Importar e exportar]]), ou use o app instalado.

### Pasta no dispositivo

No **Chrome** e no **Edge** do computador, você pode escolher uma **pasta de verdade**: **Configurações → Pasta no dispositivo**. Escolha a pasta e permita o acesso. O Urbe copia as notas para lá.

Ao abrir, o Urbe confere a pasta. Se algo mudou, ele **pergunta** o que fazer:

- **O navegador pediu permissão de novo**: toque em **Permitir acesso à pasta**. Suas notas continuam lá.
- **A pasta foi apagada, movida ou renomeada**: escolha **outra pasta** ou passe a usar o **armazenamento do aplicativo**.

Uma pasta **nova e vazia** começa do zero, com a pasta **Tutorial** criada nela.

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

## Exportações

Arquivos que você exporta (HTML de páginas, `.zip` do vault, PDF) vão para onde você escolher no Windows e para **Downloads/Urbe** no Android.

Próximo: [[Tutorial/Arquivos e segurança/Importar e exportar|Importar e exportar]]
