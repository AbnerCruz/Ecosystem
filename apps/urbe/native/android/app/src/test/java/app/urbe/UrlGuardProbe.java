package app.urbe;

import java.io.BufferedReader;
import java.io.InputStreamReader;
import java.nio.charset.StandardCharsets;

/**
 * Ferramenta de teste (sem JUnit): lê do stdin uma URL por linha, codificada em hexadecimal (UTF-8),
 * e imprime 1 (permitida) ou 0 (bloqueada) por linha. Usada por tests/security/links.mjs para
 * comparar o UrlGuard com o Electron e a ponte usando exatamente os mesmos vetores.
 */
public final class UrlGuardProbe {
    private UrlGuardProbe() {
    }

    public static void main(String[] args) throws Exception {
        BufferedReader in = new BufferedReader(new InputStreamReader(System.in, StandardCharsets.UTF_8));
        String line;
        while ((line = in.readLine()) != null) {
            int n = line.length() / 2;
            byte[] b = new byte[n];
            for (int i = 0; i < n; i++) b[i] = (byte) Integer.parseInt(line.substring(2 * i, 2 * i + 2), 16);
            System.out.println(UrlGuard.isAllowed(new String(b, StandardCharsets.UTF_8)) ? "1" : "0");
        }
    }
}
