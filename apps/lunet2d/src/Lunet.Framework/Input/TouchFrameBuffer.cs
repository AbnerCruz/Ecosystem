using System.Numerics;

namespace Lunet.Input;

/// <summary>
/// Buffer interno do Preview Android. Compacta movimentos, mas mantém cada borda
/// Pressed/Released até que o Update fixo do jogo tenha sido executado.
/// Não cria objetos no caminho de entrada, leitura ou confirmação.
/// </summary>
internal sealed class TouchFrameBuffer
{
    private struct Slot
    {
        public bool Used;
        public int Id;
        public bool Down;
        public bool PressPending;
        public bool EndPending;
        public TouchPhase EndPhase;
        public Vector2 Position;
        public Vector2 PressPosition;
        public int PressVersion;
        public int EndVersion;
    }

    private readonly Slot[] _slots = new Slot[InputState.MaxTouches];
    private readonly int[] _frameIds = new int[InputState.MaxTouches];
    private readonly int[] _frameVersions = new int[InputState.MaxTouches];
    private readonly TouchPhase[] _framePhases = new TouchPhase[InputState.MaxTouches];
    private int _frameCount;
    private int _version;

    /// <summary>Recebe uma fotografia completa dos dedos de um MotionEvent na thread de UI.</summary>
    public void Submit(ReadOnlySpan<TouchPoint> touches)
    {
        var count = Math.Min(touches.Length, InputState.MaxTouches);
        for (var i = 0; i < count; i++)
        {
            var touch = touches[i];
            if (touch.Id < 0) continue;
            var slotIndex = Find(touch.Id);
            if (slotIndex < 0)
            {
                if (!touch.IsDown) continue;
                slotIndex = FreeSlot();
                if (slotIndex < 0) continue;
                _slots[slotIndex] = new Slot { Used = true, Id = touch.Id };
            }

            ref var slot = ref _slots[slotIndex];
            slot.Position = touch.Position;
            switch (touch.Phase)
            {
                case TouchPhase.Pressed:
                    slot.Down = true;
                    slot.PressPending = true;
                    slot.PressPosition = touch.Position;
                    slot.PressVersion = ++_version;
                    slot.EndPending = false;
                    break;
                case TouchPhase.Moved:
                    // Um evento de movimento não apaga um Pressed ainda não entregue.
                    if (!slot.EndPending) slot.Down = true;
                    break;
                case TouchPhase.Released:
                case TouchPhase.Cancelled:
                    slot.Down = false;
                    slot.EndPending = true;
                    slot.EndPhase = touch.Phase;
                    slot.EndVersion = ++_version;
                    break;
            }
        }

        // Android envia a lista completa de dedos. Uma ausência inesperada também
        // encerra a captura, evitando um analógico permanentemente preso.
        for (var i = 0; i < _slots.Length; i++)
        {
            ref var slot = ref _slots[i];
            if (!slot.Used || !slot.Down) continue;
            var found = false;
            for (var j = 0; j < count; j++)
                if (touches[j].Id == slot.Id) { found = true; break; }
            if (found) continue;
            slot.Down = false;
            slot.EndPending = true;
            slot.EndPhase = TouchPhase.Cancelled;
            slot.EndVersion = ++_version;
        }
    }

    /// <summary>Prepara os toques do próximo desenho sem consumir bordas.</summary>
    public int CopyFrame(Span<TouchPoint> destination)
    {
        if (destination.Length < InputState.MaxTouches)
            throw new ArgumentException("O destino precisa suportar MaxTouches.", nameof(destination));
        _frameCount = 0;
        for (var i = 0; i < _slots.Length; i++)
        {
            ref var slot = ref _slots[i];
            if (!slot.Used) continue;
            var phase = slot.PressPending ? TouchPhase.Pressed
                : slot.EndPending ? slot.EndPhase : TouchPhase.Moved;
            if (!slot.Down && !slot.PressPending && !slot.EndPending)
            {
                slot = default;
                continue;
            }

            destination[_frameCount] = new TouchPoint(slot.Id, phase,
                phase == TouchPhase.Pressed ? slot.PressPosition : slot.Position);
            _frameIds[_frameCount] = slot.Id;
            _framePhases[_frameCount] = phase;
            _frameVersions[_frameCount] = phase == TouchPhase.Pressed
                ? slot.PressVersion : slot.EndVersion;
            _frameCount++;
        }
        return _frameCount;
    }

    /// <summary>Confirma o frame somente depois que ao menos um Update/Step rodou.</summary>
    public void AcknowledgeFrame()
    {
        for (var i = 0; i < _frameCount; i++)
        {
            var slotIndex = Find(_frameIds[i]);
            if (slotIndex < 0) continue;
            ref var slot = ref _slots[slotIndex];
            if (_framePhases[i] == TouchPhase.Pressed
                && slot.PressPending && slot.PressVersion == _frameVersions[i])
                slot.PressPending = false;
            else if (_framePhases[i] is TouchPhase.Released or TouchPhase.Cancelled
                && slot.EndPending && slot.EndVersion == _frameVersions[i])
                slot = default;
        }
        _frameCount = 0;
    }

    /// <summary>Descarta toques ao reiniciar/parar uma sessão de jogo.</summary>
    public void Clear()
    {
        Array.Clear(_slots);
        _frameCount = 0;
    }

    private int Find(int id)
    {
        for (var i = 0; i < _slots.Length; i++)
            if (_slots[i].Used && _slots[i].Id == id) return i;
        return -1;
    }

    private int FreeSlot()
    {
        for (var i = 0; i < _slots.Length; i++)
            if (!_slots[i].Used) return i;
        return -1;
    }
}
