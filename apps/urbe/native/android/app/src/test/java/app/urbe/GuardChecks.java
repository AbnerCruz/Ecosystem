package app.urbe;

import java.io.File;
import java.io.IOException;
import java.lang.reflect.Method;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.Comparator;
import java.util.stream.Stream;

/**
 * Verificações de PathGuard e UrlGuard sem JUnit nem Android SDK (RM-F3-20).
 * É a fonte única das asserções: PathGuardTest/UrlGuardTest (JUnit4, rodados pelo Gradle) só chamam
 * estes métodos, e o main() abaixo roda todos com javac/java puros:
 *
 *   javac -d out native/android/app/src/main/java/app/urbe/PathGuard.java \
 *       native/android/app/src/main/java/app/urbe/UrlGuard.java \
 *       native/android/app/src/test/java/app/urbe/GuardChecks.java
 *   java -cp out app.urbe.GuardChecks
 */
public final class GuardChecks {

    private GuardChecks() {
    }

    static void check(boolean cond, String msg) {
        if (!cond) throw new AssertionError(msg);
    }

    static void mustThrow(Runnable r, String msg) {
        try {
            r.run();
        } catch (SecurityException e) {
            return;
        }
        throw new AssertionError("deveria lançar SecurityException: " + msg);
    }

    /** Pasta temporária: base/ (o vault) ao lado de fora/ (segredo). */
    static final class Sandbox implements AutoCloseable {
        final Path root, base, outside;

        Sandbox() throws IOException {
            root = Files.createTempDirectory("urbe-guard-");
            base = Files.createDirectory(root.resolve("Urbe"));
            outside = Files.createDirectory(root.resolve("fora"));
            Files.write(outside.resolve("segredo.txt"), "segredo".getBytes());
        }

        @Override
        public void close() throws IOException {
            try (Stream<Path> w = Files.walk(root)) {
                w.sorted(Comparator.reverseOrder()).forEach(p -> p.toFile().delete());
            }
        }
    }

    /** Cria symlink; devolve false se o sistema não permite (ex.: Windows sem privilégio). */
    static boolean symlink(Path link, Path target) {
        try {
            Files.createSymbolicLink(link, target);
            return true;
        } catch (IOException | UnsupportedOperationException | SecurityException e) {
            return false;
        }
    }

    // ---------------- PathGuard ----------------

    public static void testPathSafeRelative() {
        for (String ok : new String[]{"", "a", "a/b.md", "Notas/ção 🙂.md", "a/b/c/d", ".oculto/x", "a..b", "..a"})
            check(PathGuard.isSafeRelative(ok), "deveria aceitar: " + ok);
        for (String bad : new String[]{"..", "../x", "a/../b", "a/..", ".", "./a", "a/./b", "/etc/passwd", "\\x", "a\\b", "a\u0000b", "C:\\x\\..\\y"})
            check(!PathGuard.isSafeRelative(bad), "deveria recusar: " + bad.replace("\u0000", "<NUL>"));
        check(!PathGuard.isSafeRelative(null), "null");
        StringBuilder longo = new StringBuilder();
        for (int i = 0; i < PathGuard.MAX_PATH_CHARS + 1; i++) longo.append('a');
        check(!PathGuard.isSafeRelative(longo.toString()), "caminho longo demais");
        StringBuilder fundo = new StringBuilder("a");
        for (int i = 0; i < PathGuard.MAX_DEPTH + 1; i++) fundo.append("/a");
        check(!PathGuard.isSafeRelative(fundo.toString()), "profundidade demais");
    }

