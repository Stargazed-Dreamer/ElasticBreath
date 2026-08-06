using System.IO;
using System.Media;
using ElasticBreath.App.Domain;

namespace ElasticBreath.App.Services;

/// <summary>
/// 提醒音效类型。不同事件触发不同音色，便于用户凭听觉辨识当前状态。
/// </summary>
public enum ReminderSoundKind
{
    /// <summary>状态切换提示（较弱）</summary>
    Transition,
    /// <summary>进入工作预警区</summary>
    WorkingWarning,
    /// <summary>进入工作硬性区</summary>
    WorkingHard,
    /// <summary>进入休息超时区</summary>
    RestOvertime
}

/// <summary>
/// 声音提醒服务。
/// 通过在内存中生成 16-bit PCM WAV 流并交由 <see cref="SoundPlayer"/> 异步播放，
/// 实现零外部依赖（无需 NAudio 等第三方库）的音量可控提示音。
/// 设计参考：design.md §8.4（声音提醒/提醒音量/全屏回退提示音）。
/// </summary>
/// <remarks>
/// 音量控制原理：<see cref="SoundPlayer"/> 本身不支持音量调节，
/// 因此在生成 WAV 时按 <see cref="ElasticBreathSettings.ReminderVolumePercent"/>
/// 缩放采样幅值，将音量"烘焙"进波形数据。
/// 所有播放均为异步（<see cref="SoundPlayer.Play"/> 在后台线程播放），
/// 不会阻塞 UI 线程；任何播放异常都被吞掉，确保音频永远不会导致应用崩溃。
/// </remarks>
public sealed class SoundService : IDisposable
{
    private const int SampleRate = 44100;
    private const short BitsPerSample = 16;
    private const short Channels = 1;

    private readonly ElasticBreathSettings _settings;
    private readonly Dictionary<ReminderSoundKind, byte[]> _cache = new();
    private byte[]? _fallbackBeepCache;
    private bool _disposed;

