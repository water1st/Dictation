using System.Buffers.Binary;
using System.Text;

namespace Dictation.Core.Tts
{
    public enum TtsPipeMessageType
    {
        Ping = 1,
        Synthesize = 2,
        Shutdown = 3,
        Pong = 4,
        Audio = 5,
        Error = 6,
        ShutdownAcknowledged = 7
    }

    public sealed record TtsPipeRequest(
        TtsPipeMessageType Type,
        TtsLanguage? Language = null,
        string? Text = null);

    public sealed record TtsPipeResponse(
        TtsPipeMessageType Type,
        byte[]? Payload = null,
        string? Error = null);

    public sealed class TtsPipeProtocolException : Exception
    {
        public TtsPipeProtocolException(string message) : base(message) { }

        public TtsPipeProtocolException(string message, Exception innerException)
            : base(message, innerException) { }
    }

    public static class TtsPipeCodec
    {
        public const byte ProtocolVersion = 1;
        public const int MaximumTextLength = 500;
        public const int MaximumTextUtf8Length = 2048;
        public const int MaximumErrorUtf8Length = 16 * 1024;
        public const int MaximumAudioLength = 16 * 1024 * 1024;

        private const byte NoLanguage = 0xff;
        private const int RequestHeaderLength = 7;
        private const int ResponseHeaderLength = 6;
        private static readonly UTF8Encoding StrictUtf8 = new(false, true);

        public static Task WriteRequestAsync(Stream stream, TtsPipeRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentNullException.ThrowIfNull(request);
            var payload = EncodeRequest(request, out var language);
            return WriteFrameAsync(stream, request.Type, language, payload, true, cancellationToken);
        }

        public static async Task<TtsPipeRequest> ReadRequestAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            var frame = await ReadFrameAsync(stream, true, cancellationToken).ConfigureAwait(false);
            var type = (TtsPipeMessageType)frame.Type;
            var language = ReadLanguage(frame.Language, type);
            return type is TtsPipeMessageType.Ping or TtsPipeMessageType.Shutdown
                ? new TtsPipeRequest(type)
                : new TtsPipeRequest(type, language, DecodeUtf8(frame.Payload, MaximumTextLength, true, "文本"));
        }

        public static Task WriteResponseAsync(Stream stream, TtsPipeResponse response, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentNullException.ThrowIfNull(response);
            return WriteFrameAsync(stream, response.Type, NoLanguage, EncodeResponse(response), false, cancellationToken);
        }

