package app.urbe;

import org.junit.Test;

/** JUnit4: as asserções vivem em GuardChecks (que também roda sem JUnit, via main). */
public class PathGuardTest {
    @Test
    public void caminhoRelativoSeguro() {
        GuardChecks.testPathSafeRelative();
    }

    @Test
    public void resolveDentroEFora() throws Exception {
        GuardChecks.testPathResolveInsideAndOutside();
    }

    @Test
    public void atalhoNaoEscapa() throws Exception {
        GuardChecks.testPathSymlinkEscape();
    }

    @Test
    public void leituraTamanhoETipo() throws Exception {
        GuardChecks.testPathReadableSizeAndKind();
    }

    @Test
    public void alvosDeEscrita() throws Exception {
        GuardChecks.testPathWriteTargets();
    }

    @Test
    public void detectaSymlink() throws Exception {
        GuardChecks.testPathIsSymlink();
    }

    @Test
    public void nomeDeArquivoSeguro() {
        GuardChecks.testPathSafeFileName();
    }
}
