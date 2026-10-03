using Android.App;

// NN-016: rede pública e serviço dataSync apenas para APK explicitamente solicitado, em cache privado.
[assembly: UsesPermission(Android.Manifest.Permission.Internet)]
[assembly: UsesPermission(Android.Manifest.Permission.ForegroundService)]
[assembly: UsesPermission(Android.Manifest.Permission.ForegroundServiceDataSync)]
// Android 13+: solicitada pela UI; recusa não impede FGS, exibido pelo painel de apps ativos do sistema.
[assembly: UsesPermission(Android.Manifest.Permission.PostNotifications)]

// P4-3: UI-only request, never a permission inherited by agents/plugins.
[assembly: UsesPermission(Android.Manifest.Permission.RequestInstallPackages)]
