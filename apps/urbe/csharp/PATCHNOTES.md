# Numa — Notas da versão atual

**Versão canônica preparada:** 2.0.0-beta.63
**Canal:** Numa 2.0 nativo, Android e Windows
**Publicação:** pelo workflow oficial depois da integração.
**Atualização:** instala por cima da beta anterior (app.urbe.csharp, mesmo certificado).

## Etapas da IA sem travar (#1000)

- **O app não trava mais com muitas etapas.** Cada palavra que o modelo escrevia redesenhava o chat inteiro e, com o painel de etapas aberto, esvaziava e remontava a lista. Com 1000 etapas, isso custava 53 ms por palavra num computador. Dezenas de palavras por segundo passavam de um segundo de trabalho por segundo, e o toque, a rolagem e a digitação ficavam sem vez.
- **Agora:** só a etapa que mudou é trocada, no lugar, e as demais nem saem da tela. A linha da atividade só é refeita quando o que ela diz muda; o resumo do raciocínio muda só o texto, e a barra de progresso não reinicia. A busca de cada ferramenta virou um índice. O chat é redesenhado no máximo a cada 90 ms enquanto a IA trabalha. Medido: de 53 ms para 15 ms com o painel aberto, e de 13 para 11 ms com ele fechado, num computador (no celular, os números são maiores, na mesma proporção).
- Nenhuma etapa é escondida e a execução não tem limite: o painel continua com X fixo e "Ver anteriores" / "Ver mais recentes".

## Vídeos

- **Um vídeo numa chamada.** O Assistente cria o vídeo inteiro de uma vez: uma nota por cena com a fala, a publicação com o tema e cada cena composta por um layout. Tudo é conferido antes de gravar, então um erro custa uma correção, não um vídeo pela metade. Depois ele olha os quadros e gera o MP4: são três passos onde antes eram dezenas.
- **Atividade com nomes claros.** A linha de atividade diz "Criando o vídeo", "Olhando os quadros do vídeo", "Ajustando uma cena" e "Gerando o MP4".

## Não testado no aparelho

- Testado com testes automáticos e um modelo simulado; não reexecutei um pedido real de vídeo com o seu modelo.
- A fluidez do painel de etapas foi medida no computador, sem GPU; não medi no seu Android.
