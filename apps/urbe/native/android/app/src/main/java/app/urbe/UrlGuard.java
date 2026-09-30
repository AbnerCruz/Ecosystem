package app.urbe;

import java.util.Locale;

/**
 * Allowlist de esquemas de links externos (http, https, mailto, tel), sem dependência de Android.
 * Mesma regra do app de computador (native/desktop/guards.js) e da ponte (src/native/bridge.js).
 */
public final class UrlGuard {

    public static final int MAX_URL_CHARS = 8192;

    private UrlGuard() {
    }

    public static boolean isAllowed(String url) {
        if (url == null || url.isEmpty() || url.length() > MAX_URL_CHARS) return false;
        for (int i = 0; i < url.length(); i++) {
            char c = url.charAt(i);
            if (c < 0x20 || c == 0x7f) return false; // controles (e espaço/tab iniciais) não passam
        }
        int colon = url.indexOf(':');
        if (colon <= 0) return false;
        String scheme = url.substring(0, colon).toLowerCase(Locale.ROOT);
        String rest = url.substring(colon + 1);
        switch (scheme) {
            case "http":
            case "https": {
                if (!rest.startsWith("//")) return false;
                String auth = rest.substring(2);
                int end = auth.length();
                for (int i = 0; i < auth.length(); i++) {
                    char c = auth.charAt(i);
                    if (c == '/' || c == '?' || c == '#') {
                        end = i;
                        break;
                    }
                }
                String host = auth.substring(0, end);
                return !host.isEmpty() && !Character.isWhitespace(host.charAt(0));
            }
            case "mailto":
            case "tel":
                return !rest.isEmpty();
            default:
                return false;
        }
    }
}