    public static void testPathResolveInsideAndOutside() throws IOException {
        try (Sandbox sb = new Sandbox()) {
            PathGuard g = new PathGuard(sb.base.toFile());
            Files.write(sb.base.resolve("nota.md"), "oi".getBytes());
            check(g.resolve("nota.md").equals(new File(g.base(), "nota.md")), "dentro");
            check(g.resolve("").equals(g.base()), "a própria base");
            check(g.resolve("novo/arquivo.md").getPath().startsWith(g.base().getPath()), "arquivo ainda inexistente");
            mustThrow(() -> g.resolve("../fora/segredo.txt"), "..");
            mustThrow(() -> g.resolve("a/../../fora/segredo.txt"), "a/../..");
            mustThrow(() -> g.resolve("/etc/passwd"), "absoluto");
            mustThrow(() -> g.resolve(null), "null");
            // irmão com o mesmo prefixo ("Urbe2") não conta como dentro de "Urbe"
            Files.createDirectory(sb.root.resolve("Urbe2"));
            check(!g.isInside(sb.root.resolve("Urbe2").toFile()), "prefixo parecido");
            check(!g.isInside(sb.outside.toFile()), "fora");
        }
    }

    public static void testPathSymlinkEscape() throws IOException {
        try (Sandbox sb = new Sandbox()) {
            PathGuard g = new PathGuard(sb.base.toFile());
            if (!symlink(sb.base.resolve("atalho"), sb.outside)) {
                System.out.println("  (symlink indisponível aqui; teste de atalho pulado)");
                return;
            }
            mustThrow(() -> g.resolve("atalho/segredo.txt"), "atalho para fora (arquivo)");
            mustThrow(() -> g.resolve("atalho"), "atalho para fora (pasta)");
            mustThrow(() -> g.resolveReadable("atalho/segredo.txt", 1024), "leitura via atalho");
            mustThrow(() -> g.resolveForWrite("atalho/novo.txt"), "escrita via atalho");
            // atalho que aponta para DENTRO da base é aceito
            Files.createDirectory(sb.base.resolve("real"));
            Files.write(sb.base.resolve("real/ok.txt"), "ok".getBytes());
            check(symlink(sb.base.resolve("interno"), sb.base.resolve("real")), "criar atalho interno");
            check(g.resolveReadable("interno/ok.txt", 1024).isFile(), "atalho interno");
            // atalho de arquivo para fora
            check(symlink(sb.base.resolve("s.txt"), sb.outside.resolve("segredo.txt")), "atalho de arquivo");
            mustThrow(() -> g.resolveReadable("s.txt", 1024), "atalho de arquivo para fora");
        }
    }

    public static void testPathReadableSizeAndKind() throws IOException {
        try (Sandbox sb = new Sandbox()) {
            PathGuard g = new PathGuard(sb.base.toFile());
            Files.write(sb.base.resolve("a.md"), new byte[100]);
            Files.createDirectory(sb.base.resolve("pasta"));
            check(g.resolveReadable("a.md", 100).length() == 100, "no limite");
            mustThrow(() -> g.resolveReadable("a.md", 99), "acima do limite");
            mustThrow(() -> g.resolveReadable("pasta", 100), "pasta não é arquivo");
            mustThrow(() -> g.resolveReadable("nao-existe.md", 100), "inexistente");
            check(PathGuard.MAX_TEXT_BYTES == 4L * 1024 * 1024, "limite de texto = 4 MB (igual ao desktop)");
        }
    }

    public static void testPathWriteTargets() throws IOException {
        try (Sandbox sb = new Sandbox()) {
            PathGuard g = new PathGuard(sb.base.toFile());
            check(g.resolveForWrite("x/y.md").getName().equals("y.md"), "alvo comum");
            mustThrow(() -> g.resolveForWrite(""), "a base não é alvo de escrita/remoção");
            mustThrow(() -> g.resolveForWrite(null), "null");
            mustThrow(() -> g.resolveForWrite("../x.md"), "..");
        }
    }

    public static void testPathIsSymlink() throws IOException {
        try (Sandbox sb = new Sandbox()) {
            Files.write(sb.base.resolve("a.txt"), "a".getBytes());
            check(!PathGuard.isSymlink(sb.base.resolve("a.txt").toFile()), "arquivo comum");
            check(!PathGuard.isSymlink(sb.base.toFile()), "pasta comum");
            if (symlink(sb.base.resolve("l"), sb.outside)) {
                check(PathGuard.isSymlink(sb.base.resolve("l").toFile()), "symlink de pasta");
            }
        }
    }

