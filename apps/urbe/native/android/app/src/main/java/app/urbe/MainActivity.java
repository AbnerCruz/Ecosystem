package app.urbe;

import android.os.Bundle;

import androidx.activity.OnBackPressedCallback;

import com.getcapacitor.BridgeActivity;

public class MainActivity extends BridgeActivity {
    @Override
    public void onCreate(Bundle savedInstanceState) {
        registerPlugin(UrbeAndroidPlugin.class);
        super.onCreate(savedInstanceState);
        /* o "voltar" do Android vai para o app decidir: fecha o que está aberto,
           sai da nota ou, na cidade, minimiza (nunca fecha o app de repente) */
        getOnBackPressedDispatcher().addCallback(this, new OnBackPressedCallback(true) {
            @Override
            public void handleOnBackPressed() {
                if (bridge != null) bridge.triggerDocumentJSEvent("urbeBack");
                else moveTaskToBack(true);
            }
        });
    }
}
