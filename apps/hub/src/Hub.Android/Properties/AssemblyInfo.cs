using Android.App;

// network.access (ecosystem.json): leitura do repositório e da API pública do GitHub. Nenhuma outra permissão é pedida (NN-016).
[assembly: UsesPermission(Android.Manifest.Permission.Internet)]
