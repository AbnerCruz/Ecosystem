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

import com.getcapacitor.JSArray;
import com.getcapacitor.JSObject;
import com.getcapacitor.Plugin;
import com.getcapacitor.PluginCall;
import com.getcapacitor.PluginMethod;
import com.getcapacitor.annotation.CapacitorPlugin;

import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.io.OutputStream;
import java.nio.charset.StandardCharsets;

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

    /* ---------- leitura em lote da pasta do Urbe (Documentos/Urbe) ----------
       Abrir o app lia arquivo por arquivo pela ponte (centenas de chamadas: "Lendo 9 / 54").
       Agora a pasta inteira é listada numa chamada e os textos lidos em outra. */
    private File vaultDir() {
        return new File(Environment.getExternalStoragePublicDirectory(Environment.DIRECTORY_DOCUMENTS), "Urbe");
    }

    private void walk(File dir, String prefix, JSArray out, int depth) {
        if (depth > 32) return;
        File[] list = dir.listFiles();
        if (list == null) return;
        for (File f : list) {
            String rel = prefix.isEmpty() ? f.getName() : prefix + "/" + f.getName();
            JSObject e = new JSObject();
            e.put("path", rel);
            boolean isDir = f.isDirectory();
            e.put("kind", isDir ? "directory" : "file");
            e.put("size", isDir ? 0 : f.length());
            e.put("mtime", f.lastModified());
            out.put(e);
            if (isDir) walk(f, rel, out, depth + 1);
        }
    }

    @PluginMethod
    public void listTree(PluginCall call) {
        File root = vaultDir();
        JSObject r = new JSObject();
        if (!root.isDirectory()) {
            r.put("exists", false);
            r.put("entries", new JSArray());
            call.resolve(r);
            return;
        }
        JSArray out = new JSArray();
        walk(root, "", out, 0);
        r.put("exists", true);
        r.put("entries", out);
        call.resolve(r);
    }

    @PluginMethod
    public void readTexts(PluginCall call) {
        JSArray paths = call.getArray("paths");
        File root = vaultDir();
        JSObject files = new JSObject();
        try {
            String base = root.getCanonicalPath() + File.separator;
            for (int i = 0; paths != null && i < paths.length(); i++) {
                String p = paths.getString(i);
                if (p == null || p.contains("..")) continue;
                File f = new File(root, p);
                if (!f.getCanonicalPath().startsWith(base) || !f.isFile() || f.length() > 4 * 1024 * 1024) continue;
                try (InputStream in = new FileInputStream(f); ByteArrayOutputStream buf = new ByteArrayOutputStream((int) f.length())) {
                    byte[] chunk = new byte[65536];
                    int n;
                    while ((n = in.read(chunk)) > 0) buf.write(chunk, 0, n);
                    files.put(p, new String(buf.toByteArray(), StandardCharsets.UTF_8));
                } catch (Exception ignored) {
                }
            }
            JSObject r = new JSObject();
            r.put("files", files);
            call.resolve(r);
        } catch (Exception e) {
            call.reject("Não consegui ler a pasta: " + e.getMessage());
        }
    }

    @PluginMethod
    public void minimize(PluginCall call) {
        getActivity().runOnUiThread(() -> getActivity().moveTaskToBack(true));
        call.resolve();
    }
}
