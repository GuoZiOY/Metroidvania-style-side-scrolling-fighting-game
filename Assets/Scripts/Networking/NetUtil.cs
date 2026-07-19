using System;
using UnityEngine;

namespace Networking
{
    // 网络工具类。提供手动打包/拆包方法，不依赖 DLL。
    // 格式：[4字节消息ID][消息体字节]
    public static class NetUtil
    {
        // 打包：[4字节msgId] + [body]
        public static byte[] PackMessage(int msgId, byte[] body)
        {
            byte[] packet = new byte[4 + body.Length];
            BitConverter.GetBytes(msgId).CopyTo(packet, 0);
            Buffer.BlockCopy(body, 0, packet, 4, body.Length);
            return packet;
        }

        // 拆包：取出 msgId 和 body
        public static (int msgId, byte[] body) UnpackMessage(byte[] packet)
        {
            int msgId = BitConverter.ToInt32(packet, 0);
            byte[] body = new byte[packet.Length - 4];
            Buffer.BlockCopy(packet, 4, body, 0, body.Length);
            return (msgId, body);
        }
    }
}
