using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace zavod.Sage;

// S1 persistence for the sage_only channel.
//
// Observations are appended to
//   <projectRoot>/.zavod/sage/observations.jsonl
// one JSON object per line, UTF-8 without BOM.
//
// This file is write-only from the system's point of view:
// v2.1a guardrail #2 forbids reading it back into any role
// prompt (Lead/Worker/QC). The only legitimate readers are
// the Sage UI surface and future typed S3 rules.
public sealed class SageObservationSink
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly object _writeLock = new();

    public void Write(string projectRootPath, SageObservation observation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRootPath);
        ArgumentNullException.ThrowIfNull(observation);

        if (observation.Channel != SageChannel.SageOnly)
        {
            throw new InvalidOperationException(
                "S1 sink only accepts SageChannel.SageOnly. Other channels are reserved for future slices.");
        }

        var directory = Path.Combine(Path.GetFullPath(projectRootPath), ".zavod", "sage");
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, "observations.jsonl");

        var line = JsonSerializer.Serialize(observation, SerializerOptions);

        lock (_writeLock)
        {
            using var stream = new FileStream(file, FileMode.Append, FileAccess.Write, FileShare.Read);
            using var writer = new StreamWriter(stream, Utf8NoBom);
            writer.WriteLine(line);
        }
    }
}