    public SoundService(ElasticBreathSettings settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// 播放指定类型的提醒音。受 <see cref="ElasticBreathSettings.EnableSound"/> 总开关控制。
    /// </summary>
    public void PlayReminder(ReminderSoundKind kind)
    {
        if (_disposed || !_settings.EnableSound)
        {
            return;
        }

        try
        {
            var wav = GetOrCreateReminder(kind);
            Play(wav);
        }
        catch
        {
            // 音频播放失败不应影响主功能
        }
    }

    /// <summary>
    /// 播放全屏回退短促提示音。受 <see cref="ElasticBreathSettings.EnableFullscreenFallbackBeep"/> 开关控制。
    /// 用于全屏应用遮挡光晕时的低频蜂鸣回退方案（design.md §7）。
    /// </summary>
    public void PlayFallbackBeep()
    {
        if (_disposed || !_settings.EnableFullscreenFallbackBeep)
        {
            return;
        }

        try
        {
            _fallbackBeepCache ??= BuildTone(1000, 0.10, 1.0);
            Play(_fallbackBeepCache);
        }
        catch
        {
            // 忽略
        }
    }

    private byte[] GetOrCreateReminder(ReminderSoundKind kind)
    {
        if (_cache.TryGetValue(kind, out var cached))
        {
            return cached;
        }

        var volumeScale = Math.Clamp(_settings.ReminderVolumePercent / 100.0, 0.0, 1.0);
        byte[] wav = kind switch
        {
            ReminderSoundKind.Transition => BuildTone(740, 0.12, 0.55 * volumeScale),
            ReminderSoundKind.WorkingWarning => BuildTone(880, 0.22, 0.75 * volumeScale),
            ReminderSoundKind.WorkingHard => BuildBeeps(660, 0.15, 0.08, 2, 0.85 * volumeScale),
            ReminderSoundKind.RestOvertime => BuildTone(990, 0.20, 0.75 * volumeScale),
            _ => BuildTone(800, 0.15, 0.6 * volumeScale)
        };
        _cache[kind] = wav;
        return wav;
    }

    /// <summary>
    /// 生成单音 WAV。带 5ms 线性attack/release 包络以消除爆音。
    /// </summary>
    /// <param name="frequencyHz">频率（赫兹）</param>
    /// <param name="durationSec">时长（秒）</param>
    /// <param name="amplitude">幅值 0.0~1.0</param>
    private static byte[] BuildTone(double frequencyHz, double durationSec, double amplitude)
    {
        amplitude = Math.Clamp(amplitude, 0.0, 1.0);
        var sampleCount = (int)(SampleRate * durationSec);
        var samples = new short[sampleCount];
        var rampSamples = Math.Min(sampleCount / 4, (int)(SampleRate * 0.005));

        var twoPiF = 2.0 * Math.PI * frequencyHz / SampleRate;
        var maxAmp = short.MaxValue * amplitude;

        for (var i = 0; i < sampleCount; i++)
        {
            var env = 1.0;
            if (i < rampSamples)
            {
                env = i / (double)rampSamples;
            }
            else if (i > sampleCount - rampSamples)
            {
                env = (sampleCount - i) / (double)rampSamples;
            }
            samples[i] = (short)(Math.Sin(twoPiF * i) * maxAmp * env);
        }

        return PcmToWav(samples);
    }

    /// <summary>
    /// 生成多短音 WAV（用于硬性区的双声提示）。
    /// </summary>
    /// <param name="frequencyHz">频率</param>
    /// <param name="beepSec">单声时长</param>
    /// <param name="gapSec">间隔时长（静音）</param>
    /// <param name="beepCount">声数</param>
    /// <param name="amplitude">幅值</param>
    private static byte[] BuildBeeps(double frequencyHz, double beepSec, double gapSec, int beepCount, double amplitude)
    {
        amplitude = Math.Clamp(amplitude, 0.0, 1.0);
        var beepSamples = (int)(SampleRate * beepSec);
        var gapSamples = (int)(SampleRate * gapSec);
        var total = (beepSamples * beepCount) + (gapSamples * (beepCount - 1));
        var samples = new short[total];
        var rampSamples = Math.Min(beepSamples / 4, (int)(SampleRate * 0.005));
        var twoPiF = 2.0 * Math.PI * frequencyHz / SampleRate;
        var maxAmp = short.MaxValue * amplitude;

        var offset = 0;
        for (var b = 0; b < beepCount; b++)
        {
            for (var i = 0; i < beepSamples; i++)
            {
                var env = 1.0;
                if (i < rampSamples)
                {
                    env = i / (double)rampSamples;
                }
                else if (i > beepSamples - rampSamples)
                {
                    env = (beepSamples - i) / (double)rampSamples;
                }
                samples[offset + i] = (short)(Math.Sin(twoPiF * i) * maxAmp * env);
            }
            offset += beepSamples;
            // gap 区域保持 0（静音），samples 默认已为 0
            if (b < beepCount - 1)
            {
                offset += gapSamples;
            }
        }

        return PcmToWav(samples);
    }

    /// <summary>
    /// 将 16-bit PCM 采样封装为可被 <see cref="SoundPlayer"/> 播放的 WAV 字节流。
    /// </summary>
    private static byte[] PcmToWav(short[] samples)
    {
        var dataLen = samples.Length * 2;
        var ms = new MemoryStream(44 + dataLen);
        using var w = new BinaryWriter(ms, System.Text.Encoding.ASCII, leaveOpen: true);
        // RIFF header
        w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        w.Write(36 + dataLen);
        w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        // fmt chunk
        w.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        w.Write(16);
        w.Write((short)1); // PCM
        w.Write(Channels);
        w.Write(SampleRate);
        w.Write(SampleRate * Channels * BitsPerSample / 8); // byteRate
        w.Write((short)(Channels * BitsPerSample / 8)); // blockAlign
        w.Write(BitsPerSample);
        // data chunk
        w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        w.Write(dataLen);
        var buffer = new byte[dataLen];
        Buffer.BlockCopy(samples, 0, buffer, 0, dataLen);
        w.Write(buffer);
        return ms.ToArray();
    }

    private static void Play(byte[] wav)
    {
        using var player = new SoundPlayer(new MemoryStream(wav, false));
        player.Play();
    }

    /// <summary>
    /// 在设置变更后调用，使缓存的 WAV 失效以便重新按新音量生成。
    /// </summary>
    public void InvalidateCache()
    {
        _cache.Clear();
        _fallbackBeepCache = null;
    }

    public void Dispose()
    {
        _disposed = true;
        _cache.Clear();
        _fallbackBeepCache = null;
    }
}