        public static async Task<TtsPipeResponse> ReadResponseAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            var frame = await ReadFrameAsync(stream, false, cancellationToken).ConfigureAwait(false);
            var type = (TtsPipeMessageType)frame.Type;
            return type switch
            {
                TtsPipeMessageType.Audio when frame.Payload.Length > 0
                    => new TtsPipeResponse(type, frame.Payload),
                TtsPipeMessageType.Audio
                    => throw new TtsPipeProtocolException("Audio 响应不能携带空 payload。"),
                TtsPipeMessageType.Error => new TtsPipeResponse(type, Error: DecodeUtf8(frame.Payload, -1, true, "错误信息")),
                TtsPipeMessageType.Pong or TtsPipeMessageType.ShutdownAcknowledged
                    => new TtsPipeResponse(type),
                _ => throw new TtsPipeProtocolException("响应消息类型无效。")
            };
        }

        private static byte[] EncodeRequest(TtsPipeRequest request, out byte language)
        {
            if (request.Type is TtsPipeMessageType.Ping or TtsPipeMessageType.Shutdown)
            {
                if (request.Language is not null || request.Text is not null)
                {
                    throw new TtsPipeProtocolException("Ping 或 Shutdown 请求不能包含语言和文本。");
                }

                language = NoLanguage;
                return Array.Empty<byte>();
            }

            if (request.Type != TtsPipeMessageType.Synthesize)
            {
                throw new TtsPipeProtocolException("请求消息类型未知。");
            }

            if (request.Language is null || !Enum.IsDefined(request.Language.Value))
            {
                throw new TtsPipeProtocolException("合成请求的语言无效。");
            }

            if (string.IsNullOrWhiteSpace(request.Text))
            {
                throw new TtsPipeProtocolException("合成请求的文本不能为空。");
            }

            if (request.Text.Length > MaximumTextLength)
            {
                throw new TtsPipeProtocolException("合成请求的文本不能超过 500 个字符。");
            }

            language = (byte)request.Language.Value;
            return EncodeUtf8(request.Text, MaximumTextUtf8Length, "文本");
        }

        private static byte[] EncodeResponse(TtsPipeResponse response)
        {
            if (response.Type is TtsPipeMessageType.Pong or TtsPipeMessageType.ShutdownAcknowledged)
            {
                if (response.Payload is { Length: > 0 } || response.Error is not null)
                {
                    throw new TtsPipeProtocolException("Pong 或 ShutdownAck 不能携带 payload。");
                }

                return Array.Empty<byte>();
            }

            if (response.Type == TtsPipeMessageType.Audio)
            {
                if (response.Payload is null || response.Payload.Length == 0)
                {
                    throw new TtsPipeProtocolException("Audio 响应不能携带空 payload。");
                }

                if (response.Payload.Length > MaximumAudioLength)
                {
                    throw new TtsPipeProtocolException("音频长度超过协议限制。");
                }

                if (response.Error is not null)
                {
                    throw new TtsPipeProtocolException("Audio 响应不能包含错误信息。");
                }

                return response.Payload;
            }

            if (response.Type == TtsPipeMessageType.Error)
            {
                if (response.Payload is { Length: > 0 } || string.IsNullOrWhiteSpace(response.Error))
                {
                    throw new TtsPipeProtocolException("Error 响应不能携带空白 payload。");
                }

                return EncodeUtf8(response.Error!, MaximumErrorUtf8Length, "错误信息");
            }

            throw new TtsPipeProtocolException("响应消息类型未知。");
        }

        private static async Task<Frame> ReadFrameAsync(
            Stream stream,
            bool request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(stream);
            var header = new byte[request ? RequestHeaderLength : ResponseHeaderLength];
            await ReadExactlyAsync(stream, header, "协议头", cancellationToken).ConfigureAwait(false);
            if (header[0] != ProtocolVersion)
            {
                throw new TtsPipeProtocolException("协议版本未知。");
            }

            var type = header[1];
            var maximum = PayloadLimit(type, request);
            var length = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(request ? 3 : 2));
            if (length < 0)
            {
                throw new TtsPipeProtocolException("payload 长度不能为负数。");
            }

            if (length > maximum)
            {
                throw new TtsPipeProtocolException("payload 长度超过协议限制。");
            }

            var payload = new byte[length];
            await ReadExactlyAsync(stream, payload, "payload", cancellationToken).ConfigureAwait(false);
            return new Frame(type, request ? header[2] : NoLanguage, payload);
        }

        private static int PayloadLimit(byte type, bool request) => (request, type) switch
        {
            (true, (byte)TtsPipeMessageType.Ping) or (true, (byte)TtsPipeMessageType.Shutdown)
                or (false, (byte)TtsPipeMessageType.Pong)
                or (false, (byte)TtsPipeMessageType.ShutdownAcknowledged) => 0,
            (true, (byte)TtsPipeMessageType.Synthesize) => MaximumTextUtf8Length,
            (false, (byte)TtsPipeMessageType.Audio) => MaximumAudioLength,
            (false, (byte)TtsPipeMessageType.Error) => MaximumErrorUtf8Length,
            (true, _) => throw new TtsPipeProtocolException("请求消息类型未知。"),
            _ => throw new TtsPipeProtocolException("响应消息类型未知。")
        };

        private static TtsLanguage? ReadLanguage(byte value, TtsPipeMessageType type) => (type, value) switch
        {
            (TtsPipeMessageType.Ping or TtsPipeMessageType.Shutdown, NoLanguage) => null,
            (TtsPipeMessageType.Ping or TtsPipeMessageType.Shutdown, _) =>
                throw new TtsPipeProtocolException("Ping 或 Shutdown 请求的语言必须为 0xFF。"),
            (_, _) when Enum.IsDefined((TtsLanguage)value) => (TtsLanguage)value,
            _ => throw new TtsPipeProtocolException("TTS 语言未知。")
        };

        private static byte[] EncodeUtf8(string value, int maximum, string name)
        {
            byte[] bytes;
            try
            {
                bytes = StrictUtf8.GetBytes(value);
            }
            catch (EncoderFallbackException exception)
            {
                throw new TtsPipeProtocolException($"{name}不是有效的 UTF-8 字符串。", exception);
            }

            if (bytes.Length > maximum)
            {
                throw new TtsPipeProtocolException($"{name} UTF-8 长度超过协议限制。");
            }

            return bytes;
        }

        private static string DecodeUtf8(byte[] payload, int maximumCharacters, bool required, string name)
        {
            string value;
            try
            {
                value = StrictUtf8.GetString(payload);
            }
            catch (DecoderFallbackException exception)
            {
                throw new TtsPipeProtocolException($"{name}不是有效的 UTF-8 字节序列。", exception);
            }

            if (required && string.IsNullOrWhiteSpace(value))
            {
                throw new TtsPipeProtocolException($"{name}不能是空白内容。");
            }

            if (maximumCharacters >= 0 && value.Length > maximumCharacters)
            {
                throw new TtsPipeProtocolException("合成请求的文本不能超过 500 个字符。");
            }

            return value;
        }

        private static async Task WriteFrameAsync(
            Stream stream,
            TtsPipeMessageType type,
            byte language,
            byte[] payload,
            bool request,
            CancellationToken cancellationToken)
        {
            var header = new byte[request ? RequestHeaderLength : ResponseHeaderLength];
            header[0] = ProtocolVersion;
            header[1] = (byte)type;
            if (request)
            {
                header[2] = language;
            }

            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(request ? 3 : 2), payload.Length);
            await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
            await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        private static async Task ReadExactlyAsync(Stream stream, Memory<byte> buffer, string name, CancellationToken cancellationToken)
        {
            try
            {
                await stream.ReadExactlyAsync(buffer, cancellationToken).ConfigureAwait(false);
            }
            catch (EndOfStreamException exception)
            {
                throw new TtsPipeProtocolException($"{name}被截断。", exception);
            }
        }

        private readonly record struct Frame(byte Type, byte Language, byte[] Payload);
    }
}
