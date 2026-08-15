//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Text;
//using Fluxo.Core.IO;
//using Fluxo.Core.Util;

//namespace Fluxo.Core
//{
//    public static class ProxyInfoSerializer
//    {
//        public static void Serialize(ProxyInfo proxy, BinaryWriter w)
//        {
//            ConfigIO.SerializeProxyInfo(proxy, w);
//        }

//        //public static ProxyInfo? Deserialize(BinaryReader reader)
//        //{
//        //    if (reader.ReadBoolean())
//        //    {
//        //        return new ProxyInfo
//        //        {
//        //            Host = Fluxo.Messaging.StreamHelper.ReadString(reader),
//        //            Port = reader.ReadInt32(),
//        //            ProxyType = (ProxyType)reader.ReadInt32(),
//        //            UserName = Fluxo.Messaging.StreamHelper.ReadString(reader),
//        //            Password = Fluxo.Messaging.StreamHelper.ReadString(reader),
//        //        };
//        //    }
//        //    return null;
//        //}

        
//        public static ProxyInfo Deserialize(BinaryReader r)
//        {
//            return ConfigIO.DeserializeProxyInfo(r);
//        }
//    }
//}
