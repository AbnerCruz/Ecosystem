package app.urbe;

import java.io.File;
import java.io.IOException;

/**
 * Validação de caminhos do plugin, sem nenhuma dependência de Android (roda em JVM pura nos testes).
 * Todo acesso a arquivo do UrbeAndroidPlugin passa por aqui: o caminho vem da página (não confiável)
 * e precisa ficar dentro da pasta base, sem "..", sem atalho (symlink) que escape e com tamanho limitado.
 */
public final class PathGuard {

    public static final long MAX_TEXT_BYTES = 4L * 1024 * 1024;
    public static final int MAX_PATH_CHARS = 1024;
    public static final int MAX_DEPTH = 32;
    public static final int MAX_NAME_CHARS = 120;

    private final File base;
    private final String basePrefix; // caminho canônico da base + separador

    public PathGuard(File base) throws IOException {
        this.base = base.getCanonicalFile();
        String p = this.base.getPath();
        this.basePrefix = p.endsWith(File.separator) ? p : p + File.separator;
    }

    public File base() {
        return base;
    }

    /** Caminho relativo textualmente aceitável: sem NUL, "\", "/" inicial, "." ou ".." como segmento. */
    public static boolean isSafeRelative(String rel) {
        if (rel == null || rel.length() > MAX_PATH_CHARS) return false;
        if (rel.isEmpty()) return true; // a própria base
        if (rel.indexOf('\0') >= 0 || rel.indexOf('\\') >= 0 || rel.charAt(0) == '/') return false;
        String[] parts = rel.split("/", -1);
        if (parts.length > MAX_DEPTH) return false;
        for (String s : parts) {
            if (s.equals("..") || s.equals(".")) return false;
        }
        return true;
    }

    /** Está dentro (ou é) a base, considerando o caminho canônico (segue atalhos)? */
    public boolean isInside(File f) {
        try {
            String c = f.getCanonicalPath();
            return c.equals(base.getPath()) || c.startsWith(basePrefix);
        } catch (IOException e) {
            return false;
        }
    }

    /**
     * Resolve um caminho relativo à base para leitura ou escrita. Lança SecurityException se o
     * caminho for malformado ou se o resultado canônico (com atalhos resolvidos) sair da base.
     * Serve também para arquivos que ainda não existem: o pai existente é resolvido.
     */
    public File resolve(String rel) {
        if (!isSafeRelative(rel)) throw new SecurityException("Caminho inválido");
        File f = rel.isEmpty() ? base : new File(base, rel);
        if (!isInside(f)) throw new SecurityException("Caminho fora da pasta do Urbe");
        return f;
    }

    /** Arquivo legível: existe, é arquivo comum, dentro da base e com até maxBytes. */
    public File resolveReadable(String rel, long maxBytes) {
        File f = resolve(rel);
        if (!f.isFile()) throw new SecurityException("Não é um arquivo");
        if (f.length() > maxBytes) throw new SecurityException("Arquivo grande demais");
        return f;
    }

    /** Alvo de escrita/remoção: nunca a própria base (a pasta do Urbe não é apagada pelo app). */
    public File resolveForWrite(String rel) {
        if (rel == null || rel.isEmpty()) throw new SecurityException("A pasta do Urbe não é alterada assim");
        File f = resolve(rel);
        File parent = f.getParentFile();
        if (parent == null || !isInside(parent)) throw new SecurityException("Caminho fora da pasta do Urbe");
        return f;
    }

    /** É um atalho (symlink)? Compara o caminho canônico com pai canônico + nome (sem java.nio, que exige API 26). */
    public static boolean isSymlink(File f) {
        try {
            File parent = f.getParentFile();
            File probe = parent == null ? f : new File(parent.getCanonicalFile(), f.getName());
            return !probe.getCanonicalFile().equals(probe.getAbsoluteFile());
        } catch (IOException e) {
            return true; // na dúvida, trata como atalho
        }
    }

    /** Nome de arquivo para gravar em Downloads/Urbe: sem separadores, sem ".." e com tamanho limitado. */
    public static String safeFileName(String name, String fallback) {
        String s = name == null ? "" : name.replaceAll("[\\u0000-\\u001f\\\\/:*?\"<>|]+", "-");
        s = s.replaceAll("^[.\\s-]+|[.\\s]+$", "");
        if (s.length() > MAX_NAME_CHARS) s = s.substring(0, MAX_NAME_CHARS);
        return s.isEmpty() ? fallback : s;
    }
}
