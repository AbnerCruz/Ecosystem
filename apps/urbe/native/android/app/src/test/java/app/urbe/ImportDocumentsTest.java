package app.urbe;
import org.junit.Test;
import static org.junit.Assert.*;
import java.io.*;
import java.nio.charset.StandardCharsets;
public class ImportDocumentsTest {
    @Test public void copiesBinaryNonSeekableStream() throws Exception {
        byte[] input = new byte[]{0,(byte)255,(byte)137,10}; ByteArrayOutputStream output = new ByteArrayOutputStream();
        InputStream stream = new FilterInputStream(new ByteArrayInputStream(input)) { @Override public boolean markSupported(){return false;} @Override public void reset() throws IOException {throw new IOException("not seekable");} };
        assertEquals(4,ImportDocuments.copy(stream,output,100,100));assertArrayEquals(input,output.toByteArray());
    }
    @Test public void acceptsUnicodeAndHiddenMetadataName() throws Exception {assertEquals("ação 🙂.md",ImportDocuments.safeName("ação 🙂.md"));assertEquals(".urbe",ImportDocuments.safeName(".urbe"));}
    @Test public void rejectsUnsafeNames() throws Exception {for(String name:new String[]{"..",".","../x","a/b","a\\b","content:doc","a\u0000b",""}){try{ImportDocuments.safeName(name);fail(name);}catch(IOException expected){}}}
    @Test public void enforcesActualBytesInsteadOfProviderReportedSize() throws Exception {ByteArrayOutputStream output=new ByteArrayOutputStream();try{ImportDocuments.copy(new ByteArrayInputStream(new byte[100]),output,10,1000);fail();}catch(IOException expected){assertEquals(0,output.size());}}
    @Test public void enforcesRemainingSelectionSpace() throws Exception {try{ImportDocuments.copy(new ByteArrayInputStream(new byte[100]),new ByteArrayOutputStream(),1000,10);fail();}catch(IOException expected){}}
    @Test public void propagatesProviderFailure() throws Exception {InputStream stream=new InputStream(){@Override public int read() throws IOException{throw new IOException("provider unavailable");}};try{ImportDocuments.copy(stream,new ByteArrayOutputStream(),100,100);fail();}catch(IOException expected){assertEquals("provider unavailable",expected.getMessage());}}
}
