using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AuraLe.Models;

/// <summary>
/// QWERTY 자판 키와 매핑된 단일 건반 모델
/// </summary>
public partial class QwertyKeyNote : ObservableObject
{
    public Key Key { get; init; }
    public char KeyChar { get; init; }
    public string NoteName { get; init; } = string.Empty;   // "도", "레", "미"
    public string NotePitch { get; init; } = string.Empty;  // "C5", "C4", "C3"
    public int MidiNote { get; init; }
    public string OctaveGroup { get; init; } = "Mid";       // "High", "Mid", "Low"

    [ObservableProperty]
    private bool _isPressed;

    public QwertyKeyNote(Key key, char keyChar, string noteName, string notePitch, int midiNote, string octaveGroup)
    {
        Key = key;
        KeyChar = keyChar;
        NoteName = noteName;
        NotePitch = notePitch;
        MidiNote = midiNote;
        OctaveGroup = octaveGroup;
    }
}
