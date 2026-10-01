# Git no Lunet

O Lunet tem Git próprio, que funciona no aparelho sem instalar nada. Abra **⋯ → Ferramentas → Git**.

## Primeiros passos

1. Toque em **Iniciar repositório Git**. O Lunet cria um `.gitignore` que ignora a pasta interna `.lunet/`, `bin/` e `obj/`.
2. Em **Alterações**, toque em **Commit…**, escreva o que mudou e confirme. Na primeira vez o Lunet pede seu nome e e-mail.
3. Em **Histórico**, toque num commit para ver os arquivos alterados, reverter ou criar um ramo.

## Ramos

Crie um ramo para experimentar sem medo: **Ramos → Novo ramo…**. Troque de ramo tocando nele. Se houver mudanças não commitadas que seriam perdidas, o Lunet recusa e diz quais arquivos.

## GitHub

1. Em **Remoto**, defina o endereço, por exemplo `https://github.com/usuario/projeto.git`.
2. Crie um token no GitHub (Settings → Developer settings → Personal access tokens) com acesso ao repositório e informe em **Editar conta…**.
3. Use **Pull** para trazer novidades e **Push** para enviar.

Se o servidor tiver commits que você não tem, o push é recusado: faça **Pull** antes. Quando os dois lados mudam o mesmo arquivo, o Pull para e avisa; resolva o conflito em outro cliente Git.

Para baixar um projeto existente, use **Clonar do Git** na lista de projetos (o repositório precisa ter o `lunet.json` na raiz).
