using Concentus;
using Concentus.Oggfile;

namespace WebReader.Helpers;

public static class AudioConverter
{
    public static void ConvertWavToOpus(Stream inputWavStream, Stream outputMemoryStream, int bitrate = 96000)
    {
        inputWavStream.Position = 22;
        var channelsBuffer = new byte[2];
        inputWavStream.ReadExactly(channelsBuffer, 0, 2);
        var channels = BitConverter.ToInt16(channelsBuffer, 0);

        var sampleRateBuffer = new byte[4];
        inputWavStream.ReadExactly(sampleRateBuffer, 0, 4);
        var inputSampleRate = BitConverter.ToInt32(sampleRateBuffer, 0);

        var encoder = OpusCodecFactory.CreateEncoder(
            48000,
            channels
        );

        encoder.Bitrate = bitrate;

        using var tempStream = new MemoryStream();

        var oggOut = new OpusOggWriteStream(encoder, tempStream, inputSampleRate: inputSampleRate);

        inputWavStream.Seek(44, SeekOrigin.Begin);

        var byteBuffer = new byte[4096];
        int bytesRead;

        while ((bytesRead = inputWavStream.Read(byteBuffer, 0, byteBuffer.Length)) > 0)
        {
            var pcmSamples = new short[bytesRead / 2];
            Buffer.BlockCopy(byteBuffer, 0, pcmSamples, 0, bytesRead);

            oggOut.WriteSamples(pcmSamples, 0, pcmSamples.Length);
        }

        oggOut.Finish();

        var opusBytes = tempStream.ToArray();
        outputMemoryStream.Write(opusBytes, 0, opusBytes.Length);

        outputMemoryStream.Position = 0;
    }
}
