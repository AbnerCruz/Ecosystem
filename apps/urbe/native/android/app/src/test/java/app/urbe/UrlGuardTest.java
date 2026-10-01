package app.urbe;

import org.junit.Test;

/** JUnit4: as asserções vivem em GuardChecks (que também roda sem JUnit, via main). */
public class UrlGuardTest {
    @Test
    public void permiteHttpHttpsMailtoTel() {
        GuardChecks.testUrlAllowed();
    }

    @Test
    public void bloqueiaOResto() {
        GuardChecks.testUrlBlocked();
    }
}
