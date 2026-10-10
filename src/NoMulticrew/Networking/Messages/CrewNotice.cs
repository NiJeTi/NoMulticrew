using Mirage.Serialization;

namespace NoMulticrew.Networking.Messages;

internal struct CrewNotice : IMessage<CrewNotice>
{
    public string Text { get; private set; }
    public NoticeTone Tone { get; private set; }
    public CrewCue Cue { get; private set; }

    public CrewNotice(string text, NoticeTone tone, CrewCue cue)
    {
        Text = text;
        Tone = tone;
        Cue = cue;
    }

    public void Read(NetworkReader reader)
    {
        Text = reader.ReadString();
        Tone = (NoticeTone)reader.ReadByte();
        Cue = (CrewCue)reader.ReadByte();
    }

    public void Write(NetworkWriter writer)
    {
        writer.WriteString(Text);
        writer.WriteByte((byte)Tone);
        writer.WriteByte((byte)Cue);
    }
}