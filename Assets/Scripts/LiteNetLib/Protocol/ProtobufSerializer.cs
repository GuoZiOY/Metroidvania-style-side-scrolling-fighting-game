using System;
using System.IO;
using ProtoBuf;

namespace LiteNetLib.Protocol
{

/// <summary>
/// 基于Protobuf-net的序列化器
/// </summary>
public static class ProtobufSerializer
{
    public static byte[] Serialize<T>(T message)
    {
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, message);
        return stream.ToArray();
    }

    public static T Deserialize<T>(byte[] data)
    {
        using var stream = new MemoryStream(data);
        return Serializer.Deserialize<T>(stream);
    }

    public static byte[] Serialize(object message)
    {
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, message);
        return stream.ToArray();
    }

    public static object Deserialize(byte[] data, Type type)
    {
        using var stream = new MemoryStream(data);
        return Serializer.Deserialize(type, stream);
    }
}}
