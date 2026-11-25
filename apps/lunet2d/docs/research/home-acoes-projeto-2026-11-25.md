# Ações de projeto na tela inicial, 25/11/2026

Estudo antes de levar renomear, duplicar e apagar para a casca.

## O que existia

Nas telas anteriores (`MainActivity.ShowProjectMenu`), o menu tinha Fixar, Exportar ZIP e **Excluir**. Excluir abria uma caixa de confirmação e apagava a pasta de vez: "Isso não pode ser desfeito". Para criar havia um formulário longo, com nome, orientação, resolução, atualizações por segundo, ajuste de tela, versão e identificador Android.

## Referências

- **Material, confirmação e desfazer:** a confirmação não é necessária quando a ação é reversível; basta oferecer "Desfazer" num snackbar, que some sozinho ([Material 2, confirmation & acknowledgement](https://m2.material.io/design/communication/confirmation-acknowledgement.html), [M3 snackbar](https://material.io/components/snackbars)). Diálogo em toda ação cansa ([Compose snackbar undo](https://proandroiddev.com/implementing-snackbar-to-undo-actions-in-jetpack-compose-7b8ec92cf8a7)). O Android documenta o mesmo padrão, "1 item removido · DESFAZER" ([snackbar action](https://developer.android.com/training/snackbar/action)).

## Decisões

1. **Apagar move para `.lixeira/`** dentro da pasta de projetos (mesmo disco, então mover é instantâneo) e mostra **Desfazer** por 8 s. Só **Apagar de vez**, na Lixeira, perde dados, e pede um segundo toque em linha, sem caixa de diálogo.
2. **Renomear não muda GameId nem PackageId:** o APK instalado continua atualizando.
3. **Duplicar dá identidade nova** (GameId, PackageId e chave), para a cópia se instalar ao lado da original. `.lunet` (saves, cache, histórico), `bin`, `obj` e `.git` ficam com a original. A cópia aparece inteira ou não aparece (pasta de preparo e depois mover).
4. **Criar em um toque:** o nome já vem preenchido, há cinco pontos de partida (os modelos do núcleo) e a orientação. Resolução, versão e identificador continuam com padrões seguros e são editáveis depois no `lunet.json`. O formulário longo afastava quem só queria começar.

## Fora deste passo

- Exportar e importar ZIP e a pasta compartilhada (P1).
- Fixar no início e miniatura do jogo na capa.
- Esvaziar a lixeira automaticamente: nada é apagado sem a pessoa pedir.
