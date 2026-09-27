package app.urbe;

import android.Manifest;
import android.content.ContentResolver;
import android.content.ContentValues;
import android.content.Context;
import android.content.Intent;
import android.content.pm.PackageInfo;
import android.content.pm.PackageManager;
import android.net.Uri;
import android.os.Build;
import android.os.Environment;
import android.print.PrintAttributes;
import android.print.PrintDocumentAdapter;
import android.print.PrintManager;
import android.provider.MediaStore;
import android.provider.Settings;
import android.util.Base64;
import android.webkit.WebView;
import android.webkit.WebViewClient;

import androidx.core.content.ContextCompat;

import com.getcapacitor.JSObject;
import com.getcapacitor.Plugin;
import com.getcapacitor.PluginCall;
import com.getcapacitor.PluginMethod;
import com.getcapacitor.annotation.CapacitorPlugin;

import java.io.File;
import java.io.FileOutputStream;
import java.io.OutputStream;

/**
 * O que o Urbe precisa do Android além do Filesystem do Capacitor:
 * acesso a todos os arquivos (para a pasta Documentos/Urbe ser de verdade), abrir links
 * no navegador, salvar exportações em Downloads/Urbe, imprimir/gerar PDF e minimizar.
 */
@CapacitorPlugin(name = "UrbeAndroid")
public class UrbeAndroidPlugin extends Plugin {

    private WebView printView; // mantém a página viva enquanto o Android imprime

    @PluginMethod
    public void getInfo(PluginCall call) {
        JSObject r = new JSObject();
        try {
            PackageInfo pi = getContext().getPackageManager().getPackageInfo(getContext().getPackageName(), 0);
            r.put("version", pi.versionName);
        } catch (Exception e) {
            r.put("version", "");
        }
        r.put("sdk", Build.VERSION.SDK_INT);
        call.resolve(r);
    }

    @PluginMethod
    public void storageStatus(PluginCall call) {
        int sdk = Build.VERSION.SDK_INT;
        JSObject r = new JSObject();
        r.put("sdk", sdk);
        r.put("needsAllFiles", sdk >= 30);
        r.put("allFiles", sdk >= 30 && Environment.isExternalStorageManager());
        r.put("legacy", sdk < 30 && ContextCompat.checkSelfPermission(getContext(), Manifest.permission.WRITE_EXTERNAL_STORAGE) != PackageManager.PERMISSION_GRANTED);
        call.resolve(r);
    }

    @PluginMethod
    public void requestAllFiles(PluginCall call) {
        if (Build.VERSION.SDK_INT >= 30) {
            try {
                Intent i = new Intent(Settings.ACTION_MANAGE_APP_ALL_FILES_ACCESS_PERMISSION, Uri.parse("package:" + getContext().getPackageName()));
                getActivity().startActivity(i);
            } catch (Exception e) {
                try {
                    getActivity().startActivity(new Intent(Settings.ACTION_MANAGE_ALL_FILES_ACCESS_PERMISSION));
                } catch (Exception ignored) {
                }
            }
        }
        call.resolve();
    }

    @PluginMethod
    public void openUrl(PluginCall call) {
        String url = call.getString("url", "");
        if (url == null || !(url.startsWith("https://") || url.startsWith("http://") || url.startsWith("mailto:") || url.startsWith("tel:"))) {
            call.reject("Endereço não permitido");
            return;
        }
        try {
            Intent i = new Intent(Intent.ACTION_VIEW, Uri.parse(url));
            i.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
            getContext().startActivity(i);
            call.resolve();
        } catch (Exception e) {
            call.reject("Nenhum app abre este endereço");
        }
    }

    @PluginMethod
    public void saveFile(PluginCall call) {
        String name = call.getString("name", "arquivo");
        String mime = call.getString("mime", "");
        String data = call.getString("data", "");
        if (name == null || name.isEmpty()) name = "arquivo";
        name = name.replaceAll("[\\\\/:*?\"<>|]", "-");
        if (mime == null || mime.isEmpty()) mime = "application/octet-stream";
        try {
            byte[] bytes = Base64.decode(data, Base64.DEFAULT);
            if (Build.VERSION.SDK_INT >= 29) {
                ContentResolver cr = getContext().getContentResolver();
                ContentValues v = new ContentValues();
                v.put(MediaStore.Downloads.DISPLAY_NAME, name);
                v.put(MediaStore.Downloads.MIME_TYPE, mime);
                v.put(MediaStore.Downloads.RELATIVE_PATH, Environment.DIRECTORY_DOWNLOADS + "/Urbe");
                Uri uri = cr.insert(MediaStore.Downloads.EXTERNAL_CONTENT_URI, v);
                if (uri == null) throw new Exception("Downloads indisponível");
                try (OutputStream os = cr.openOutputStream(uri)) {
                    if (os == null) throw new Exception("Downloads indisponível");
                    os.write(bytes);
                }
            } else {
                File dir = new File(Environment.getExternalStoragePublicDirectory(Environment.DIRECTORY_DOWNLOADS), "Urbe");
                if (!dir.exists() && !dir.mkdirs()) throw new Exception("Não consegui criar Downloads/Urbe");
                try (FileOutputStream os = new FileOutputStream(new File(dir, name))) {
                    os.write(bytes);
                }
            }
            JSObject r = new JSObject();
            r.put("where", "Downloads/Urbe/" + name);
            call.resolve(r);
        } catch (Exception e) {
            call.reject("Não consegui salvar: " + e.getMessage());
        }
    }

    @PluginMethod
    public void printHtml(PluginCall call) {
        final String html = call.getString("html", "");
        final String name = call.getString("name", "Urbe");
        getActivity().runOnUiThread(() -> {
            try {
                WebView wv = new WebView(getActivity());
                wv.getSettings().setJavaScriptEnabled(true);
                wv.setWebViewClient(new WebViewClient() {
                    private boolean done = false;

                    @Override
                    public void onPageFinished(WebView view, String url) {
                        if (done) return;
                        done = true;
                        view.postDelayed(() -> {
                            try {
                                PrintManager pm = (PrintManager) getActivity().getSystemService(Context.PRINT_SERVICE);
                                PrintDocumentAdapter ad = view.createPrintDocumentAdapter(name);
                                pm.print(name, ad, new PrintAttributes.Builder().build());
                                JSObject r = new JSObject();
                                r.put("printing", true);
                                call.resolve(r);
                            } catch (Exception e) {
                                call.reject("Não consegui abrir a impressão: " + e.getMessage());
                            }
                        }, 700);
                    }
                });
                printView = wv;
                wv.loadDataWithBaseURL("https://localhost/", html, "text/html", "UTF-8", null);
            } catch (Exception e) {
                call.reject("Não consegui abrir a impressão: " + e.getMessage());
            }
        });
    }

    @PluginMethod
    public void minimize(PluginCall call) {
        getActivity().runOnUiThread(() -> getActivity().moveTaskToBack(true));
        call.resolve();
    }
}
