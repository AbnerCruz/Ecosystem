package app.urbe;

import android.content.ContentResolver;
import android.database.Cursor;
import android.net.Uri;
import android.provider.DocumentsContract;
import android.provider.OpenableColumns;
import com.getcapacitor.JSArray;
import com.getcapacitor.JSObject;
import java.io.*;
import java.util.*;

/** Read-only SAF source selection. Domain detection and vault mutation do not belong here. */
final class ImportDocuments {
    static final long MAX_TOTAL = 256L * 1024 * 1024, MAX_FILE = 64L * 1024 * 1024;
    static final int MAX_FILES = 10000;
    private final ContentResolver resolver;
    private final File root;
    private final JSArray files = new JSArray();
    private long total;
    private int count;
    private final Set<String> visited = new HashSet<>();

    ImportDocuments(ContentResolver resolver, File root) throws IOException {
        this.resolver = resolver; this.root = root;
        if (!root.mkdirs()) throw new IOException("Não foi possível preparar os arquivos. Confira o espaço disponível.");
    }
    JSArray files() { return files; }
    void document(Uri uri) throws Exception {
        if (!"content".equals(uri.getScheme())) throw new IOException("O seletor não forneceu um documento seguro.");
        String name = null;
        try (Cursor c = resolver.query(uri, new String[]{OpenableColumns.DISPLAY_NAME}, null, null, null)) {
            if (c != null && c.moveToFirst()) name = c.getString(0);
        }
        add(uri, safeName(name));
    }
    void tree(Uri uri) throws Exception {
        if (!"content".equals(uri.getScheme())) throw new IOException("Pasta não reconhecida pelo seletor.");
        walk(uri, DocumentsContract.getTreeDocumentId(uri), "", 0);
    }
    private void walk(Uri tree, String id, String prefix, int depth) throws Exception {
        if (depth > 64 || !visited.add(id)) throw new IOException("A pasta contém uma estrutura circular ou profunda demais.");
        Uri children = DocumentsContract.buildChildDocumentsUriUsingTree(tree, id);
        try (Cursor c = resolver.query(children, new String[]{DocumentsContract.Document.COLUMN_DOCUMENT_ID,
                DocumentsContract.Document.COLUMN_DISPLAY_NAME, DocumentsContract.Document.COLUMN_MIME_TYPE}, null, null, null)) {
            if (c == null) throw new IOException("Não foi possível ler a pasta selecionada.");
            while (c.moveToNext()) {
                String childId = c.getString(0), name = safeName(c.getString(1));
                if (DocumentsContract.Document.MIME_TYPE_DIR.equals(c.getString(2))) walk(tree, childId, prefix + name + "/", depth + 1);
                else add(DocumentsContract.buildDocumentUriUsingTree(tree, childId), prefix + name);
            }
        }
    }
    private void add(Uri uri, String path) throws Exception {
        if (++count > MAX_FILES) throw new IOException("Há arquivos demais nesta seleção.");
        File target = new File(root, Integer.toString(count));
        long size;
        try (InputStream input = resolver.openInputStream(uri); FileOutputStream output = new FileOutputStream(target)) {
            if (input == null) throw new IOException("Não foi possível abrir " + path);
            size = copy(input, output, MAX_FILE, MAX_TOTAL - total);
            output.getFD().sync();
        }
        total += size;
        JSObject entry = new JSObject(); entry.put("id", Integer.toString(count)); entry.put("path", path); entry.put("size", size); files.put(entry);
    }
    static long copy(InputStream input, OutputStream output, long fileLimit, long remaining) throws IOException {
        byte[] chunk = new byte[65536]; long written = 0; int n;
        while ((n = input.read(chunk)) != -1) {
            if (Thread.currentThread().isInterrupted()) throw new InterruptedIOException("Importação cancelada.");
            if (written + n > fileLimit || written + n > remaining) throw new IOException("A seleção excede o limite seguro de importação.");
            output.write(chunk, 0, n); written += n;
        }
        return written;
    }
    static String safeName(String name) throws IOException {
        if (name == null || name.isEmpty() || name.equals(".") || name.equals("..") || name.indexOf('/') >= 0 || name.indexOf('\\') >= 0 || name.indexOf(':') >= 0)
            throw new IOException("O seletor retornou um nome de arquivo inválido.");
        for (int i = 0; i < name.length(); i++) if (name.charAt(i) < 32 || name.charAt(i) == 127) throw new IOException("Nome de arquivo inválido.");
        return name;
    }
    static void delete(File file) {
        File[] children = file.listFiles(); if (children != null) for (File child : children) delete(child);
        file.delete();
    }
}
