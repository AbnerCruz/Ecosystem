# Plano — Lunet na casca única em Avalonia (25/11/2026)

Fonte: estudo do proprietário *"Lunet — o que foi pedido, o que foi entregue e o caminho até o potencial real"* (24/11/2026) e a ordem "assume o desenvolvimento em avalonia" (ADD-0036 / DEC-0068 / [ADR 0013](../adr/0013-casca-unica-avalonia.md)). Este arquivo é a ordem de trabalho; o estado vivo fica no ROADMAP (LUNET-530…) e nas Issues.

## O que o proprietário quer (cinco desejos estáveis do estudo)

1. **Fluxo contínuo:** código, arte, som, teste e estudo são vistas do mesmo jogo, a um toque, preservando onde você estava.
2. **Ferramenta profissional no polegar:** Aseprite, LDtk, BeepBox e VS Code adaptados ao toque.
3. **IA confiável:** configura uma vez, distingue ensinar de fazer, edita trechos, mostra o diff, sempre desfaz.
4. **Seus dados são seus:** pastas reais, formatos abertos, offline.
5. **Um produto, não um laboratório:** cada frente serve o fluxo, não vira mais uma tela.

## Ondas e portões

Cada onda termina num portão que o proprietário pode experimentar. Capturas headless e CI são evidência automática, não teste no aparelho (ADD-0035).

### Onda 0 — Limpar o terreno
- Congelar Explorar, novos cursos, mapas mentais e novas APIs isoladas do Framework.
- Encolher a governança local do Lunet (ROADMAP curto, `OWNER-ORDERS-STATUS.md` como o do Numa).
- Corrigir guias que não compilam; todo exemplo de documentação sob teste.
- Ambiente de agente com .NET 10; capturas em todo PR de interface (**feito nesta fatia para a casca nova**: `Lunet --screens` no CI).

### Onda 1 — Fundação comum com o Numa
- **Casca `Lunet.Client` (feito, fatia 1 — LUNET-530):** Home, Workspace com seis abas, painéis Console/Problemas, jogo embutido com pausa/passo/velocidade, layout compacto/expandido, Back/Esc, gravação verificada. Desktop já usa a casca.
- **Cabeça Android Avalonia (LUNET-531):** projeto `net10.0-android` hospedando `LunetShell`, pasta de projetos do app, Back do sistema; provas: IME PT-BR, jogo a 60 FPS, partida a frio < 1,5 s no moto g32.
- **Ecosystem.Ui (LUNET-532):** Extraction Review NN-022 com Numa e Lunet como consumidores; só extrair se o saldo for positivo.
- Portão: Home e projeto vazio abrem na casca nova, no celular e no Windows, com a cara do Numa e acento próprio.

### Onda 2 — Ciclo do programador
- View de código própria sobre `PieceTable` (linhas virtualizadas, realce incremental), barra de teclas de deslize, signature help, paleta de comandos.
- IA no painel comum, com revisão por trecho e ferramentas de rodar e capturar o jogo.
- Portão: sem teclado físico, criar projeto, escrever personagem que anda, pedir um pulo à IA, revisar o diff e jogar, sem diálogo modal.

### Onda 3 — Ciclo do artista
- Motor de canvas comum (gestos, zoom, undo por diferença, autosave) para todos os Studios.
- Sprite v2 (camadas × frames, clips), Tile v2 (IntGrid + regras), música no canvas novo, colisores sobre o sprite; biblioteca visível em cada Studio.
- Portão: personagem com três animações, fase com regras de tile, música, tudo rodando ao lado sem exportar à mão.

### Onda 4 — Prova de produto
- Tutorial 1 (plataforma) feito inteiro no Lunet; curso Aprender reescrito sobre ele.
- APK com ícone, assinatura de release e compartilhamento; Launcher como vitrine.
- Portão: jogo de plataforma completo instalado em outro celular por APK, feito sem sair do Lunet.

Depois: multiplayer em dois aparelhos, UI Studio, plugins, Tutorial 2.
