# Publicações: o que cada formato preserva (CST-185, 16/11/2026)

A tabela registra só o que foi confirmado no código ou num teste. Onde nada confirmou, está escrito "não verificado".

**Prova de abertura** (`ExportFidelityTests`, executada neste ambiente): a mesma nota foi exportada em DOCX, ODT, PPTX e PDF. Os três primeiros foram abertos e convertidos pelo LibreOffice 7; o PDF foi lido pelo poppler. Em todos o texto saiu como texto, com acentos ("Ação, coração e pão"), tabela e lista. Nenhum formato virou imagem. O teste é pulado onde o LibreOffice não existe, como no CI.

| Formato | Texto editável | Pesquisável | Estrutura | Imagens | Fórmulas | Acessível | Offline | Limitações declaradas ao exportar |
|---|---|---|---|---|---|---|---|---|
| HTML | sim | sim | títulos e seções | dentro do arquivo (`data:`) | SVG | semântica HTML; não auditado com leitor de tela | sim (sem dependência externa; a página bloqueia tudo de fora) | imagem ausente aparece como texto no lugar |
| Pacote ZIP | sim (o HTML) | sim | HTML + `manifest.json` | dentro do HTML; áudio e vídeo em `media/` | SVG | como o HTML | sim | manifesto com tipo, tamanho e SHA-256 de cada arquivo |
| EPUB | sim | sim | sumário (`nav`), capítulos | dentro do pacote | — | metadados `schema:accessMode`; aviso de imagem sem descrição | sim | imagens sem descrição são listadas |
| DOCX | sim (aberto no LibreOffice) | sim | estilos de título, listas, tabelas | dentro | TeX como texto | não verificado | sim | fórmulas em TeX; gráficos em SVG; imagem em lista, tabela ou citação vira texto alternativo; ligações `[[…]]` viram texto |
| ODT | sim (aberto no LibreOffice) | sim | idem DOCX | dentro | TeX como texto | não verificado | sim | idem DOCX |
| PPTX | sim: caixas de texto, não imagens (aberto no LibreOffice) | sim | um slide por seção | dentro | TeX como texto | não verificado | sim | idem DOCX |
| PDF | não (formato final) | sim (texto real; poppler extrai) | sumário em páginas no layout livro; links clicáveis | dentro (desenhadas) | desenhadas (vetor) | **não**: o PDF não tem marcação para leitor de tela | sim | barras de progresso como texto; não há marcadores de navegação nem marcação de acessibilidade |
| MP4 | — | — | cenas da timeline | quadros desenhados | desenhadas | legendas SRT e WebVTT à parte | sim (codificador do aparelho ou ffmpeg) | sem codificador, o app diz que não há e não finge |

**Regras dos rótulos:**
- Nada rasterizado é chamado de editável.
- O PDF não é chamado de livro acessível.
- Quando uma exportação perde algo, ela diz o quê no aviso de salvar.

**Pendente:**
- abrir os arquivos no Word e no PowerPoint;
- leitor de tela no HTML e no EPUB;
- comparação visual de renderização entre formatos;
- testar no aparelho.
