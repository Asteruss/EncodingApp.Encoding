using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EncodingApp.Encoding.Core;

public sealed class PipelineResult
{
    public byte[] Data { get; }

    public Dictionary<string, object>? Metadata { get; }

    internal PipelineResult(EncodingResult internalResult)
    {
        if (internalResult.RentedBuffer != null)
        {
            Data = internalResult.Data.ToArray();

            ArrayPool<byte>.Shared.Return(internalResult.RentedBuffer);
        }
        else
        {
            Data = [];
        }

        Metadata = internalResult.Metadata;
    }
}