    public static void testPathSafeFileName() {
        check(PathGuard.safeFileName("livro.html", "arquivo").equals("livro.html"), "normal");
        check(PathGuard.safeFileName("a/b\\c:d*e?f\"g<h>i|j.md", "arquivo").equals("a-b-c-d-e-f-g-h-i-j.md"), "separadores e reservados");
        check(!PathGuard.safeFileName("../../etc/passwd", "arquivo").contains("/"), "sem barras");
        check(!PathGuard.safeFileName("..", "arquivo").equals(".."), "só pontos");
        check(PathGuard.safeFileName("", "arquivo").equals("arquivo"), "vazio");
        check(PathGuard.safeFileName(null, "arquivo").equals("arquivo"), "null");
        check(PathGuard.safeFileName("...", "arquivo").equals("arquivo"), "pontos");
        check(PathGuard.safeFileName("a\u0000b.txt", "arquivo").equals("a-b.txt"), "NUL");
        StringBuilder longo = new StringBuilder();
        for (int i = 0; i < 500; i++) longo.append('x');
        check(PathGuard.safeFileName(longo.toString(), "arquivo").length() <= PathGuard.MAX_NAME_CHARS, "tamanho");
    }

    // ---------------- UrlGuard ----------------

    public static void testUrlAllowed() {
        for (String ok : new String[]{"https://urbe.app", "http://example.com/a?b=c#d", "HTTPS://Example.com", "mailto:a@b.com", "tel:+5511999999999",
                "https://user@host:8080/x", "https://[::1]/x", "MAILTO:a@b.com"})
            check(UrlGuard.isAllowed(ok), "deveria permitir: " + ok);
    }

    public static void testUrlBlocked() {
        String[] bad = {"javascript:alert(1)", "JaVaScRiPt:alert(1)", "file:///etc/passwd", "data:text/html,<script>alert(1)</script>", "vbscript:msgbox(1)",
                "intent://scan/#Intent;scheme=zxing;end", "intent:#Intent;action=android.intent.action.VIEW;end", "content://media/x", "android-app://com.x", "ftp://x/y",
                "blob:https://x/y", "about:blank", "app://urbe/index.html", " https://x.com", "\thttps://x.com", "https://x.com\n", "https:x.com", "https:///x", "http://",
                "https://", "mailto:", "tel:", "https://a b.com", "https://a\\b", "https:\\\\x.com", "", "urbe", "://x", "javascript://https://x.com/%0aalert(1)", "java\u0000script:alert(1)"};
        for (String b : bad) check(!UrlGuard.isAllowed(b), "deveria bloquear: " + b.replace("\u0000", "<NUL>").replace("\n", "<LF>").replace("\t", "<TAB>"));
        check(!UrlGuard.isAllowed(null), "null");
        StringBuilder longo = new StringBuilder("https://x.com/");
        for (int i = 0; i < UrlGuard.MAX_URL_CHARS; i++) longo.append('a');
        check(!UrlGuard.isAllowed(longo.toString()), "URL gigante");
    }

    // ---------------- execução ----------------

    public static void main(String[] args) throws Exception {
        int ok = 0, fail = 0;
        Method[] ms = GuardChecks.class.getDeclaredMethods();
        java.util.Arrays.sort(ms, Comparator.comparing(Method::getName));
        for (Method m : ms) {
            if (!m.getName().startsWith("test") || m.getParameterCount() != 0) continue;
            try {
                m.invoke(null);
                ok++;
                System.out.println("OK   " + m.getName());
            } catch (java.lang.reflect.InvocationTargetException e) {
                fail++;
                System.out.println("FAIL " + m.getName() + ": " + e.getCause());
                e.getCause().printStackTrace(System.out);
            }
        }
        System.out.println(ok + " ok, " + fail + " falha(s)");
        if (fail > 0 || ok == 0) System.exit(1);
    }
}
